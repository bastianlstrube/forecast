using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidParticlesDispatch : MonoBehaviour
{

    // A definition for a 3D vector of integers
    [System.Serializable]
    public struct IntVector3
    {
        public int x;
        public int y;
        public int z;

        public IntVector3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }

    [System.Serializable]
    public struct LifeSpanStruct
    {
        public float min;
        public float max;
        public LifeSpanStruct(float _min, float _max)
        {
            min = _min;
            max = _max;
        }
    }

    public struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector4 color;
        public float timeElapsed;
        public float lifeSpan;
        public Vector3 spawnPosition;

        public Particle(Vector3 _position, Vector3 _velocity, Vector4 _color, float _timeElapsed, float _lifeSpan, Vector3 _spawnPosition)
        {
            position = _position;
            velocity = _velocity;
            color = _color;
            timeElapsed = _timeElapsed;
            lifeSpan = _lifeSpan;
            spawnPosition = _spawnPosition;
        }
    }

    [Header("Shaders")]
    //public ComputeShader fluidSourcesComputeShader;
    public ComputeShader fluidAffectorComputeShader;
    public ComputeShader diffuseFluidComputeShader;
    public ComputeShader advectFluidComputeShader;
    
    // PROJECTION STEPS
    public ComputeShader divergenceComputeShader;
    public ComputeShader jacobiComputeShader;   // multiple iterations
    public ComputeShader projectionComputeShader;

    public ComputeShader advectParticlesComputeShader;

    public ComputeShader vectorMeshComputeShader;

    public Shader particleSurfaceShader;
    public Shader vectorSurfaceShader;

    [Space(10.0f)]
    [Header("Fluid Solver Attributes")]
    public IntVector3 velocityBoxSize = new IntVector3(64, 64, 64);   // length of the sides of the particle box 
    public float diffusionRate = 0.1f;
    public bool drawVelocityVectors = false;
    public bool useRandomStartVelocities = false;

    [Space(10.0f)]
    [Header("Particle System Attributes")]
    public int numParticles = 65535;
    [Range(0,1)]
    public float drag = 0.9f;
    public Vector3 constantForces = Vector3.zero;

    [Space(10.0f)]
    [Header("Fluid Affector Properties")]
    public Transform affector;
    public float affectorSize = 1f;
    [Range(0,2)]
    public float affectorVelocityScale = 1f;

    [Space(10.0f)]
    [Header("Particle Properties")]
    public Texture2D particleSprite;
    public Vector2 particleSize = Vector2.one;
    public LifeSpanStruct particleLifeSpan = new LifeSpanStruct(1, 5);
    public float sizeByVelocity = 10.0f;
    public Color globalTint = Color.white;
    public bool useVelocityAlpha = true;

    // compute shader buffers for retrieving data from and sending data to the compute shader
    //private ComputeBuffer fluidSourcesBuffer;
    private ComputeBuffer flowMapBuffer;
    private ComputeBuffer flowMapBufferPrev;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer divergenceBuffer;
    private ComputeBuffer pressureBuffer;
    private ComputeBuffer meshPointsBuffer;

    // variables
    private int boxVolume;  // volume of the box, based off the side length
    private Vector3 affectorPrev;   // stores previous position of the affector

    // kernels
    private int fluidAffectorKernel;
    //private int fluidSourcesKernel;
    private int diffuseFluidKernel;
    private int advectFluidKernel;
    private int divergenceKernel;
    private int jacobiKernel;
    private int projectionKernel;
    private int advectParticleKernel;
    private int meshComputeKernel;

    // materials
    private Material vectorMaterial;  // vector material created from the vector shader
    private Material particleMaterial;  // particle material created from the particle shader

    //private OSCReceiver oscReceiver;

    void Start()
    {
        affectorPrev = affector.transform.position;

        // calculate box volume
        boxVolume = velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z;

        // initialise materials
        vectorMaterial = new Material(vectorSurfaceShader);
        particleMaterial = new Material(particleSurfaceShader);

        // find the compute shader's "main" function and store it
        //fluidSourcesKernel = fluidSourcesComputeShader.FindKernel("AddFluidSources");
        fluidAffectorKernel = fluidAffectorComputeShader.FindKernel("AddFluidAffector");
        diffuseFluidKernel = diffuseFluidComputeShader.FindKernel("DiffuseFluid");
        advectFluidKernel = advectFluidComputeShader.FindKernel("AdvectFluid");
        advectParticleKernel = advectParticlesComputeShader.FindKernel("AdvectParticles");
        divergenceKernel = divergenceComputeShader.FindKernel("ComputeDivergence");
        jacobiKernel = jacobiComputeShader.FindKernel("SolveJacobi");
        projectionKernel = projectionComputeShader.FindKernel("ProjectVelocities");
        meshComputeKernel = vectorMeshComputeShader.FindKernel("CreateVectorMesh");

        // create 1 dimensional buffer of float3's with a length of the box volume
        // this stores the particles' positions and colours
        flowMapBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        flowMapBufferPrev = new ComputeBuffer(boxVolume, sizeof(float) * 3);

        divergenceBuffer = new ComputeBuffer(boxVolume, sizeof(float));
        pressureBuffer = new ComputeBuffer(boxVolume, sizeof(float));
        
        particleBuffer = new ComputeBuffer(numParticles, (sizeof(float) * 3) * 3 + (sizeof(float) * 4) + (sizeof(float)) * 2);

        meshPointsBuffer = new ComputeBuffer(boxVolume * 2, sizeof(float) * 3);

        //fluidSourcesBuffer = new ComputeBuffer(OSCReceiver.maxNumSources, sizeof(float) * 6);

        InitialiseVectorMap();
        InitialiseParticles();
        InitialiseProjectionBuffers();

        //oscReceiver = GetComponent<OSCReceiver>();

        InitialiseFluidSourcesBuffer();
    }

    void InitialiseVectorMap()
    {
        Vector3[] flowMap = new Vector3[velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z];
        for (int i = 0; i < velocityBoxSize.x*velocityBoxSize.y*velocityBoxSize.z; i++)
        {
            if (useRandomStartVelocities)
                flowMap[i] = new Vector3(Random.Range(-.5f, .5f), Random.Range(-.5f, .5f), Random.Range(-.5f, .5f));
            else
                flowMap[i] = Vector3.zero;
        }

        flowMapBuffer.SetData(flowMap);
        flowMapBufferPrev.SetData(flowMap);
    }

    void InitialiseParticles()
    {
        Particle[] particleMap = new Particle[numParticles];
        for (int i = 0; i < numParticles; i++)
        {
            Vector3 spawnPosition = new Vector3(Random.Range(0.0f, velocityBoxSize.x), Random.Range(0.0f, velocityBoxSize.y), Random.Range(0.0f, velocityBoxSize.z));

            particleMap[i] = new Particle(spawnPosition, Vector3.zero, Vector4.zero, 0, Random.Range(particleLifeSpan.min, particleLifeSpan.max), spawnPosition);
        }

        particleBuffer.SetData(particleMap);
    }

    void InitialiseProjectionBuffers()
    {
        float[] initData = new float[velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z];
        for (int i = 0; i < velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z; i++)
        {
            initData[i] = 0f;
        }
        divergenceBuffer.SetData(initData);
        pressureBuffer.SetData(initData);
    }

    void InitialiseFluidSourcesBuffer()
    {
        //fluidSourcesBuffer.SetData(oscReceiver.sourceMap);
    }

    void AddFluidAffectorVelocity(Vector3 difference)
    {
        fluidAffectorComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        fluidAffectorComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        fluidAffectorComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        fluidAffectorComputeShader.SetFloat("timeStep", Time.deltaTime);
        fluidAffectorComputeShader.SetFloat("size", affectorSize);
        fluidAffectorComputeShader.SetVector("sourcePosition", affector.transform.position);
        fluidAffectorComputeShader.SetVector("sourceVelocity", difference * affectorVelocityScale);
        fluidAffectorComputeShader.SetVector("constantForces", constantForces);
        fluidAffectorComputeShader.SetBuffer(fluidAffectorKernel, "flowBuffer", flowMapBuffer);
        fluidAffectorComputeShader.Dispatch(fluidAffectorKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
        
    }

    void AddFluidSources()
    {
        /*
        if (oscReceiver.hasVelocity)
        {
            fluidSourcesBuffer.SetData(oscReceiver.sourceMap);

            fluidSourcesComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
            fluidSourcesComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
            fluidSourcesComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
            fluidSourcesComputeShader.SetFloat("timeStep", Time.deltaTime);
            fluidSourcesComputeShader.SetBuffer(fluidSourcesKernel, "flowBuffer", flowMapBuffer);
            fluidSourcesComputeShader.SetBuffer(fluidSourcesKernel, "fluidSourcesBuffer", fluidSourcesBuffer);
            fluidSourcesComputeShader.Dispatch(fluidSourcesKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);

            oscReceiver.ResetSourceMap();
        }
        */
    }

    void DiffuseFluid(int iterations)
    {
        

        for (int i = 0; i < iterations; i++)
        {
            diffuseFluidComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
            diffuseFluidComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
            diffuseFluidComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
            diffuseFluidComputeShader.SetFloat("diffusionRate", diffusionRate);
            diffuseFluidComputeShader.SetFloat("timeStep", Time.deltaTime);

            if (i % 2 == 0)
            {
                diffuseFluidComputeShader.SetBuffer(diffuseFluidKernel, "flowBuffer", flowMapBuffer);
                diffuseFluidComputeShader.SetBuffer(diffuseFluidKernel, "flowBufferPrev", flowMapBufferPrev);
            }
            else
            {
                diffuseFluidComputeShader.SetBuffer(diffuseFluidKernel, "flowBufferPrev", flowMapBuffer);
                diffuseFluidComputeShader.SetBuffer(diffuseFluidKernel, "flowBuffer", flowMapBufferPrev);
            }
            diffuseFluidComputeShader.Dispatch(diffuseFluidKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
        }
    }

    void AdvectFluid()
    {
        advectFluidComputeShader.SetBuffer(advectFluidKernel, "flowBuffer", flowMapBuffer);
        advectFluidComputeShader.SetBuffer(advectFluidKernel, "flowBufferPrev", flowMapBufferPrev);
        advectFluidComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        advectFluidComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        advectFluidComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        advectFluidComputeShader.SetFloat("timeStep", Time.deltaTime);
        diffuseFluidComputeShader.Dispatch(advectFluidKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void AdvectParticles()
    {
        advectParticlesComputeShader.SetBuffer(advectParticleKernel, "flowBuffer", flowMapBuffer);
        advectParticlesComputeShader.SetBuffer(advectParticleKernel, "particleBuffer", particleBuffer);
        advectParticlesComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        advectParticlesComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        advectParticlesComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        advectParticlesComputeShader.SetFloat("drag", drag);
        if (useVelocityAlpha)
            advectParticlesComputeShader.SetInt("velocityAlpha", 0);
        else
            advectParticlesComputeShader.SetInt("velocityAlpha", 1);
        advectParticlesComputeShader.SetFloat("timeStep", Time.deltaTime);

        advectParticlesComputeShader.Dispatch(advectParticleKernel, numParticles / 512, 1, 1);


    }

    void DrawVelocityVectors()
    {
        vectorMeshComputeShader.SetBuffer(meshComputeKernel, "vectorMapBuffer", flowMapBuffer);
        vectorMeshComputeShader.SetBuffer(meshComputeKernel, "meshPointBuffer", meshPointsBuffer);
        vectorMeshComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        vectorMeshComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        vectorMeshComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        vectorMeshComputeShader.Dispatch(meshComputeKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void ProjectFluid(int iterations)
    {
        ComputeDivergence();

        for (int i = 0; i < iterations; i++)
        {
            SolveJacobi();
        }

        ProjectVelocities();
    }

    void ComputeDivergence()
    {
        divergenceComputeShader.SetBuffer(divergenceKernel, "flowBuffer", flowMapBuffer);
        divergenceComputeShader.SetBuffer(divergenceKernel, "divergenceBuffer", divergenceBuffer);
        divergenceComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        divergenceComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        divergenceComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        divergenceComputeShader.Dispatch(divergenceKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void SolveJacobi()
    {
        jacobiComputeShader.SetBuffer(jacobiKernel, "pressureBuffer", pressureBuffer);
        jacobiComputeShader.SetBuffer(jacobiKernel, "divergenceBuffer", divergenceBuffer);
        jacobiComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        jacobiComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        jacobiComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        jacobiComputeShader.Dispatch(jacobiKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void ProjectVelocities()
    {
        projectionComputeShader.SetBuffer(projectionKernel, "pressureBuffer", pressureBuffer);
        projectionComputeShader.SetBuffer(projectionKernel, "flowBuffer", flowMapBuffer);
        projectionComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        projectionComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        projectionComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        projectionComputeShader.Dispatch(projectionKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    private void Update()
    {

        // make the affector push the fluid around
        AddFluidAffectorVelocity(affectorPrev - affector.transform.position);

        // save affector position
        affectorPrev = affector.transform.position;

        // add velocity sources from the Astra optical flow
        AddFluidSources();

        // diffuse the fluid, with 20 iterations of smoothing
        DiffuseFluid(20);

        // project the fluid, with 20 jacobi iterations
        ProjectFluid(20);

        // advect the fluid along its own velocities
        AdvectFluid();

        // project the fluid again, with 20 jacobi iterations
        ProjectFluid(20);

        // move the particles along the fluid
        AdvectParticles();

        // draw the vector map in the editor
        if (drawVelocityVectors)
            DrawVelocityVectors();
    }

    // render the materials
    void OnRenderObject()
    {
        // procedurally draw all the particles as a set of points with the particle material
        particleMaterial.SetPass(0);
        particleMaterial.SetColor("_Tint", globalTint);
        particleMaterial.SetBuffer("particles", particleBuffer);
        particleMaterial.SetTexture("_Sprite", particleSprite);
        particleMaterial.SetVector("_Size", particleSize);
        particleMaterial.SetFloat("_SizeByVelocity", sizeByVelocity);
        particleMaterial.SetVector("_worldPos", transform.position);

        Graphics.DrawProcedural(MeshTopology.Points, particleBuffer.count);

        // draw the velocity vectors
        if (drawVelocityVectors)
        {
            vectorMaterial.SetPass(0);
            vectorMaterial.SetBuffer("buf_Points", meshPointsBuffer);

            Graphics.DrawProcedural(MeshTopology.Lines, meshPointsBuffer.count);
        }
    }

    // when this GameObject is disabled, release the buffers and materials
    private void OnDisable()
    {
        flowMapBuffer.Release();
        flowMapBufferPrev.Release();
        divergenceBuffer.Release();
        pressureBuffer.Release();
        particleBuffer.Release();
        meshPointsBuffer.Release();
        //fluidSourcesBuffer.Release();
           
        DestroyImmediate(vectorMaterial);
        DestroyImmediate(particleMaterial);
    }
}
