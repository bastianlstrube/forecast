using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSimulation : MonoBehaviour {

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

    // This struct basically exists just to collapse the lifespan properties in the inspector
    // TODO: replace this with a custom editor
    [System.Serializable]
    public struct LifeSpanStruct
    {
        public float min;
        public float max;

        // Constructor
        public LifeSpanStruct(float _min, float _max)
        {
            min = _min;
            max = _max;
        }
    }

    // Data representing a single particle, just so I can initialise on the CPU before passing to the GPU
    public struct Particle
    {
        public Vector3 position;        //
        public Vector3 direction;        //
        public Vector4 color;           //
        public float timeElapsed;       // How long this particle has lived
        public float lifeSpan;          //
        public Vector3 spawnPosition;   // Initial spawn position never changes
        public float thisDrag;
        public float velocity;

        // Constructor
        public Particle(Vector3 _position, Vector3 _direction, Vector4 _color, float _timeElapsed, float _lifeSpan, Vector3 _spawnPosition, float _thisDrag, float _velocity)
        {
            position = _position;
            direction = _direction;
            color = _color;
            timeElapsed = _timeElapsed;
            lifeSpan = _lifeSpan;
            spawnPosition = _spawnPosition;
            thisDrag = _thisDrag;
            velocity = _velocity;
        }
    }

    [Header("Compute Shaders")]
    // Push the particles around
    public ComputeShader advectParticlesComputeShader;

    public ComputeShader flowPainterComputeShader;

    // Used for debugging the fluid vector field
    public ComputeShader vectorMeshComputeShader;

    [Header("Surface Shaders")]
    public Shader particleSurfaceShader;    // billboard particle shader
    public Shader vectorSurfaceShader;

    [Space(10.0f)]
    [Header("Flow Attributes")]
    public IntVector3 velocityBoxSize = new IntVector3(64, 64, 64);   // How large the vector field is for computing the fluid movement
    public bool drawVelocityVectors = false;
    public bool useRandomStartVelocities = false;

    [Space(10.0f)]
    [Header("Particle System Attributes")]
    public int numParticles = 1048575;
    [Range(0, 1)]
    public float drag = 0.9f;   // drag on the particles, not the fluid viscosity

    [Space(10.0f)]
    [Header("Particle Properties")]
    public Texture2D particleSprite;
    public Vector2 particleSize = new Vector2(0.02f, 0.02f);
    public LifeSpanStruct particleLifeSpan = new LifeSpanStruct(1f, 3f);
    public float sizeByVelocity = 200.0f;
    public Color globalTint = Color.white;
    public bool useVelocityAlpha = false;

    [Space(10.0f)]
    [Header("Color Fractals")]
    public Vector2 fold = new Vector2(0.5f, -0.5f);
    public Vector2 translate = new Vector2(1.5f, 1.5f);
    public float scale = 1.3f;

    // Buffers for storing data in the compute buffer
    private ComputeBuffer flowMapBuffer;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer meshPointsBuffer;

    // kernels
    private int advectParticleKernel;
    private int flowPainterKernel;
    private int meshComputeKernel;

    // materials
    private Material vectorMaterial;  // vector material created from the vector shader
    private Material particleMaterial;  // particle material created from the particle shader

    // Other variables
    private int boxVolume;          // volume of the box, calculated at runtime from side length

    [HideInInspector]
    public Vector3 flowpainterSourcePosition = Vector3.zero;
    [HideInInspector]
    public float flowpainterBrushDistance = 0.0f;
    [HideInInspector]
    public Vector3 flowpainterSourceVelocity = Vector3.zero;
    [HideInInspector]
    public float flowpainterBrushSize = 1f;

    void Start()
    {

        // calculate box volume
        boxVolume = velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z;

        // initialise materials
        vectorMaterial = new Material(vectorSurfaceShader);
        particleMaterial = new Material(particleSurfaceShader);

        // find the compute shader's "main" function and store it
        advectParticleKernel = advectParticlesComputeShader.FindKernel("AdvectParticles");
        meshComputeKernel = vectorMeshComputeShader.FindKernel("CreateVectorMesh");
        flowPainterKernel = flowPainterComputeShader.FindKernel("PaintFlow");

        // create 1 dimensional buffer of float3's with a length of the box volume
        // this stores the particles' positions and colours
        flowMapBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        particleBuffer = new ComputeBuffer(numParticles, (sizeof(float) * 3) * 3 + (sizeof(float) * 4) + (sizeof(float)) * 4);
        meshPointsBuffer = new ComputeBuffer(boxVolume * 2, sizeof(float) * 3 + sizeof(float) * 4);

        InitialiseVectorMap();
        InitialiseParticles();
    }

    public void InitialiseVectorMap()
    {
        Vector3[] flowMap = new Vector3[boxVolume];
        for (int i = 0; i < boxVolume; i++)
        {
            if (useRandomStartVelocities)
            {
                //int xPosition = i % velocityBoxSize.z;
                //int yPosition = (i / velocityBoxSize.z) % velocityBoxSize.y;
                //int zPosition = i / (velocityBoxSize.y * velocityBoxSize.z);

                //float xVelocity, yVelocity, zVelocity = 0;

                //xVelocity = Mathf.Sin(xPosition / 5f) * Mathf.Cos(xPosition / 25f);
                //yVelocity = Mathf.Cos(yPosition / 5f) * Mathf.Cos(zPosition / 10f);
                //zVelocity = 0f;

                //flowMap[i] = new Vector3(xVelocity * 0.05f, yVelocity * 0.05f, zVelocity * 0.05f);
                flowMap[i] = Vector3.zero;
                //flowMap[i] = new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.05f, 0.05f), Random.Range(-0.05f, 0.05f)).normalized;

                /*
                if (xPosition % 5 == 0)
                {

                    flowMap[i] = new Vector3((zPosition - velocityBoxSize.x / 2.0f) * 0.005f, (yPosition - velocityBoxSize.y / 2.0f) * 0.005f, (xPosition - velocityBoxSize.z / 2.0f) * 0.005f);
                }
                else
                {

                    flowMap[i] = new Vector3(Random.Range(0, (zPosition - velocityBoxSize.x / 2.0f) * 0.005f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
                }
                */
            }
            else
            {
                flowMap[i] = Vector3.zero;
            }
        }

        flowMapBuffer.SetData(flowMap);
    }

    void InitialiseParticles()
    {
        Particle[] particleMap = new Particle[numParticles];
        for (int i = 0; i < numParticles; i++)
        {
            Vector3 spawnPosition = new Vector3(Random.Range(0.0f, velocityBoxSize.x), Random.Range(0.0f, velocityBoxSize.y), Random.Range(0.0f, velocityBoxSize.z));
            Vector3 startDirection = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
            //startDirection = Vector3.zero;
            particleMap[i] = new Particle(spawnPosition, startDirection.normalized, Vector4.zero, 0, Random.Range(particleLifeSpan.min, particleLifeSpan.max), spawnPosition, 1.0f, 0f);
        }

        particleBuffer.SetData(particleMap);
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
        advectParticlesComputeShader.SetFloat("currentTime", Time.time);

        advectParticlesComputeShader.SetVector("fold", fold);
        advectParticlesComputeShader.SetVector("translate", translate);
        advectParticlesComputeShader.SetFloat("scale", scale);

        advectParticlesComputeShader.SetVector("velocityBoxSize", new Vector3(velocityBoxSize.x * transform.localScale.x, velocityBoxSize.y * transform.localScale.y, velocityBoxSize.z * transform.localScale.z));
        advectParticlesComputeShader.Dispatch(advectParticleKernel, numParticles / 512, 1, 1);

    }

    void DrawVelocityVectors()
    {
        vectorMeshComputeShader.SetBuffer(meshComputeKernel, "vectorMapBuffer", flowMapBuffer);
        vectorMeshComputeShader.SetBuffer(meshComputeKernel, "meshPointBuffer", meshPointsBuffer);
        vectorMeshComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        vectorMeshComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        vectorMeshComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        vectorMeshComputeShader.SetVector("brushPosition", flowpainterSourcePosition);
        vectorMeshComputeShader.SetFloat("brushSize", flowpainterBrushSize);
        vectorMeshComputeShader.Dispatch(meshComputeKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    void PaintFlow()
    {
        flowPainterComputeShader.SetVector("worldPos", transform.position);
        flowPainterComputeShader.SetVector("worldScale", transform.localScale);
        flowPainterComputeShader.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        flowPainterComputeShader.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        flowPainterComputeShader.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        flowPainterComputeShader.SetFloat("timeStep", Time.deltaTime);
        flowPainterComputeShader.SetFloat("sourceSize", flowpainterBrushSize);
        flowPainterComputeShader.SetVector("sourcePosition", flowpainterSourcePosition);
        flowPainterComputeShader.SetVector("sourceVelocity", flowpainterSourceVelocity);
        if (flowpainterSourceVelocity.magnitude > 0)
            flowPainterComputeShader.SetBool("painting", true);
        else
            flowPainterComputeShader.SetBool("painting", false);
        flowPainterComputeShader.SetBuffer(flowPainterKernel, "flowBuffer", flowMapBuffer);
        flowPainterComputeShader.Dispatch(flowPainterKernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);

    }

    private void Update()
    {
        // move the particles along the fluid
        AdvectParticles();

        PaintFlow();

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
        particleMaterial.SetVector("_localScale", transform.localScale);

        Graphics.DrawProcedural(MeshTopology.Points, particleBuffer.count);

        // draw the velocity vectors
        if (drawVelocityVectors)
        {
            vectorMaterial.SetPass(0);
            vectorMaterial.SetFloat("_PaintBrushSize", flowpainterBrushSize);
            vectorMaterial.SetFloat("_PaintSourceDistance", flowpainterBrushDistance);
            vectorMaterial.SetBuffer("buf_Points", meshPointsBuffer);

            Graphics.DrawProcedural(MeshTopology.Lines, meshPointsBuffer.count);
        }
    }

    // when this GameObject is disabled, release the buffers and materials
    private void OnDisable()
    {
        flowMapBuffer.Release();
        particleBuffer.Release();
        meshPointsBuffer.Release();

        DestroyImmediate(vectorMaterial);
        DestroyImmediate(particleMaterial);
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5F);
        Vector3 cube = new Vector3(velocityBoxSize.x * transform.localScale.x, velocityBoxSize.y * transform.localScale.y, velocityBoxSize.z * transform.localScale.z);
        Gizmos.DrawWireCube(transform.position + cube / 2, cube);

        
    }

}
