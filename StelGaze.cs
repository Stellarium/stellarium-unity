/*
    StelGaze.cs, part of the Stellarium Unity Bridge tools by Georg Zotti (LBI ArchPro) and John Fillwalk (IDIA Lab) 
    (c) 2017 Georg Zotti

    USAGE:
    Attach this script to your Stellarium Controller object. 
    The camera's orientation is transmitted to Stellarium via StelController, so that a linked SpoutReceiver panel will receive the current view.
   
    NOTE: 
    Stellarium does not like setting views into zenith or nadir. Limit your camera motion to -89..+89 degrees to prevent black sky. 
    2018-01: Updated to Unity 2017.3 and disable Update action for WebGL builds. 
 */
 using UnityEngine;
using System.Collections;

[RequireComponent(requiredComponent: typeof(StelController))]
public class StelGaze : MonoBehaviour {

    private StelController controller; // This finds the related script providing communication to Stellarium.
    private Vector3 gaze; // keep a record, send update message only if it changes.

    // Use this for initialization
    void Awake () {
        // just initialize the controller. That script must be attached to the same gameObject.
        controller = gameObject.GetComponent<StelController>();
        if (controller == null)
            Debug.LogError("StelGaze: Cannot find StelController!");
    }

    void Start() { }
    // Update is called once per frame
    // This sends the camera angle to Stellarium after applying a north angle correction.
    // We do LateUpdate because cam may be attached to some other gameobject.
    // No, LateUpdate breaks transfer. Solved differently, by disabling mouse component in FirstPersonController.
    
    void Update () {
#if !UNITY_WEBGL
        Vector3 p = Camera.main.transform.forward;
        if (controller && controller.connectToStellarium && controller.spoutMode &&  !p.Equals(gaze))
        {
            gaze = p;
            // X towards North, Y up, Z towards West.
            float az = Mathf.PI + Mathf.Atan2(p[2], p[0]) + controller.northAngle * Mathf.PI / 180.0f;
            float alt = Mathf.Asin(p[1]);
            // Staring right into the zenith causes still blackouts in Stellarium. This should help. TODO: Remove when possible.
            if (alt == Mathf.PI / 2.0f)
                alt -= 0.00001f;
            //Debug.Log("Camera gaze Vector: " + p[0] + "/" + p[1] + "/" + p[2]+"=> Az: "+az * 180.0f / Mathf.PI + " Alt: "+alt * 180.0f / Mathf.PI);

            StartCoroutine(controller.SetViewDirection(az, alt));
        }
#endif
    }
}
