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
        public float targetVelocity;
        public float baseScale;
        public Vector4 emissive;

        // Constructor
        public Particle(Vector3 _position, Vector3 _direction, Vector4 _color, float _timeElapsed, float _lifeSpan, Vector3 _spawnPosition, float _thisDrag, float _velocity, float _targetVelocity, float _baseScale, Vector4 _emissive)
        {
            position = _position;
            direction = _direction;
            color = _color;
            timeElapsed = _timeElapsed;
            lifeSpan = _lifeSpan;
            spawnPosition = _spawnPosition;
            thisDrag = _thisDrag;
            velocity = _velocity;
            targetVelocity = _targetVelocity;
            baseScale = _baseScale;
            emissive = _emissive;
        }
    }

    public struct Affector
    {
        public Vector3 position;
        public Vector3 velocity;

        public Affector(Vector3 _position, Vector3 _velocity)
        {
            position = _position;
            velocity = _velocity;
        }
    }

    [Header("Compute Shaders")]
    // Push the particles around
    public ComputeShader moveParticles_compute;
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
    public IntVector3 velocityBoxSize = new IntVector3(64, 64, 64);   // How large the vector field is for computing the fluid movement
    public bool drawVelocityVectors = false;
    public bool useRandomStartVelocities = false;

    [Space(10.0f)]
    [Header("Particle System Attributes")]
    public int numParticles = 1048575;
    [Range(0, 1)]
    public float drag = 0.9f;   // drag on the particles, not the fluid viscosity
    [Range(0, 1)]
    public float pulse = 1.0f;
    public float animatePulse = 0.01f;


    [Space(10.0f)]
    [Header("Particle Properties")]
    public Texture2D particleSprite;
    public Vector2 particleSize = new Vector2(0.02f, 0.02f);
    public LifeSpanStruct particleLifeSpan = new LifeSpanStruct(1f, 3f);
    public float sizeByVelocity = 200.0f;
    public Color globalTint = Color.white;
    public Gradient colourByZPosition;
    public bool useVelocityAlpha = false;
    public AnimationCurve spawnDensityZ = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve sizeByZPosition = AnimationCurve.Linear(0, 1, 1, 0);
    public float baseVelocity = 1.0f;

    [Space(10.0f)]
    [Header("Color Fractals")]
    public Vector2 fold = new Vector2(0.5f, -0.5f);
    public Vector2 translate = new Vector2(1.5f, 1.5f);
    public float scale = 1.3f;

    [Space(10.0f)]
    [Header("Velocity Affectors")]
    public Transform[] velocityAffectorsCurrent;
    private Affector[] velocityAffectorsPrev;

    // Buffers for storing data in the compute buffer
    private ComputeBuffer constantFlowBuffer;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer meshPointsBuffer;
    private ComputeBuffer realtimeFlowBuffer;
    private ComputeBuffer realtimeFlowBufferPrev;
    private ComputeBuffer velocitySourcesBuffer;

    // kernels
    private int moveParticles_kernel;
    private int paintFlow_kernel;
    private int generateVectorMesh_kernel;
    private int evaluateVelocitySources_kernel;
    private int resetRealtimeFlowMap_kernel;

    // materials
    private Material vectorMaterial;  // vector material created from the vector shader
    private Material particleMaterial;  // particle material created from the particle shader

    // Other variables
    private int boxVolume;          // volume of the box, calculated at runtime from side length
    private int numAffectors;

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

    bool forward = true;

    void Start()
    {
        

        // calculate box volume
        boxVolume = velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z;

        
        numAffectors = velocityAffectorsCurrent.Length;

        // initialise materials
        vectorMaterial = new Material(vectorSurfaceShader);
        particleMaterial = new Material(particleSurfaceShader);

        // find the compute shader's "main" function and store it
        moveParticles_kernel = moveParticles_compute.FindKernel("MoveParticles");
        generateVectorMesh_kernel = generateVectorMesh_compute.FindKernel("GenerateVectorMesh");
        paintFlow_kernel = paintFlow_compute.FindKernel("PaintFlow");
        evaluateVelocitySources_kernel = evaluateVelocitySources_compute.FindKernel("EvaluateVelocitySources");
        resetRealtimeFlowMap_kernel = resetRealtimeFlowMap_compute.FindKernel("ResetRealtimeFlowMap");

        // create 1 dimensional buffer of float3's with a length of the box volume
        // this stores the particles' positions and colours
        constantFlowBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        realtimeFlowBuffer = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        realtimeFlowBufferPrev = new ComputeBuffer(boxVolume, sizeof(float) * 3);
        if(numAffectors > 0)
            velocitySourcesBuffer = new ComputeBuffer(numAffectors, sizeof(float) * 6);
        particleBuffer = new ComputeBuffer(numParticles, sizeof(float) * 23);
        meshPointsBuffer = new ComputeBuffer(boxVolume * 2, sizeof(float) * 3 + sizeof(float) * 4);

        InitialiseConstantFlowBuffer();
        InitialiseParticles();
        InitialiseRealtimeFlowBuffer();
        if (numAffectors > 0)
            InitialiseVelocitySourcesBuffer();
    }

    public void InitialiseConstantFlowBuffer()
    {
        Vector3[] constantFlowMap = new Vector3[boxVolume];
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

                constantFlowMap[i] = Vector3.zero;
            }
            else
            {
                constantFlowMap[i] = Vector3.zero;
            }
        }

        constantFlowBuffer.SetData(constantFlowMap);
    }

    public void InitialiseRealtimeFlowBuffer()
    {
        Vector3[] realtimeFlowMap = new Vector3[boxVolume];
        for (int i = 0; i < boxVolume; i++)
        {
            realtimeFlowMap[i] = Vector3.zero;
        }
        realtimeFlowBuffer.SetData(realtimeFlowMap);
        realtimeFlowBufferPrev.SetData(realtimeFlowMap);
    }

    void InitialiseParticles()
    {
        Particle[] particleMap = new Particle[numParticles];

        Vector3 prevPos = Vector3.zero;


        for (int i = 0; i < numParticles; i++)
        {
            float zPosition = spawnDensityZ.Evaluate(Random.value) * velocityBoxSize.z;
            //float lifeSpan = particleLifeSpan.min + spawnDensityZ.Evaluate(Random.value) * (particleLifeSpan.max- particleLifeSpan.min);
            float lifeSpan = Random.Range(particleLifeSpan.min, particleLifeSpan.max);
            float size = sizeByZPosition.Evaluate(zPosition/velocityBoxSize.z);

            Vector3 spawnPosition;

            spawnPosition = new Vector3(Random.Range(0.0f, velocityBoxSize.x), Random.Range(0.0f, velocityBoxSize.y), zPosition);

            //spawnPosition = new Vector3(spawnPosition.x, Mathf.Sin(spawnPosition.y) * velocityBoxSize.y *0.5f + velocityBoxSize.y * 0.5f, spawnPosition.z);
            Vector3 startDirection = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
            //startDirection = Vector3.zero;
            particleMap[i] = new Particle(spawnPosition, startDirection.normalized, Vector4.zero, 0, lifeSpan, spawnPosition, 1.0f, 0f, baseVelocity, size, Vector4.zero);
        }

        particleBuffer.SetData(particleMap);
    }

    void InitialiseVelocitySourcesBuffer()
    {
        velocityAffectorsPrev = new Affector[numAffectors];
        for (int i = 0; i < numAffectors; i++)
        {
            velocityAffectorsPrev[i].position = velocityAffectorsCurrent[i].position;
            velocityAffectorsPrev[i].velocity = Vector3.zero;
        }
        velocitySourcesBuffer.SetData(velocityAffectorsPrev);
    }

    void MoveParticles()
    {
        moveParticles_compute.SetBuffer(moveParticles_kernel, "flowBuffer", constantFlowBuffer);
        moveParticles_compute.SetBuffer(moveParticles_kernel, "particleBuffer", particleBuffer);
        moveParticles_compute.SetBuffer(moveParticles_kernel, "realtimeFlowMapBuffer", realtimeFlowBuffer);
        moveParticles_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        moveParticles_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        moveParticles_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        moveParticles_compute.SetFloat("drag", drag);
        if (useVelocityAlpha)
            moveParticles_compute.SetInt("velocityAlpha", 0);
        else
            moveParticles_compute.SetInt("velocityAlpha", 1);
        moveParticles_compute.SetFloat("timeStep", Time.deltaTime);
        moveParticles_compute.SetFloat("currentTime", Time.time);

        moveParticles_compute.SetVector("fold", fold);
        moveParticles_compute.SetVector("translate", translate);
        moveParticles_compute.SetFloat("scale", scale);
        moveParticles_compute.SetFloat("pulse", pulse);

        moveParticles_compute.SetVector("velocityBoxSize", new Vector3(velocityBoxSize.x * transform.localScale.x, velocityBoxSize.y * transform.localScale.y, velocityBoxSize.z * transform.localScale.z));
        moveParticles_compute.Dispatch(moveParticles_kernel, numParticles / 512, 1, 1);

    }

    void DrawVelocityVectors()
    {
        generateVectorMesh_compute.SetVector("worldPos", transform.position);
        generateVectorMesh_compute.SetVector("worldScale", transform.localScale);
        generateVectorMesh_compute.SetBuffer(generateVectorMesh_kernel, "vectorMapBuffer", constantFlowBuffer);
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
        paintFlow_compute.SetBuffer(paintFlow_kernel, "flowBuffer", constantFlowBuffer);
        paintFlow_compute.Dispatch(paintFlow_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);

    }

    void GenerateFlowMap()
    {
        evaluateVelocitySources_compute.SetVector("worldPos", transform.position);
        evaluateVelocitySources_compute.SetVector("worldScale", transform.localScale);
        evaluateVelocitySources_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        evaluateVelocitySources_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        evaluateVelocitySources_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        evaluateVelocitySources_compute.SetBuffer(evaluateVelocitySources_kernel, "realtimeFlowMapBuffer", realtimeFlowBuffer);
        evaluateVelocitySources_compute.SetBuffer(evaluateVelocitySources_kernel, "velocitySourcesBuffer", velocitySourcesBuffer);
        evaluateVelocitySources_compute.Dispatch(evaluateVelocitySources_kernel, numAffectors / 5, 1, 1);
        // CHANGE DEPENDING ON NUMBER OF SOURCES! MAKE SURE TO CHANGE IN SHADER TOO!
    }

    void UpdateVelocitySourcesBuffer()
    {
        for (int i = 0; i < numAffectors; i++)
        {
            velocityAffectorsPrev[i].velocity = velocityAffectorsCurrent[i].position - velocityAffectorsPrev[i].position;
            velocityAffectorsPrev[i].position = velocityAffectorsCurrent[i].position;
        }
        velocitySourcesBuffer.SetData(velocityAffectorsPrev);
    }

    void ResetRealtimeFlowMapBuffer()
    {
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsX", velocityBoxSize.x / 8);
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsY", velocityBoxSize.y / 8);
        resetRealtimeFlowMap_compute.SetInt("numThreadGroupsZ", velocityBoxSize.z / 8);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBufferPrev", realtimeFlowBuffer);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBuffer", realtimeFlowBufferPrev);
        resetRealtimeFlowMap_compute.SetFloat("deltaTime", Time.deltaTime);
        resetRealtimeFlowMap_compute.SetFloat("diffusionRate", 0.9f);
        resetRealtimeFlowMap_compute.Dispatch(resetRealtimeFlowMap_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBuffer", realtimeFlowBuffer);
        resetRealtimeFlowMap_compute.SetBuffer(resetRealtimeFlowMap_kernel, "realtimeFlowMapBufferPrev", realtimeFlowBufferPrev);
        resetRealtimeFlowMap_compute.Dispatch(resetRealtimeFlowMap_kernel, velocityBoxSize.x / 8, velocityBoxSize.y / 8, velocityBoxSize.z / 8);
    }

    public Vector3[] GetFlowMap()
    {

        Vector3[] flowMap = new Vector3[velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z];
        constantFlowBuffer.GetData(flowMap);

        return flowMap;
    }

    public void SetFlowMap(Vector3[] flowMap, int mapSizeX, int mapSizeY, int mapSizeZ)
    {
        velocityBoxSize.x = mapSizeX;
        velocityBoxSize.y = mapSizeY;
        velocityBoxSize.z = mapSizeZ;

        constantFlowBuffer.SetData(flowMap);
    }

    /*********************************************/
    /*********************************************/
    /**************** UPDATE *********************/
    /*********************************************/
    /*********************************************/

    private void Update()
    {
        if (forward)
            pulse += animatePulse;
        else
            pulse -= animatePulse;

        if (pulse < -1.0)
            forward = true;
        else if (pulse > 0.8f)
            forward = false;

        if (numAffectors > -0.5f)
            ResetRealtimeFlowMapBuffer();

        if (numAffectors > 0)
            UpdateVelocitySourcesBuffer();

        if (numAffectors > 0)
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
            vectorMaterial.SetVector("_worldPos", transform.position);
            vectorMaterial.SetVector("_localScale", transform.localScale);

            Graphics.DrawProcedural(MeshTopology.Lines, meshPointsBuffer.count);
        }
    }

    // when this GameObject is disabled, release the buffers and materials
    private void OnDisable()
    {
        constantFlowBuffer.Release();
        particleBuffer.Release();
        meshPointsBuffer.Release();
        if (numAffectors > 0)
            velocitySourcesBuffer.Release();

        realtimeFlowBuffer.Release();
        realtimeFlowBufferPrev.Release();

        DestroyImmediate(vectorMaterial);
        DestroyImmediate(particleMaterial);
    }


    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5F);
        Vector3 cube = new Vector3(velocityBoxSize.x * transform.localScale.x, velocityBoxSize.y * transform.localScale.y, velocityBoxSize.z * transform.localScale.z);
        Gizmos.DrawWireCube(transform.position + cube / 2, cube);
    }
}
