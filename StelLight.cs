/* Interfacing Unity3D with Stellarium. Joint project (started 2015-12) of John Fillwalk (IDIA Lab) and Georg Zotti (LBI ArchPro).
   Authors of this collection of scripts: 
   Georg Zotti (LBI ArchPro)
   Neil Zehr (IDIA Lab)
   David Rodriguez (IDIA Lab)
   A Stellarium script (by G. Zotti) creates 6 tiles of a skybox plus data file "output.txt" to get information about light etc.
   This script auto-detects changes in the image directory, and reloads skybox and light information.
   Later versions of the system may be able to connect to Stellarium directly to periodically initiate re-creation of the images.
   INSTRUCTIONS:
   Create folder "Stellarium" in your Assets folder
   Put Stel*.cs there
   In Unity editor, Create New Gameobject: Light->Directional. Name it "StelLight" so it will be found automatically by other components of the Stellarium/Unity bridge modules.
   Add this script as property of a Game.
   Set the directories to those used by Stellarium (Usually, C:\Users\YOU\Pictures\Stellarium\ and C:\Users\YOU\AppData\Roaming\Stellarium)
   Assign Flares, e.g. from StandardAssets 50mm for Sun and Moon, Star for Venus
   Create a directional Light (we call it Shadowmaker) in the "scene root" with the same Flare and "Draw Halo". Assign this to the SunLightObj in the script. 
   This light will be fed with position data from Stellarium. If it does not exist, a default directional light (without Flare) will be created.
*/

using UnityEngine;
using System.Collections;

[RequireComponent(requiredComponent: typeof(StelController), requiredComponent2: typeof(Light))]
public class StelLight : MonoBehaviour {


    // Light variables                              // These are actually light type settings which are pushed forwarded into the linked stelLight
    public Flare sunFlare;                          // Set flare style for sunlight. E.g. Cold Clear Sun or Sun, or 50mmZoom. Avoid excessive size though.
    public Flare moonFlare;                         // Set flare style for moonlight. Should be less aggressive than SunFlare, but 50mmZoom also looks fine. 
    public Flare venusFlare;                        // Set flare style for Venus. Must be much less aggressive than SunFlare. E.g. FlareSmall 

    private Light stelLight;  // the light component of our gameObject
    private StelController controller;
    private JSONObject lightObjectInfo;
    private GameObject sunImpostorSphere; // used in Skybox mode to display a visible sphere when looking through instruments.

    private void Awake()
    {
        // just initialize the controller...
        controller = gameObject.GetComponent<StelController>();
        if (controller == null)
            Debug.LogWarning("StelLight: Cannot find StelController! controller not initialized");

        //Check to see if we have a sun linked to our public variable; if not, create one
        stelLight = gameObject.GetComponent<Light>();
        if (stelLight == null)
            stelLight = gameObject.AddComponent<Light>();

        stelLight.type = LightType.Directional;              // GZ: This was commented out. It should be set directional, however (set in inspector)
                                                             //stelLight.flare = sunFlare;
                                                             //stelLight.shadows = shadowType;

        sunImpostorSphere = transform.Find(name: "LightImpostorSphere").gameObject;
    }

    // Use this for initialization
    void Start () {
        SetAzAlt(135, 45);
        SetColor(1, 1, 1);
        SetLightsourcePropertiesByName("Sun");

#if UNITY_WEBGL
        InvokeRepeating(methodName: "UpdateLightPosition", time: 2.0f, repeatRate: 5.0f); // just one update. Runs safely after all initialisations.
#else
        InvokeRepeating(methodName: "UpdateLightPosition", time: 2.0f, repeatRate: 0.25f); // Update 10x per second should be OK. 2018: NO! 4/s must be enough.
#endif
    }

