using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeOfDay : MonoBehaviour {
    public Light directionalLight;
    public float timeSpeed = 1f;

    public float timeOfDay;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        timeOfDay += timeSpeed;

        directionalLight.transform.rotation = Quaternion.Euler(timeOfDay, 13, 0);

    }
}
