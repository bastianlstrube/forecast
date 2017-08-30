using System.Collections;
using System.Collections.Generic;
//using System;
using UnityEngine;

public struct FluidSource
{
    public Vector3 position;
    public Vector3 velocity;
}

public class OSCReceiver : MonoBehaviour
{
    [HideInInspector]
    public string RemoteIP = "127.0.0.1"; //127.0.0.1 signifies a local host 
    [HideInInspector]
    public int SendToPort = 9109; //the port you will be sending from
    public int ListenerPort = 9109; //the port you will be listening on

    private Osc handler;
    private UDPPacketIO udp;

    [HideInInspector]
    public FluidSource[] sourceMap;
    [HideInInspector]
    public bool hasVelocity;

    public const int maxNumSources = 30 * 20;

    private FluidSource[] resetMap;

    // Use this for initialization
    void Awake()
    {
        udp = new UDPPacketIO();
        udp.init(RemoteIP, SendToPort, ListenerPort);
        handler = new Osc();
        handler.init(udp);
        handler.SetAllMessageHandler(AllMessageHandler);
        Debug.Log("OSC Connection initialized");

        hasVelocity = false;
        sourceMap = new FluidSource[maxNumSources];
        resetMap = new FluidSource[maxNumSources];

        for (int i = 0; i < maxNumSources; i++)
        {
            FluidSource thisSource = new FluidSource();
            thisSource.position = Vector3.zero;
            thisSource.velocity = Vector3.zero;
            sourceMap[i] = thisSource;
            resetMap[i] = thisSource;
        }
    }

    public void ResetSourceMap()
    {
        for (int i = 0; i < maxNumSources; i++)
        {
            sourceMap[i].position = Vector3.zero;
            sourceMap[i].velocity = Vector3.zero;
        }

        hasVelocity = false;
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
        int index = int.Parse(msgComponents[1]);
        
        if (index >= 0 && index < maxNumSources)
        {
            float xPos = float.Parse(msgComponents[2]);
            float yPos = float.Parse(msgComponents[3]);
            float zPos = float.Parse(msgComponents[4]);
            sourceMap[index].position = new Vector3(xPos, yPos, zPos);
            sourceMap[index].velocity = new Vector3(float.Parse(msgComponents[5]) - xPos, yPos - float.Parse(msgComponents[6]), float.Parse(msgComponents[7]) - zPos);
            hasVelocity = true;
        }
    }
}
