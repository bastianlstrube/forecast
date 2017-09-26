using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;

public class TimeOfDay : MonoBehaviour {
    private FMODUnity.StudioEventEmitter eventEmitterRef;

    public Light directionalLight;
    public float timeSpeed = 1f;

    public float timeOfDay;

    private Quaternion originalRotation;

    void Awake()

    {
        //VOICE EMITTER IS CONSTANTLY PLAYING
        eventEmitterRef = GetComponent<FMODUnity.StudioEventEmitter>();
        GetComponent<FMODUnity.StudioEventEmitter>().Play();
    }


    // Use this for initialization
    void Start () {
        originalRotation = directionalLight.transform.rotation;

    }
	
	// Update is called once per frame
	void Update () {
        timeOfDay += timeSpeed;

        directionalLight.transform.rotation = Quaternion.Euler(timeOfDay, originalRotation.eulerAngles.y, originalRotation.eulerAngles.z);

        if (directionalLight.transform.rotation.eulerAngles.x > 0.0f && directionalLight.transform.rotation.eulerAngles.x < 180.0f)
        {
            if (RenderSettings.ambientIntensity < 1.0f)
                RenderSettings.ambientIntensity += 0.02f;
        } else
        {
            if (RenderSettings.ambientIntensity > 0.5f)
                RenderSettings.ambientIntensity -= 0.02f;
        }

        

        /*
        float TriggerR = Input.GetAxis("TriggerR");   
        eventEmitterRef.SetParameter("TriggerR", TriggerR);

        float TriggerL = Input.GetAxis("TriggerL");
        eventEmitterRef.SetParameter("TriggerL",TriggerL);

        //ANALOG UP
        float AnalogUp = Input.GetAxis("Right Stick Up");
        eventEmitterRef.SetParameter("VolumeUp", AnalogUp);

        //ANALOG SIDE
        float AnalogSide = Input.GetAxis("Right Stick Side");
        eventEmitterRef.SetParameter("VolumeSide", AnalogSide);
        */
    }
}
