using System.Collections;
using System.Collections.Generic;
//using System;
using UnityEngine;

public struct FluidSource
{
    public int id;
    public Vector3 position;
    public Vector3 velocity;
    public float lifespan;
}

public class OSCReceiver : MonoBehaviour
{
    public FluidSource[] fluidSources;

    [HideInInspector]
    public string RemoteIP = "127.0.0.1"; //127.0.0.1 signifies a local host 
    [HideInInspector]
    public int SendToPort = 9109; //the port you will be sending from
    public int ListenerPort = 9109; //the port you will be listening on

    private Osc handler;
    private UDPPacketIO udp;

    private Dictionary<int, GameObject> lightList;

    // Use this for initialization
    void Awake()
    {
        udp = new UDPPacketIO();
        udp.init(RemoteIP, SendToPort, ListenerPort);
        handler = new Osc();
        handler.init(udp);
        handler.SetAllMessageHandler(AllMessageHandler);
        Debug.Log("OSC Connection initialized");
        fluidSources = new FluidSource[50];
        lightList = new Dictionary<int, GameObject>();

        for (int i = 0; i < 50; i++)
        {
            fluidSources[i].id = i;
            fluidSources[i].position = Vector3.zero;
            fluidSources[i].velocity = Vector3.zero;
            fluidSources[i].lifespan = 0.0f;
        }
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

        int numContours = int.Parse(msgComponents[msgComponents.Length - 1]);

        for (int i = 7; i < numContours; i++)
        {
            Vector3 velocitySourceEnd = new Vector3(float.Parse(msgComponents[i]), float.Parse(msgComponents[i++]), 0);
            Vector3 velocitySourceStart = new Vector3(float.Parse(msgComponents[i++]), float.Parse(msgComponents[i++]), 0);
        }

        fluidSources[id].id = id;
        fluidSources[id].position = centroidPositionCurrent;
        fluidSources[id].velocity = centroidPositionCurrent - centroidPositionPrevious;
        fluidSources[id].lifespan = lifespan;
    }

    void Update()
    {
        for (int i = 0; i < 50; i++)
        {
            if(fluidSources[i].lifespan > 1)
            {
                if(!lightList.ContainsKey(i))
                {
                    GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    gameObject.GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", Color.cyan * 2.0f);
                    gameObject.transform.localScale = Vector3.one * 0.1f;
                    Light light = gameObject.AddComponent<Light>();
                    light.color = Color.cyan;
                    light.range = 20.0f;
                    lightList.Add(i, gameObject);
                }
                lightList[i].SetActive(true);
                lightList[i].transform.position = transform.position + transform.rotation * new Vector3(fluidSources[i].position.x/640.0f * transform.localScale.x, fluidSources[i].position.y/480.0f * transform.localScale.y, fluidSources[i].position.z * transform.localScale.z);
            } else {
                if (lightList.ContainsKey(i))
                    lightList[i].SetActive(false);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 1);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.one * 0.5f, Vector3.one);
    }
}