    //This is called repeatedly to update the shadow caster. 
    private void UpdateLightPosition()
    {
//#if !UNITY_WEBGL
        if (controller.spoutMode)
        {
            // GZ: We must not call this excessively, else remote control will be overloaded (fail with too many connections). 
            // 2018: This is now mitigated in StelController.
            StartCoroutine(controller.UpdateLightObjInfo());
        }
//#endif
    }


    // Update is called once per frame. 
    void Update() {
        // The next line may not find a valid object in every case. Hope for the best...
        JSONObject newLightObjectInfo = controller.GetLightObjInfo(); // deliver either the right object, or null if no light is active.
        //Debug.Log("StelLight::Update: light object now is " + newLightObjectInfo["name"].str.ToString());
        //if (lightObjectInfo != newLightObjectInfo)
        //{
            // We must always call this: flare may be switched off depending on FoV.
            UpdateLightObject(newLightObjectInfo);
        //}
//#if false
        // Move our position to be able to carry the LightImpostorSphere
        transform.position = Camera.main.transform.position;
        if (controller.connectToStellarium && controller.spoutMode)
            sunImpostorSphere.SetActive(false);
        else
        {
            sunImpostorSphere.SetActive(newLightObjectInfo["name"].str == "Sun");
            if (newLightObjectInfo["name"].str == "Sun")
            {
                // 250m distance should always keep the solar ball outside the game terrain.
                float size = Mathf.Tan(Mathf.PI / 180.0f * 0.5f * newLightObjectInfo["diameter"].f) * 2.0f * 250.0f;
                sunImpostorSphere.transform.localScale.Set(size, size, size);
            }
        }
//#endif
    }

    public void EnableSunImpostor(bool enable)
    {
        sunImpostorSphere.SetActive(enable);
    }
    private void UpdateLightObject(JSONObject newLightObjectInfo)
    {
        lightObjectInfo = newLightObjectInfo;

        if (!lightObjectInfo || lightObjectInfo.keys.Count==0)
        {
            // We should at least have received a "none" light source which provides ambient light data.
            Debug.LogWarning("StelLight: empty lightObject, creating dark ambient.");
            SetLightsourcePropertiesByName("none"); // switch it off.
            RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.15f);
            RenderSettings.fogColor = new Color(0.1f, 0.1f, 0.1f);
            return;
        }

        //Debug.Log("StelLight122: LightObjectInfo: " + lightObjectInfo.ToString());
        float ambientFactor = (float) lightObjectInfo["ambientInt"].n;
        RenderSettings.ambientLight = new Color(0.8f * Mathf.Min(ambientFactor, 0.3f), 0.9f * Mathf.Min(ambientFactor, 0.3f), 1.0f * Mathf.Min(ambientFactor, 0.3f));
        RenderSettings.fogColor = new Color(0.7f * ambientFactor, 0.7f * ambientFactor, 0.7f * ambientFactor);

        string lightsourceName = lightObjectInfo["name"].str;
        //Debug.Log("StelLight::UpdateLightObject: Lightsource Name:" + lightsourceName+" Set flare...");
        SetLightsourcePropertiesByName(lightsourceName); // set main characteristics

        //Debug.Log("Now setting light magnitudes: " + lightObjectInfo.ToString());

        if (lightsourceName=="none")
        {
            return;
        }

        float alt = (float) lightObjectInfo["altitude"].n;
        float az = (float) lightObjectInfo["azimuth"].n + controller.northAngle;
        SetAzAlt(az, alt);

        // allow dimming and reddening close to horizon.
        float extinctedMag = (float) lightObjectInfo["vmage"].n - (float) lightObjectInfo["vmag"].n;
        float magFactorGreen = Mathf.Pow(0.85f, 0.6f * extinctedMag);
        float magFactorBlue = Mathf.Pow(0.6f, 0.5f * extinctedMag);

