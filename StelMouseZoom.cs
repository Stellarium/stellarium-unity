// GZMouseZoom, a simple mouse zoom script. 
// (c) 2012-16 Georg Zotti
// GZ 2012-11-10: Modify camera field of view via mouse wheel
// GZ 2016-12-07: Ported to C#
// GZ 2016-12-26: Communicates to StelController.cs to transport FoV.
// GZ 2017-02-04: Rename to StelMouseZoom
// GZ 2018-01-24: allow proportional scaling.
// GZ 2020-04-07: invert direction (harmonize with Stellarium...)

// INSTRUCTIONS: Attach this script to the Stellarium GameObject. It will find your main Camera (e.g. of FPSController/FirstPersonCharacter.)

using UnityEngine;
using System.Collections;

[RequireComponent(requiredComponent: typeof(StelController))]
public class StelMouseZoom : MonoBehaviour {

    public float minFieldOfView = 2.0f;
    public float maxFieldOfView = 120.0f;
    public bool linearScaling=true; // zoom in/out by step degrees
    public float stepOrFactor=1; // either n degrees per mouse rotation impulse or a scaling factor like 0.05 for 5 percent change.

    private float lastFoV=200; // keep track of this to avoid too much traffic.
    private StelController controller; // This finds the related script providing communication to Stellarium.

    void Awake()
    {
        // just initialize the controller. That script must be attached to the same gameObject.
        controller = gameObject.GetComponent<StelController>();
        if (controller==null)
            Debug.LogWarning("StelMouseZoom: Cannot find StelController!");
    }

    void Start()
    {
#if false
        if (controller && controller.spoutMode)
        {
            if (lastFoV != Camera.main.fieldOfView)
            {
                lastFoV = Camera.main.fieldOfView;
                StartCoroutine(controller.SetFoV(lastFoV));
                //Debug.Log("StelMouseZoom: NEW FOV SET: " + lastFoV);
            }
            //else
            //    Debug.Log("NEW FOV not required to SET.");
        }
#endif
    }

    void Update () {
    if (Input.GetAxis("Mouse ScrollWheel") > 0) // forward
        {
            if (linearScaling)
            {
                Camera.main.fieldOfView = Mathf.Max(Camera.main.fieldOfView - stepOrFactor, minFieldOfView);
            }
            else
            {
                Camera.main.fieldOfView = Mathf.Max(Camera.main.fieldOfView *(1.0f-stepOrFactor), minFieldOfView);
            }

        }
        if (Input.GetAxis("Mouse ScrollWheel") < 0) // backward
        {
            if (linearScaling)
            {
                Camera.main.fieldOfView = Mathf.Min(Camera.main.fieldOfView + stepOrFactor, maxFieldOfView);
            }
            else
            {
                Camera.main.fieldOfView = Mathf.Min(Camera.main.fieldOfView *(1.0f+stepOrFactor), maxFieldOfView);
            }

        }

        if (controller && controller.connectToStellarium && controller.spoutMode)
        {
            if (lastFoV != Camera.main.fieldOfView)
            {
                lastFoV = Camera.main.fieldOfView;
                StartCoroutine(controller.SetFoV(lastFoV));
                //Debug.Log("StelMouseZoom: NEW FOV SET: "+lastFoV);
            }
            //else
            //    Debug.Log("NEW FOV not required to SET.");
        }
    }
}