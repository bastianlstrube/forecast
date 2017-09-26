using System.Collections;
using System.Collections.Generic;
//using System;
using UnityEngine;

public struct Centroid
{
    public Vector3 position;
    public Vector3 velocity;
    public float lifespan;
}

public struct FluidSource
{
    public Vector3 position;
    public Vector3 velocity;
}

public class OSCReceiver : MonoBehaviour
{
    public ParticleSimulation particleSimulation;

    [HideInInspector]
    public Centroid[] centroids;
    
    public string RemoteIP = "127.0.0.1"; //127.0.0.1 signifies a local host 
    [HideInInspector]
    public int SendToPort = 9109; //the port you will be sending from
    public int ListenerPort = 9109; //the port you will be listening on

    private Osc handler;
    private UDPPacketIO udp;

    private Dictionary<int, GameObject> centroidList;
    private List<FluidSource> velocitySourceList;

    private int sourceCount = 0;
    private const int maxSourceCount = 1024;

    [HideInInspector]
    public FluidSource[] velocitySourceArray;

    private Vector3 thisTransform;
    private Vector3 thisScale;
    private Quaternion thisRotation;

    // Use this for initialization
    void Awake()
    {
        udp = new UDPPacketIO();
        udp.init(RemoteIP, SendToPort, ListenerPort);
        handler = new Osc();
        handler.init(udp);
        handler.SetAllMessageHandler(AllMessageHandler);
        Debug.Log("OSC Connection initialized");
        centroids = new Centroid[50];
        centroidList = new Dictionary<int, GameObject>();
        velocitySourceList = new List<FluidSource>();

        for (int i = 0; i < 50; i++)
        {
            centroids[i].position = Vector3.zero;
            centroids[i].velocity = Vector3.zero;
            centroids[i].lifespan = 0.0f;
        }

        velocitySourceArray = new FluidSource[maxSourceCount];
        for (int i = 0; i < maxSourceCount; i++)
        {
            velocitySourceArray[i].position = Vector3.zero;
            velocitySourceArray[i].velocity = Vector3.zero;
        }
        thisTransform = transform.position;
        thisScale = transform.localScale;
        thisRotation = transform.rotation;
    }

    void OnDisable()
    {
        udp.Close();
    }

    public void AllMessageHandler(OscMessage oscMessage)
    {
        string msgString = Osc.OscMessageToString(oscMessage); //the message and value combined
        //string msgAddress = oscMessage.Address; //the message address
        
        string[] msgComponents = msgString.Split(' ');
        int id = int.Parse(msgComponents[1]);
        float lifespan = float.Parse(msgComponents[2]);

        Vector3 centroidPositionCurrent = new Vector3(float.Parse(msgComponents[3]), float.Parse(msgComponents[4]), 0);
        Vector3 centroidPositionPrevious = new Vector3(float.Parse(msgComponents[5]), float.Parse(msgComponents[6]), 0);

        int numContourPoints = int.Parse(msgComponents[msgComponents.Length - 1]);

        FluidSource thisSource = new FluidSource();
        thisSource.position = centroidPositionCurrent;
        thisSource.velocity = centroidPositionCurrent - centroidPositionPrevious;

        velocitySourceList.Add(thisSource);

        for (int i = 7; i < numContourPoints; i++)
        {
            thisSource.position = new Vector3(float.Parse(msgComponents[i++]), float.Parse(msgComponents[i++]), 0);
            thisSource.velocity = thisSource.position - new Vector3(float.Parse(msgComponents[i++]), float.Parse(msgComponents[i]), 0);
            velocitySourceList.Add(thisSource);

            velocitySourceArray[sourceCount].position = thisTransform + thisRotation * new Vector3(thisSource.position.x / 640.0f * thisScale.x, thisSource.position.y / 480.0f * thisScale.y, thisSource.position.z * thisScale.z);
            //velocitySourceArray[sourceCount].position = thisSource.position;
            velocitySourceArray[sourceCount].velocity = thisSource.velocity;
            //*0.1f;

            sourceCount++;

            //Vector3 velocitySourceCurrent = new Vector3(float.Parse(msgComponents[i]), float.Parse(msgComponents[i++]), 0);
            //Vector3 velocitySourcePrevious = new Vector3(float.Parse(msgComponents[i++]), float.Parse(msgComponents[i++]), 0);
        }

        centroids[id].position = centroidPositionCurrent;
        centroids[id].velocity = centroidPositionCurrent - centroidPositionPrevious;
        centroids[id].lifespan = lifespan;
    }


    void Connect()
    {
        udp.init(RemoteIP, SendToPort, ListenerPort);
        handler.init(udp);
        handler.SetAllMessageHandler(AllMessageHandler);
        Debug.Log("OSC Connection initialized");
    }

    void Update()
    {
        
        for (int i = 0; i < 50; i++)
        {
            if (centroids[i].lifespan > 1)
            {
                if (!centroidList.ContainsKey(i))
                {
                    GameObject gameObject = new GameObject();// GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    //gameObject.GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", Color.cyan * 2.0f);
                    //gameObject.transform.localScale = Vector3.one * 0.1f;
                    Light light = gameObject.AddComponent<Light>();
                    light.color = Color.cyan;
                    light.intensity = 3.0f;
                    light.range = 0.0f;
                    light.shadows = LightShadows.Soft;
                    
                    //light.intensity = fluidSources[i].lifespan;
                    centroidList.Add(i, gameObject);
                }
                centroidList[i].SetActive(true);

                if (centroidList[i].GetComponent<Light>().range < centroids[i].lifespan*0.3f)
                {
                    centroidList[i].GetComponent<Light>().range += 0.05f;
                } else
                {
                    centroidList[i].GetComponent<Light>().range = centroids[i].lifespan*0.3f;
                }
                centroidList[i].transform.position = transform.position + transform.rotation * new Vector3(centroids[i].position.x / 640.0f * transform.localScale.x, centroids[i].position.y / 480.0f * transform.localScale.y, centroids[i].position.z * transform.localScale.z);
            }
            else
            {
                if (centroidList.ContainsKey(i))
                {
                    centroidList[i].GetComponent<Light>().range = 0.0f;
                    centroidList[i].SetActive(false);
                }
            }
        }
        /*
        for (int i = 0; i < velocitySourceArray.Length; i++)
        {
            if (velocitySourceArray[i].position.sqrMagnitude > 0)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = velocitySourceArray[i].position;
                cube.transform.localScale = Vector3.one * 0.1f;
            }
        }
        */
        particleSimulation.ResetRealtimeFlowMapBuffer();
        particleSimulation.UpdateVelocitySourcesBuffer();
        
        for (int i = 0; i < maxSourceCount; i++)
        {
            velocitySourceArray[i].position = Vector3.zero;
            velocitySourceArray[i].velocity = Vector3.zero;
        }
        

        sourceCount = 0;
        velocitySourceList.Clear();

    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 1);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.one * 0.5f, Vector3.one);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawIcon(transform.position + transform.rotation * (new Vector3(transform.localScale.x, transform.localScale.y / 2f, 0)), "right.png");
        Gizmos.DrawIcon(transform.position + transform.rotation * (new Vector3(0, transform.localScale.y / 2f, 0)), "left.png");
        Gizmos.DrawIcon(transform.position + transform.rotation * (new Vector3(transform.localScale.x / 2f, transform.localScale.y, 0)), "bottom.png");
    }
}