        if (lightsourceName == "Sun")
        {
            SetColor(1.4f, Mathf.Pow(0.75f, extinctedMag) * 1.4f, Mathf.Pow(0.42f, 0.9f * extinctedMag) * 1.4f);
        }
        else if (lightsourceName == "Moon")
        {
            float moonPower = (float) lightObjectInfo["illumination"].n * 0.01f;
            moonPower *= 0.25f*moonPower; // This should provide the "full moon peak": 25% brightness at Full Moon, much less in lower phases.
            SetColor(moonPower, magFactorGreen*moonPower, magFactorBlue*moonPower);
        }
        else if (lightsourceName == "Venus")
        {
            float venusPower = (float) lightObjectInfo["vmag"].n * -0.01f; // this is quite dim, likely still brighter than natural, but OK to make it more apparent.
            SetColor(venusPower, magFactorGreen*venusPower, magFactorBlue*venusPower);
        }

        // Make a Raycast into the world to see whether the light source hides behind anything. If yes, we better switch off the flare.
        Vector3 lightDir = new Vector3(Mathf.Cos(alt * Mathf.PI / 180.0f) * Mathf.Sin(az * Mathf.PI / 180.0f),
                                       Mathf.Sin(alt * Mathf.PI / 180.0f),
                                       Mathf.Cos(alt * Mathf.PI / 180.0f) * Mathf.Cos(az * Mathf.PI / 180.0f));
        bool lightHidden = Physics.Raycast(origin: Camera.main.transform.position, direction: lightDir);
        if (lightHidden)
        {
            //Debug.Log("Raycast to "+lightDir.x + "/"+lightDir.y+"/"+lightDir.z+" hit some target. Disabling flare.");
            stelLight.flare = null;
        }
    }

    public void SetColor(float r, float g, float b)
    {
        stelLight.color = new Color(r, g, b);
        sunImpostorSphere.GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(r, g, Mathf.Min(b, 0.9f)));
    }

    // Set astronomical azimuth and altitude for the luminary. This corrects for the StelController's northAngle. 
    public void SetAzAlt(float az, float alt)
    {
        //Debug.Log("StelLight: setAzAlt()");
        transform.eulerAngles = new Vector3(alt, az - 90.0f, 0.0f);
    }

    // Set the light properties: name can only be "Sun", "Moon", "Venus". Any other switches off the light. 
    // Venus casts hard shadows with small flare, Sun and Moon have soft shadows and different flares. 
    // If camera foV is smaller that 10 degrees, flare is disabled (it is assumed that we intentionally look into the light source then)
    public void SetLightsourcePropertiesByName(string name)
    {
        float foV = Camera.main.fieldOfView;
        if (name=="Sun")
        {
            //Debug.Log("StelLight:setLightsourcePropertiesByName=" + name);
            stelLight.flare = (foV < 10.0f ? null : sunFlare);
            stelLight.shadows = LightShadows.Soft;
            stelLight.enabled = true;
        }
        else if (name == "Moon")
        {
            //Debug.Log("StelLight:setLightsourcePropertiesByName=" + name);
            stelLight.flare = (foV < 10.0f ? null : moonFlare);
            stelLight.shadows = LightShadows.Soft;
            stelLight.enabled = true;
        }
        else if (name == "Venus")
        {
            //Debug.Log("StelLight:setLightsourcePropertiesByName=" + name);
            stelLight.flare = (foV < 10.0f ? null : venusFlare);
            stelLight.shadows = LightShadows.Hard;
            stelLight.enabled = true;
        }
        else
        {
            //Debug.Log("StelLight::setLightSourceName= " + name+", disabled.");
            stelLight.flare = null;
            stelLight.shadows = LightShadows.None;
            stelLight.enabled = false;
        }
    }

    // This sets Light.intensity to factor. Use this to manually overexpose e.g. to discern lunar or Venus shadow better. Range 0...8
    public void Overexpose(float factor)
    {
        stelLight.intensity = Mathf.Clamp(factor, 0.0f, max: 8.0f);
    }
}
