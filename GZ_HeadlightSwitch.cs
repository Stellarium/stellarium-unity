using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Very simple class, just toggle Light in same GameObject via Ctrl-L
[RequireComponent(requiredComponent: typeof(Light))]
public class GZ_HeadlightSwitch : MonoBehaviour {

	// Use this for initialization
	void Start () {}
	
	// Update is called once per frame
	void Update () {}

    private void LateUpdate()
    {
        // any Control, but not combined with Alt/Shift 
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
        {
            if (Input.GetKeyUp(KeyCode.L))
            {
                gameObject.GetComponent<Light>().enabled = !gameObject.GetComponent<Light>().enabled;
            }
        }
    }
}
