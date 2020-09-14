/* StelKeyboardTriggers: A few examples of hotkey communication with Stellarium via RemoteControl API.
 * (c) 2017 Georg Zotti
 * Part of the Stellarium Unity Bridge tools by Georg Zotti (LBI ArchPro Vienna) and John Fillwalk (IDIA Lab). 
 * 
 * GZ 2016-12-07: Press F11 to send message to running Stellarium instance to build new Skybox textures. 
   GZ 2016-12-08: Use a coroutine to be able to receive the HTTP answers.
                  Use JSON for more complete communication.
   GZ 2017-01-28: Separated StelController (JSON communication, depends on Stellarium API) from this TriggerScript (usage examples). 

   USAGE: Attach this to the GameObject with the StelController. Enables hotkey communication. See list in implementation below for keycodes. 
*/
using UnityEngine;
using System.Collections;

public class StelKeyboardTriggers : MonoBehaviour
{
    private StelController controller;
    private StreamingSkybox streamingSkybox;

    void Awake()
    {
        // just initialize the controller...
        controller = gameObject.GetComponent<StelController>();
        if (controller == null)
            Debug.LogWarning("StelKeyboardTriggers: Cannot find StelController! controller not initialized");
        streamingSkybox = gameObject.GetComponent<StreamingSkybox>();
        if (streamingSkybox == null)
            Debug.LogWarning("StelKeyboardTriggers: Cannot find StreamingSkybox! streamingSkybox not initialized");
    }

    void LateUpdate()
    {
        if (!controller)
        {
            Debug.LogWarning("StellariumTriggers: No controller found!");
            return;
        }

        // Any Control, but no Shift or Alt
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
        {
            if (Input.GetKeyUp(KeyCode.KeypadPlus))
            {
                float intensity = gameObject.GetComponent<Light>().intensity + 0.1f;
                gameObject.GetComponent<Light>().intensity = Mathf.Min(8.0f, intensity); // allow considerable overexposure for Venus-related things.
            }
            if (Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                float intensity = gameObject.GetComponent<Light>().intensity - 0.1f;
                gameObject.GetComponent<Light>().intensity = Mathf.Max(0.0f, intensity);
            }
        }

        // NO shifts at all...
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
        {
            // Preliminary until GUI can show JSON list of skies.
            if (Input.GetKeyUp(KeyCode.Alpha1)) streamingSkybox.SkyName = "1";
            if (Input.GetKeyUp(KeyCode.Alpha2)) streamingSkybox.SkyName = "2";
            if (Input.GetKeyUp(KeyCode.Alpha3)) streamingSkybox.SkyName = "3";
            if (Input.GetKeyUp(KeyCode.Alpha4)) streamingSkybox.SkyName = "4";
            if (Input.GetKeyUp(KeyCode.Alpha5)) streamingSkybox.SkyName = "5";
            if (Input.GetKeyUp(KeyCode.Alpha6)) streamingSkybox.SkyName = "6";
            if (Input.GetKeyUp(KeyCode.Alpha7)) streamingSkybox.SkyName = "7";
            if (Input.GetKeyUp(KeyCode.Alpha8)) streamingSkybox.SkyName = "8";
            if (Input.GetKeyUp(KeyCode.Alpha9)) streamingSkybox.SkyName = "9";
            //if (Input.GetKeyUp(KeyCode.Alpha0)) streamingSkybox.SkyName = "0"; // TBD: Can we really feed live skyboxes to a WebGL game? Seems possible.
            if (Input.GetKeyUp(KeyCode.Alpha0)) streamingSkybox.SkyName = "live";
#if !UNITY_WEBGL
            // Webgl builds will always have spout=0. Also, all F keys seem to go to the browser!
            if (Input.GetKeyUp(KeyCode.F12)) controller.spoutMode = !controller.spoutMode;
            if (Input.GetKeyUp(KeyCode.F11)) StartCoroutine(controller.UpdateSkyboxTiles());
            if (Input.GetKeyUp(KeyCode.F8)) StartCoroutine(controller.TimeMinus1h());
            if (Input.GetKeyUp(KeyCode.F9)) StartCoroutine(controller.TimePlus1h());
            // The following are a few examples which behave exactly like the respective keys in Stellarium. 
            // The action names are available in Stellarium's sources or via RemoteControl query.
            if (Input.GetKeyUp(KeyCode.T)) StartCoroutine(controller.DoAction("actionShow_Atmosphere"));           // counter-example: "A" is taken by the FirstPersonController.
            if (Input.GetKeyUp(KeyCode.G)) StartCoroutine(controller.DoAction("actionShow_Ground"));
            if (Input.GetKeyUp(KeyCode.Q)) StartCoroutine(controller.DoAction("actionShow_Cardinal_Points"));
            if (Input.GetKeyUp(KeyCode.J)) StartCoroutine(controller.DoAction("actionDecrease_Time_Speed"));
            if (Input.GetKeyUp(KeyCode.K)) StartCoroutine(controller.DoAction("actionSet_Real_Time_Speed"));
            if (Input.GetKeyUp(KeyCode.L)) StartCoroutine(controller.DoAction("actionIncrease_Time_Speed"));
            if (Input.GetKeyUp(KeyCode.Keypad8)) StartCoroutine(controller.DoAction("actionReturn_To_Current_Time"));
            if (Input.GetKeyUp(KeyCode.E)) StartCoroutine(controller.DoAction("actionShow_Equatorial_Grid"));
            if (Input.GetKeyUp(KeyCode.Z)) StartCoroutine(controller.DoAction("actionShow_Azimuthal_Grid"));
            if (Input.GetKeyUp(KeyCode.H)) StartCoroutine(controller.DoAction("actionShow_Horizon_Line"));
            if (Input.GetKeyUp(KeyCode.Comma)) StartCoroutine(controller.DoAction("actionShow_Ecliptic_Line"));
            if (Input.GetKeyUp(KeyCode.M)) StartCoroutine(controller.DoAction("actionShow_Meridian_Line"));
            if (Input.GetKeyUp(KeyCode.Period)) StartCoroutine(controller.DoAction("actionShow_Equator_Line"));
            // More actions. These are from the ArchaeoLines plugin, and are usually not available as hotkeys (but can be configured in Stellarium)
            // This will emit a "not found" warning when the plugin is not loaded.
            if (Input.GetKeyUp(KeyCode.U)) StartCoroutine(controller.DoAction("actionShow_ArchaeoLines"));
            // Constellations
            if (Input.GetKeyUp(KeyCode.C)) StartCoroutine(controller.DoAction("actionShow_Constellation_Lines"));
            if (Input.GetKeyUp(KeyCode.V)) StartCoroutine(controller.DoAction("actionShow_Constellation_Labels"));
            if (Input.GetKeyUp(KeyCode.R)) StartCoroutine(controller.DoAction("actionShow_Constellation_Art"));
            if (Input.GetKeyUp(KeyCode.B)) StartCoroutine(controller.DoAction("actionShow_Constellation_Boundaries"));

            if (Input.GetKeyUp(KeyCode.Keypad4)) // toggle scaling of the Moon. This is a property, cached already in the controller's json.
            {
                if (controller.JsonIsValid())
                {
                    bool moonScaled;
                    try
                    {
                        moonScaled = bool.Parse(controller.GetPropertyValue("SolarSystem.flagMoonScale"));
                        moonScaled = !moonScaled;
                    }
                    catch (System.Exception)
                    {
                        moonScaled = false;
                    }
                    StartCoroutine(controller.SetProperty("SolarSystem.flagMoonScale", moonScaled.ToString()));
                }
            }
#endif
        }
    }
}
