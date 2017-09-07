using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeOfDay : MonoBehaviour {
    public Light directionalLight;
    public float timeSpeed = 1f;

    public float timeOfDay;

    private Quaternion originalRotation;

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
    }
}
