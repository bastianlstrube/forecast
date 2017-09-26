using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleTrailSimulation : MonoBehaviour {

    // Vector of ints rather than floats
    [System.Serializable]
    public struct IntVector3
    {
        public int x;
        public int y;
        public int z;

        // Constructor
        public IntVector3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }

    public struct TrailHead
    {
        public Vector3 position;
        public Vector3 direction;
        public float speed;
        public float timeAlive;
        public float lifeSpan;
        public Vector3 spawnPosition;

        public TrailHead(Vector3 _position, Vector3 _direction, float _speed, float _timeAlive, float _lifeSpan, Vector3 _spawnPosition)
        {
            position = _position;
            direction = _direction;
            speed = _speed;
            timeAlive = _timeAlive;
            lifeSpan = _lifeSpan;
            spawnPosition = _spawnPosition;
        }
    }

    public struct TrailParticle
    {
        public Vector3 position;
        public Vector3 albedo;
        public Vector3 emissive;
        public float alpha;
        public float scale;

        public TrailParticle(Vector3 _position, Vector3 _albedo, Vector3 _emissive, float _alpha, float _scale)
        {
            position = _position;
            albedo = _albedo;
            emissive = _emissive;
            alpha = _alpha;
            scale = _scale;
        }
    }

    [Header("Compute Shaders")]
    // Push the particles around
    public ComputeShader moveParticleTrails_compute;
    public ComputeShader updateParticleTrailBuffers_compute;
    public ComputeShader evaluateVelocitySources_compute;
    public ComputeShader paintFlow_compute;
    public ComputeShader resetRealtimeFlowMap_compute;

    // Used for debugging the fluid vector field
    public ComputeShader generateVectorMesh_compute;

    [Header("Surface Shaders")]
    public Shader particleSurfaceShader;    // billboard particle shader
    public Shader vectorSurfaceShader;

    [Space(10.0f)]
    [Header("Flow Attributes")]
    public IntVector3 velocityBoxSize = new IntVector3(128, 128, 128);   // How large the vector field is for computing the fluid movement
    public bool drawVelocityVectors = false;
    public bool useRandomStartVelocities = false;
    public int maxNumberOfFluidSources = 1024;
    public OSCReceiver oscReceiver;

    [Space(10.0f)]
    [Header("Particle System Attributes")]
    public int numberOfTrails = 17408;
    public int particlesPerTrail = 8;
    public float baseSpeed = 1.0f;
    [Range(0, 1)]
    public float drag = 0.95f;   // drag on the particles, not the fluid viscosity

    [Space(10.0f)]
    [Header("Particle Properties")]
    public Texture2D particleSprite;
    public Vector2 particleSize = new Vector2(0.02f, 0.02f);
    public float lifeSpanMin = 3.0f;
    public float lifeSpanMax = 5.0f;
    public Color globalTint = Color.white;

    // Buffers for storing data in the compute buffer
    private ComputeBuffer baseFlowBuffer;
    private ComputeBuffer userFlowBuffer;
    private ComputeBuffer userFlowBufferPrev;
    private ComputeBuffer velocitySourcesBuffer;

    private ComputeBuffer trailHeadBuffer;
    private ComputeBuffer trailParticleBuffer;
    private ComputeBuffer trailParticleBufferPrev;

    private ComputeBuffer meshPointsBuffer;

    // kernels
    private int moveParticleTrails_kernel;
    private int updateParticleTrailBuffers_kernel;
    private int evaluateVelocitySources_kernel;
    private int resetRealtimeFlowMap_kernel;

    private int paintFlow_kernel;
    private int generateVectorMesh_kernel;

    // materials
    private Material vectorMaterial;    // vector material created from the vector shader
    private Material particleMaterial;  // particle material created from the particle shader

    // Other variables
    private int boxVolume;  // volume of the box, calculated at runtime from side length
    
    [HideInInspector]
    public Vector3 flowpainterSourcePosition = Vector3.zero;
    [HideInInspector]
    public float flowpainterBrushDistance = 0.0f;
    [HideInInspector]
    public Vector3 flowpainterSourceVelocity = Vector3.zero;
    [HideInInspector]
    public float flowpainterBrushSize = 1f;
    [HideInInspector]
    public bool erasing = false;

    void Start()
    {
        // calculate box volume
        boxVolume = velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z;

        // initialise materials
        vectorMaterial = new Material(vectorSurfaceShader);
        particleMaterial = new Material(particleSurfaceShader);

        // find the compute shader's "main" function and store it
        moveParticleTrails_kernel = moveParticleTrails_compute.FindKernel("MoveParticleTrails");
        updateParticleTrailBuffers_kernel = updateParticleTrailBuffers_compute.FindKernel("UpdateParticleTrailBuffers");
        generateVectorMesh_kernel = generateVectorMesh_compute.FindKernel("GenerateVectorMesh");
        paintFlow_kernel = paintFlow_compute.FindKernel("PaintFlow");
        evaluateVelocitySources_kernel = evaluateVelocitySources_compute.FindKernel("EvaluateVelocitySources");
        resetRealtimeFlowMap_kernel = resetRealtimeFlowMap_compute.FindKernel("ResetRealtimeFlowMap");

        // create 1 dimensional buffer of float3's with a length of the box volume
        // this stores the particles' positions and colours
        baseFlowBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        userFlowBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        userFlowBufferPrev = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        velocitySourcesBuffer = new ComputeBuffer(maxNumberOfFluidSources, sizeof(float) * 6);

        trailHeadBuffer = new ComputeBuffer(numberOfTrails, sizeof(float) * 12);
        trailParticleBuffer = new ComputeBuffer(numberOfTrails * particlesPerTrail, sizeof(float) * 11);
        trailParticleBufferPrev = new ComputeBuffer(numberOfTrails * particlesPerTrail, sizeof(float) * 11);

        meshPointsBuffer = new ComputeBuffer(boxVolume * 2, sizeof(float) * 3 + sizeof(float) * 4);

        InitialiseConstantFlowBuffer();
        InitialiseParticles();
        InitialiseRealtimeFlowBuffer();
    }

    public void InitialiseConstantFlowBuffer()
    {
        Vector3[] constantFlowMap = new Vector3[boxVolume];
        for (int i = 0; i < boxVolume; i++)
        {
            if (useRandomStartVelocities)
            {
                constantFlowMap[i] = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
                constantFlowMap[i] = constantFlowMap[i] / constantFlowMap[i].magnitude;
            }
            else
            {
                constantFlowMap[i] = Vector3.zero;
            }
        }

        baseFlowBuffer.SetData(constantFlowMap);
    }

    public void InitialiseRealtimeFlowBuffer()
    {
        Vector3[] realtimeFlowMap = new Vector3[boxVolume];
        for (int i = 0; i < boxVolume; i++)
        {
            realtimeFlowMap[i] = Vector3.zero;
        }
        userFlowBuffer.SetData(realtimeFlowMap);
        userFlowBufferPrev.SetData(realtimeFlowMap);
    }

    void InitialiseParticles()
    {
        TrailHead[] trailHeadArray = new TrailHead[numberOfTrails];
        TrailParticle[] trailParticleArray = new TrailParticle[numberOfTrails*particlesPerTrail];

        Vector3 prevPos = Vector3.zero;

        for (int i = 0; i < numberOfTrails; i++)
        {
            Vector3 position = new Vector3(Random.Range(0.0f, velocityBoxSize.x), Random.Range(0.0f, velocityBoxSize.y), Random.Range(0.0f, velocityBoxSize.z));
            trailHeadArray[i] = new TrailHead(position,Vector3.zero,baseSpeed,0,Random.Range(lifeSpanMin,lifeSpanMax), position);

            for (int j = 0; j < particlesPerTrail; j++)
            {
                trailParticleArray[i * particlesPerTrail + j] = new TrailParticle(position, Vector3.one, Vector3.zero, 1.0f, j);
            }
        }

        trailHeadBuffer.SetData(trailHeadArray);
        trailParticleBuffer.SetData(trailParticleArray);
        trailParticleBufferPrev.SetData(trailParticleArray);
    }

    void MoveParticles()
    {
        // SET BUFFERS
        moveParticleTrails_compute.SetBuffer(moveParticleTrails_kernel, "trailHeadBuffer", trailHeadBuffer);
        moveParticleTrails_compute.SetBuffer(moveParticleTrails_kernel, "trailParticleBuffer", trailParticleBuffer);
        moveParticleTrails_compute.SetBuffer(moveParticleTrails_kernel, "trailParticleBufferPrev", trailParticleBufferPrev);
        moveParticleTrails_compute.SetBuffer(moveParticleTrails_kernel, "baseFlowMap", baseFlowBuffer);
        moveParticleTrails_compute.SetBuffer(moveParticleTrails_kernel, "userFlowMap", userFlowBuffer);

        // SET VARIABLES
        moveParticleTrails_compute.SetInt("particlesPerTrail", particlesPerTrail);
        moveParticleTrails_compute.SetVector("velocityBoxSize", new Vector3(velocityBoxSize.x, velocityBoxSize.y, velocityBoxSize.z));
        moveParticleTrails_compute.SetFloat("timeStep", Time.deltaTime);
        moveParticleTrails_compute.SetFloat("baseSpeed", baseSpeed);
        moveParticleTrails_compute.SetFloat("drag", drag);

        moveParticleTrails_compute.Dispatch(moveParticleTrails_kernel, numberOfTrails/1024, 1, 1);

        // UPDATE BUFFERS
        updateParticleTrailBuffers_compute.SetBuffer(updateParticleTrailBuffers_kernel, "trailParticleBuffer", trailParticleBuffer);
        updateParticleTrailBuffers_compute.SetBuffer(updateParticleTrailBuffers_kernel, "trailParticleBufferPrev", trailParticleBufferPrev);

        updateParticleTrailBuffers_compute.Dispatch(updateParticleTrailBuffers_kernel, numberOfTrails * particlesPerTrail / 1024, 1, 1);
    }

    void DrawVelocityVectors()
    {
        generateVectorMesh_compute.SetVector("worldPos", transform.position);
        generateVectorMesh_compute.SetVector("worldScale", transform.localScale);
        generateVectorMesh_compute.SetBuffer(generateVectorMesh_kernel, "vectorMapBuffer", baseFlowBuffer);
        generateVectorMesh_compute.SetBuffer(generateVectorMesh_kernel, "meshPointBuffer", meshPointsBuffer);
        generateVectorMesh_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        generateVectorMesh_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        generateVectorMesh_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        generateVectorMesh_compute.SetVector("brushPosition", flowpainterSourcePosition);
        generateVectorMesh_compute.SetFloat("brushSize", flowpainterBrushSize);
        generateVectorMesh_compute.Dispatch(generateVectorMesh_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void PaintFlow()
    {
        paintFlow_compute.SetVector("worldPos", transform.position);
        paintFlow_compute.SetVector("worldScale", transform.localScale);
        paintFlow_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        paintFlow_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        paintFlow_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        paintFlow_compute.SetFloat("timeStep", Time.deltaTime);
        paintFlow_compute.SetFloat("sourceSize", flowpainterBrushSize);
        paintFlow_compute.SetVector("sourcePosition", flowpainterSourcePosition);
        paintFlow_compute.SetVector("sourceVelocity", flowpainterSourceVelocity);
        if (flowpainterSourceVelocity.magnitude > 0 || erasing)
            paintFlow_compute.SetBool("painting", true);
        else
            paintFlow_compute.SetBool("painting", false);
        paintFlow_compute.SetBuffer(paintFlow_kernel, "flowBuffer", baseFlowBuffer);
        paintFlow_compute.Dispatch(paintFlow_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);

    }

    void GenerateFlowMap()
    {
        evaluateVelocitySources_compute.SetVector("worldPos", transform.position);
        evaluateVelocitySources_compute.SetVector("worldScale", transform.localScale);
        evaluateVelocitySources_compute.SetInt("velocityBoxSizeX", velocityBoxSize.x);
        evaluateVelocitySources_compute.SetInt("velocityBoxSizeY", velocityBoxSize.y);
        evaluateVelocitySources_compute.SetInt("velocityBoxSizeZ", velocityBoxSize.z);
        evaluateVelocitySources_compute.SetBuffer(evaluateVelocitySources_kernel, "realtimeFlowMapBuffer", userFlowBuffer);
        evaluateVelocitySources_compute.SetBuffer(evaluateVelocitySources_kernel, "velocitySourcesBuffer", velocitySourcesBuffer);
        evaluateVelocitySources_compute.Dispatch(evaluateVelocitySources_kernel, maxNumberOfFluidSources / 1024, 1, 1);
    }

    public void UpdateVelocitySourcesBuffer()
    {
        velocitySourcesBuffer.SetData(oscReceiver.velocitySourceArray);
    }

    public void ResetRealtimeFlowMapBuffer()
    {
        
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBufferPrev", userFlowBuffer);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBuffer", userFlowBufferPrev);
        resetRealtimeFlowMap_compute.SetFloat("deltaTime", Time.deltaTime);
        resetRealtimeFlowMap_compute.SetFloat("diffusionRate", 0.9f);
        resetRealtimeFlowMap_compute.Dispatch(resetRealtimeFlowMap_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBuffer", userFlowBuffer);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBufferPrev", userFlowBufferPrev);
        resetRealtimeFlowMap_compute.Dispatch(resetRealtimeFlowMap_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
        
    }

    public Vector3[] GetFlowMap()
    {

        Vector3[] flowMap = new Vector3[velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z];
        baseFlowBuffer.GetData(flowMap);

        return flowMap;
    }

    public void SetFlowMap(Vector3[] flowMap, int mapSizeX, int mapSizeY, int mapSizeZ)
    {
        velocityBoxSize.x = mapSizeX;
        velocityBoxSize.y = mapSizeY;
        velocityBoxSize.z = mapSizeZ;

        baseFlowBuffer.SetData(flowMap);
    }

    /*********************************************/
    /*********************************************/
    /**************** UPDATE *********************/
    /*********************************************/
    /*********************************************/

    private void LateUpdate()
    {
        GenerateFlowMap();
        
        MoveParticles();

        PaintFlow();

        if (drawVelocityVectors)
            DrawVelocityVectors();
    }

    // render the materials
    void OnRenderObject()
    {
        // procedurally draw all the particles as a set of points with the particle material
        particleMaterial.SetPass(0);
        particleMaterial.SetColor("_Tint", globalTint);
        particleMaterial.SetBuffer("trailParticles", trailParticleBuffer);
        particleMaterial.SetTexture("_Sprite", particleSprite);
        particleMaterial.SetVector("_Size", particleSize);
        particleMaterial.SetVector("_worldPos", transform.position);
        particleMaterial.SetVector("_localScale", transform.localScale);

        Graphics.DrawProcedural(MeshTopology.Lines, trailParticleBuffer.count);

        // draw the velocity vectors
        if (drawVelocityVectors)
        {
            vectorMaterial.SetPass(0);
            vectorMaterial.SetFloat("_PaintBrushSize", flowpainterBrushSize);
            vectorMaterial.SetFloat("_PaintSourceDistance", flowpainterBrushDistance);
            vectorMaterial.SetBuffer("buf_Points", meshPointsBuffer);
            vectorMaterial.SetVector("_worldPos", transform.position);
            vectorMaterial.SetVector("_localScale", transform.localScale);

            Graphics.DrawProcedural(MeshTopology.Lines, meshPointsBuffer.count);
        }
    }

    // when this GameObject is disabled, release the buffers and materials
    private void OnDisable()
    {
        baseFlowBuffer.Release();
        userFlowBuffer.Release();
        userFlowBufferPrev.Release();

        trailHeadBuffer.Release();
        trailParticleBuffer.Release();
        trailParticleBufferPrev.Release();

        meshPointsBuffer.Release();
        
        velocitySourcesBuffer.Release();

        DestroyImmediate(particleMaterial);
        DestroyImmediate(vectorMaterial);
    }


    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5F);
        Vector3 cube = new Vector3(velocityBoxSize.x * transform.localScale.x, velocityBoxSize.y * transform.localScale.y, velocityBoxSize.z * transform.localScale.z);
        Gizmos.DrawWireCube(transform.position + cube / 2, cube);
    }
}
