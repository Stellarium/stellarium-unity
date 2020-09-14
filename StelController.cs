/*
 * StelController: Part of the Stellarium-Unity bridge tools (c) 2017 by Georg Zotti and John Fillwalk.
 * 
 * This module takes sole responsibility for communication with a running instance of Stellarium.
 * Any setter/getter methods (time, location, longitude/latitude, Field of view, property switching etc.) should be implemented here. 
 * The GUI (IDEA Labs) should communicate with this module only and not with other JSON tools.
 * 

   Instructions: 
   1. Attach the scripts StelController, StelSkyBox, StelTriggers, StelMouseFoV to an empty GameObject "Stellarium" with a Light.
   2. Start Stellarium and activate RemoteControl plugin before starting the Unity game. Run the script once from Stellarium to create the Skybox textures.
   3. During gameplay, press F11 to update the Skybox with whatever Stellarium shows now.
                             F12 to toggle Skybox vs. Spout mode
                             F8/F9 to move 1 hour back/forward
                             See StelKeyboardTriggers for the complete list.
   Further functionality can be implemented using the remote control API mentioned above. For example. the mouse-wheel-based FoV script can set FoV in Unity and Stellarium.

   Note: You must call the available IEnumerator functions as Coroutines! E.g. StartCoroutine(mouseZoom.setFoV(fieldOfView));

   History: 
   2017 first workking version
   2018-01 GZ fixed for Unity 2017.3: WWW POST operations with WWWForms failed, no data were transferred. The only working solution is UnityWebRequest with a Dictionary payload.
   2018-01 GZ adapted to requirement to run the scene also in WebGL: StreamingSkybox. This should eventually replace the StelSkybox. 
   2018-09 GZ adapt to changed JSON interface from Stellarium to use double/boolean/string, not string-only. 
 */
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

[RequireComponent(requiredComponent: typeof(StreamingSkybox))]
public class StelController : MonoBehaviour {

    // These can be used for the planet field in the locations.
    public enum Planet { Sun, Mercury, Venus, Earth, Moon, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, Io, Europa, Ganymede, Callisto }
    public enum Bortle { Excellent=1, TrulyDark, Rural, RuralSuburbanTransition, Suburban, BrightSuburban, SuburbanUrbanTransition, City, InnerCity }

#if UNITY_WEBGL
    public readonly bool connectToStellarium = false; // WebGL version never connects to running Stellarium.
#else
    [Tooltip("True if an instance of Stellarium is assumed running in the background, creating skyboxes or spouting the scene background.")]
    public bool connectToStellarium = true; 
    // On start, we may auto-switch to false when connection cannot be established. The application may still continue to work in Skybox mode.
    // TODO: Change to private for release builds. The public is just for in-editor switching. 
#endif
    public int stelPort = 8090; // IP port of running Stellarium RemoteControl instance on localhost.
    [Tooltip("With Stellarium 0.18.1 and later, this should be the included 'skybox.ssc'")]
    public string skyboxScriptName = "skybox.ssc"; 
    [Tooltip("Location name to send to Stellarium")]
    public string locationname="Unity3D";
    [Tooltip("Country name to send to Stellarium")]
    public string country="UnityLand";
    [Tooltip("Location planet to send to Stellarium")]
    public StelController.Planet planet= StelController.Planet.Earth;
    [Tooltip("Location longitude to send to Stellarium [degrees, positive towards east]")]
    public float longitude=16.25f;
    [Tooltip("Location latitude to send to Stellarium [degrees, positive towards north]")]
    public float latitude=48.2f;
    [Tooltip("Location altitude (MAMSL) to send to Stellarium [m]")]
    public float altitude=280.0f;     // metres above mean sea level
    [Tooltip("Atmosphere pressure to send to Stellarium for refraction computation [deg. C]")]
    public float atmosphereTemperature = 10.0f;     
    [Tooltip("Atmosphere pressure to send to Stellarium for refraction computation [mbar]")]
    public float atmospherePressure    = 1013.0f;   
    [Tooltip("Extinction factor (atmosphere haze) to send to Stellarium, [mag/airmass]")]
    public float atmosphereExtinctionFactor = 0.2f; 
    [Tooltip("Sky Quality (Bortle index) to send to Stellarium")]
    public Bortle lightPollutionBortleIndex = Bortle.TrulyDark; // 1=perfect natural dry mountain sky, 2=great natural lowland, 3=very good, 5=today suburban, 9=today city centre light pollution.
    [Tooltip("Rotation angle to compensate for meridian convergence offset of grid-based coordinates. This is the azimuth of True North in terms of the local grid coordinate system.")]
    public float northAngle = 0.0f; // To correct meridian convergence offset. This is the Azimuth of True North in terms of the local grid coordinate system.

    [Tooltip("A GameObject usually in a Canvas whose Text component gets updated with new timestring if needed.")]
    public GameObject guiTimeText;

    // 
    [Tooltip("Enable Spout at startup? If false, we should disable some communication like view direction changes, FoV changes.")]
    public bool spoutMode = false;
    // echo of this variable. Used to detect changes which require a few switches, e.g. activating camera skybox and deactivating StelBackgroundMaterial.
    private bool oldSpoutMode = false;

    // We make use of the JSONObject class in numerous places to keep a copy of Stellarium's internal state in the same way as it is delivered to the web remote control plugin.
    // In Start() we retrieve state of a running instance of Stellarium (which must have RemoteControl plugin active and listening on stelPort). 
    // In 
#if !UNITY_WEBGL
    private JSONObject json;           // state tracking object. 
    private int actionId = -1111;        // required for communication with Stellarium
    private int propertyId = -2222;      // required for communication with Stellarium
    private JSONObject jsonActions;    // will be used to track the actionChanges field of the json state tracking object.
    private JSONObject jsonProperties; // will be used to track the propertyChanges field of the json state tracking object.
#endif
    private JSONObject jsonTime;       // will be used to track the propertyChanges:time field of the json state tracking object.

    // To access object information, we use 4 JSONObjects for Sun, Moon, Venus and anything else.
#if !UNITY_WEBGL
    private JSONObject jsonObjInfo;
#endif
    private JSONObject jsonSunInfo;
    private JSONObject jsonMoonInfo;
    private JSONObject jsonVenusInfo;

#if !UNITY_WEBGL
    private bool flagQueryObjectInfo=false;   // used to limit queries to Stellarium. (Like a Mutex)
    private bool flagSetViewDirection=false;   // used to limit queries to Stellarium. (Like a Mutex)
#endif

    // To control either Skybox or Spout mode, we need access to the StelBackground GameObject to disable this in Skybox mode.
#if !UNITY_WEBGL
    private GameObject stelBackground;
#endif
//    private StelSkybox stelSkybox;
    private StreamingSkybox streamingSkybox;

    //    void Awake()
    //    {
    //   }

    void Awake()
    {
#if UNITY_WEBGL
        oldSpoutMode = false;
        spoutMode = false;
#else
        oldSpoutMode = spoutMode;
        stelBackground = GameObject.Find("StelBackground");
        if (!stelBackground)
        {
            Debug.LogError("StelController: Cannot find StelBackground. Scene setup wrong!");
        }
//        stelSkybox = gameObject.GetComponent<StelSkybox>();
//        if (!stelSkybox)
//        {
//            Debug.LogError("StelController: Cannot find StelSkybox. Scene setup wrong!");
//        }
#endif
        streamingSkybox = gameObject.GetComponent<StreamingSkybox>();
        if (!streamingSkybox)
        {
            Debug.LogError("StelController: Cannot find StreamingSkybox. Scene setup wrong!");
        }
    }

    void Start()
    { 
        ConfigureSpoutMode();
        // retrieve Stellarium state. In case RemoteControl is not active, we can activate it later but need to ensure initialisation in most methods below.
        StartCoroutine(InitializeStelJson());
        ConfigureSpoutMode(); // call again in case no connection was found.
    }

    private void ConfigureSpoutMode()
    {
#if UNITY_WEBGL
            Camera.main.clearFlags = CameraClearFlags.Skybox;
#else
        if (connectToStellarium && spoutMode)
        {
            // direct view, no skybox
            Camera.main.clearFlags = CameraClearFlags.Depth;
            stelBackground.SetActive(true);
        }
        else
        {
            // skybox mode
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            stelBackground.SetActive(false);
        }
#endif
    }

    void Update()
    {
        if (spoutMode !=oldSpoutMode) // a switch has occurred
        {
            oldSpoutMode = spoutMode;
            ConfigureSpoutMode();
        }
//#if UNITY_WEBGL
        if (!jsonTime)
        {
            //Debug.Log("StelController::Update()");
            StartCoroutine(UpdateStelJson());
        }
//#endif
    }

    public bool JsonIsValid()
    {
#if UNITY_WEBGL
        return false;
#else
        return (json != null);
#endif
    }

    // This is called in Start() to retrieve a complete state of Stellarium into json and later be able to retrieve only the change deltas.
    // changes: json, actionId, propertyId
    private IEnumerator InitializeStelJson()
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // In case we don't get an answer, disable connection but keep playing in skybox mode.

            string url = "http://localhost:" + stelPort + "/api/main/status?propId=-2&actionId=-2"; // use -2 as IDs to retrieve complete state.
            UnityWebRequest uwr = UnityWebRequest.Get(url); //, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.InitializeStelJson() failed." + uwr.error + "; Continue without connection.");
                spoutMode = false;
                connectToStellarium = false;
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.InitializeStelJson(): Problem with answer from Stellarium: " + uwr.responseCode + "; Continue without connection.");
                spoutMode = false;
                connectToStellarium = false;
                yield break;
            }

            // Parse JSON answer
            json = new JSONObject(uwr.downloadHandler.text);

            jsonActions = json.GetField("actionChanges");
            //Debug.Log("actionChanges has type " + jsonActions.type); // should be OBJECT
            jsonActions.GetField(ref actionId, "id");
            //Debug.Log("actionChanges id: " + actionId);

            jsonProperties = json.GetField("propertyChanges");
            //Debug.Log("propertyChanges has type " + jsonProperties.type); // should be OBJECT
            jsonProperties.GetField(ref propertyId, "id");
            //Debug.Log("propertyChanges id: " + propertyId);

            Debug.Log("JSON initialized. actionId=" + actionId + ", propId=" + propertyId);

            // This must be done immediately to ensure proper view. The updated settings will be in jsonProperties after the next retrieval: 
            StartCoroutine(SetProperty("StelCore.currentProjectionTypeKey", "ProjectionPerspective", false));
            StartCoroutine(SetProperty("StelMovementMgr.viewportVerticalOffsetTarget", "0.0", false));
            StartCoroutine(SetLocation(latitude, longitude, altitude, locationname, country, planet));
            StartCoroutine(SetProperty("StelSkyDrawer.extinctionCoefficient", atmosphereExtinctionFactor.ToString(), false));
            StartCoroutine(SetProperty("StelSkyDrawer.atmosphereTemperature", atmosphereTemperature.ToString(), false));
            StartCoroutine(SetProperty("StelSkyDrawer.atmospherePressure", atmospherePressure.ToString(), false));
            StartCoroutine(SetProperty("StelSkyDrawer.bortleScaleIndex", ((int)lightPollutionBortleIndex).ToString(), true));
        }
#endif
    }



    // This must be called after Start() to retrieve an update to the state changes of Stellarium into the json state.
    // changes: json, actionId, propertyId
    // TODO: Change to using UWR
    private IEnumerator UpdateStelJson()
    {
#if !UNITY_WEBGL
        if (connectToStellarium && spoutMode) // Esp time updates are not useful while in skybox mode!
        {
            //Debug.Log("UpdateStelJson()...");
            // retrieve deltas
            string url = "http://localhost:" + stelPort + "/api/main/status?actionId=" + actionId + "&propId=" + propertyId;
            WWW www = new WWW(url);
            yield return www;
            //Debug.Log("WWW answer (JSON update):" + www.text);
            // Parse JSON answer!
            JSONObject json2 = new JSONObject(www.text); // this should be a rather short JSON with only the changed actions/properties.
            if (json && json.Count>0 && json2 && json2.Count>0)
            {
                json.Merge(json2);
                jsonActions = json.GetField("actionChanges");
                if (jsonActions && jsonActions.HasField("id")) { jsonActions.GetField(ref actionId, "id"); }
                jsonProperties = json.GetField("propertyChanges");
                if (jsonProperties && jsonProperties.HasField("id")) { jsonProperties.GetField(ref propertyId, "id"); }
                jsonTime = json.GetField("time");
                //Debug.Log("Json now: " + json.Print(true));
            }
            else
            {
                Debug.LogWarning("StelController::UpdateStelJson(): json invalid. Setting to Deltas. Things may break from now!");
                json = json2;
                connectToStellarium = false;
                spoutMode = false;
                jsonTime = streamingSkybox.GetJsonTime();
                StartCoroutine(QueryObjectInfo("Sun"));
                StartCoroutine(QueryObjectInfo("Moon"));
                StartCoroutine(QueryObjectInfo("Venus"));
            }
        }
        else
        {
#endif
        //Debug.Log(message: "StelController::UpdateStelJson(): getting jsonTime from streaming Skybox...");
		jsonTime=streamingSkybox.GetJsonTime();
        StartCoroutine(QueryObjectInfo("Sun"));
        StartCoroutine(QueryObjectInfo("Moon"));
        StartCoroutine(QueryObjectInfo("Venus"));
#if !UNITY_WEBGL
        }

        //Debug.Log("JSON updated. actionId=" + actionId + " propId=" + propertyId);
        //Debug.Log("jsonTime: " + jsonTime.Print(true));
        //string utcString ="";
        //jsonTime.GetField(ref utcString, "utc");
        //Debug.Log("jsonTime: utc=" + utcString);
        //Debug.Log("UpdateStelJson()...DONE");
#endif
        UpdateTimeGUI();
#if UNITY_WEBGL
        // just to fulfill return type.
        yield break;
#endif
    }

    private void UpdateTimeGUI()
    {
        if (guiTimeText)
        {
            UnityEngine.UI.Text uiText = guiTimeText.GetComponent<UnityEngine.UI.Text>();
            uiText.text = jsonTime["local"].str;
        }
    }

    public IEnumerator UpdateSkyboxTiles()
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            string url = "http://localhost:" + stelPort + "/api/scripts/run";

            Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "id", skyboxScriptName }
            };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.UpdateSkyboxTiles() failed." + uwr.error);
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.UpdateSkyboxTiles(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //Debug.Log(message: "StelController.UpdateSkyboxTiles() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                if (uwr.downloadHandler.text != "ok")
                {
                    Debug.LogWarning("StelController.UpdateSkyboxTiles(): Cannot update skybox tiles via HTTP.");
                }
            }
            // Also get latest data...
            yield return StartCoroutine(UpdateStelJson());
        }
#endif
    }

    public IEnumerator TimePlus1h()
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            jsonTime = json.GetField("time");
            //Debug.Log("Time:" + jsonTime.Print());
            double jday = 0;
            if (jsonTime.GetField(ref jday, "jday"))
            {
                string url = "http://localhost:" + stelPort + "/api/main/time";
                Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "time", (jday + 1.0 / 24.0).ToString() },
                { "timerate", "0" }
            };
                UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
                uwr.chunkedTransfer = false;
                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError)
                {
                    Debug.LogWarning("StelController.TimePlus1h() failed." + uwr.error);
                    yield break;
                }
                else if (uwr.isHttpError)
                {
                    Debug.LogWarning("StelController.TimePlus1h(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                else
                {
                    //Debug.Log(message: "StelController.TimePlus1h() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                    if (uwr.downloadHandler.text != "ok")
                    {
                        Debug.LogWarning("StelController.TimePlus1h(): Cannot update skybox tiles via HTTP.");
                    }
                    else
                    {
                        yield return StartCoroutine(UpdateStelJson());
                    }
                }
            }
            else
            {
                Debug.LogWarning("StelController.TimePlus1h(): Cannot read JD from JSON");
            }
        }
#endif
    }

    public IEnumerator TimeMinus1h()
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            jsonTime = json.GetField("time");
            double jday = 0;
            if (jsonTime.GetField(ref jday, "jday"))
            {
                //Debug.Log("Current JD:" + jday);
                string url = "http://localhost:" + stelPort + "/api/main/time";
                Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "time", (jday - 1.0 / 24.0).ToString() },
                { "timerate", "0" }
            };
                UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
                uwr.chunkedTransfer = false;
                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError)
                {
                    Debug.LogWarning("StelController.TimeMinus1h() failed." + uwr.error);
                    yield break;
                }
                else if (uwr.isHttpError)
                {
                    Debug.LogWarning("StelController.TimeMinus1h(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                else
                {
                    //Debug.Log(message: "StelController.TimeMinus1h() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                    if (uwr.downloadHandler.text != "ok")
                    {
                        Debug.LogWarning("StelController.TimeMinus1h(): Cannot update skybox tiles via HTTP.");
                    }
                    else
                    {
                        yield return StartCoroutine(UpdateStelJson());
                    }
                }
            }
            else
            {
                Debug.LogWarning("StelController.TimeMinus1h(): Cannot read JD from JSON");
            }
        }
#endif
    }

#if !UNITY_WEBGL
    // Send a new date as Julian Day Number to Stellarium.
    public IEnumerator SetJD(double newJD)
    {
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            jsonTime = json.GetField("time");
            double jday = 0;
            if (jsonTime.GetField(ref jday, "jday"))
            {
                //Debug.Log("Current JD:" + jday);
                string url = "http://localhost:" + stelPort + "/api/main/time";
                Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "time", (newJD).ToString() },
                { "timerate", "0" }
            };
                UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
                uwr.chunkedTransfer = false;
                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError)
                {
                    Debug.LogWarning("StelController.SetJD() failed." + uwr.error);
                    yield break;
                }
                else if (uwr.isHttpError)
                {
                    Debug.LogWarning("StelController.SetJD(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                else
                {
                    //Debug.Log(message: "StelController.SetJD() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                    if (uwr.downloadHandler.text != "ok")
                    {
                        Debug.LogWarning("StelController.SetJD(): Cannot set JD via HTTP.");
                    }
                    else
                    {
                        yield return StartCoroutine(UpdateSkyboxTiles());
                        yield return StartCoroutine(UpdateStelJson());
                    }
                }
            }
            else
            {
                Debug.LogWarning("StelController.SetJD(): Cannot read JD from JSON");
            }
        }
    }

    // Send a new time rate to Stellarium. (1=real-time flow, higher numbers to speed-up)
    public IEnumerator SetTimerate(double newTimerate)
    {
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            jsonTime = json.GetField("time");
            double jday = 0;
            if (jsonTime.GetField(ref jday, "jday"))
            {
                //Debug.Log("Current JD:" + jday);
                string url = "http://localhost:" + stelPort + "/api/main/time";
                Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "timerate", newTimerate.ToString() }
            };
                UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
                uwr.chunkedTransfer = false;
                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError)
                {
                    Debug.LogWarning("StelController.SetTimerate() failed." + uwr.error);
                    yield break;
                }
                else if (uwr.isHttpError)
                {
                    Debug.LogWarning("StelController.SetTimerate(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                else
                {
                    //Debug.Log(message: "StelController.SetTimerate() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                    if (uwr.downloadHandler.text != "ok")
                    {
                        Debug.LogWarning("StelController.SetTimerate(): Cannot set Timerate via HTTP.");
                    }
                    else
                    {
                        yield return StartCoroutine(UpdateStelJson());
                        yield return StartCoroutine(UpdateSkyboxTiles());
                    }
                }
            }
            else
            {
                Debug.LogWarning("Cannot read JD from JSON. Time corrupt?");
            }
        }
    }
#endif

    // The following function does nothing as long as we use the skybox. In Spout mode, transmit FoV.
    public IEnumerator SetFoV(double newFov)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium && spoutMode)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            string url = "http://localhost:" + stelPort + "/api/main/fov";
            Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "fov", (newFov).ToString() }
            };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.SetFoV() failed." + uwr.error);
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.SetFoV(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //Debug.Log(message: "StelController.SetFoV() complete! --> Answer: " + uwr.responseCode + " " + uwr.downloadHandler.text);
                if (uwr.downloadHandler.text != "ok")
                {
                    Debug.LogWarning("StelController.SetFoV(): Cannot set FoV via HTTP.");
                }
            }
        }
#endif
    }


    // The following function is rather useless as long as we use the skybox. But it is a test whether JSON messages are ok.
    // DO NOT LINK IT WITH MOUSE ZOOMING when using the skybox: It is possible to change FoV while skybox tiles are created.
    // 2018: In SPOUT mode, we must limit the queries to Stellarium, else HTTP error 503: too many connections. 
    // Azimuth, altitude must be given in radians. 
    // Call via StartCoroutine()!
    public IEnumerator SetViewDirection(double az, double alt)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium && spoutMode)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            if (flagSetViewDirection)
            {
                //Debug.LogWarning("setViewDirection: Request running. Skipping this one.");
                yield break;
            }

            flagSetViewDirection = true;
            string url = "http://localhost:" + stelPort + "/api/main/view";
            Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "az", az.ToString() },
                { "alt", alt.ToString() }
            };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.SetViewDirection() failed." + uwr.error);
                flagSetViewDirection = false;
                yield break;
            }
            else if (uwr.isHttpError)
            { 
                Debug.LogWarning("StelController.SetViewDirection(): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //Debug.Log(message: "StelController.SetViewDirection() complete! --> Answer: " + uwr.downloadHandler.text);
                if (uwr.downloadHandler.text != "ok")
                {
                    Debug.LogWarning("Cannot set view direction via HTTP.");
                }
            }
            flagSetViewDirection = false;
        }
#endif
    }

    // Call via StartCoroutine()!
    public IEnumerator SetLocation(string id)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            string url = "http://localhost:" + stelPort + "/api/location/setlocationfields";
            Dictionary<string, string> payload = new Dictionary<string, string>
        {
            { "id", id }
        };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.SetLocation(id) failed." + uwr.error);
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.SetLocation(id): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //Debug.Log("Location Form upload complete! Sent set: id=" + id + " etc. --> Received: " + uwr.downloadHandler.text);
                if (uwr.downloadHandler.text == "ok")
                {
                    yield return StartCoroutine(UpdateStelJson());
                }
                else
                {
                    Debug.LogWarning("StelController.SetLocation(id): Cannot set location via HTTP.");
                }
            }
        }
#endif
    }

    // Call via StartCoroutine()!
    public IEnumerator SetLocation(float latitude, float longitude, float altitude, string name, string country, Planet planet)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (!json) InitializeStelJson();

            string url = "http://localhost:" + stelPort + "/api/location/setlocationfields";
            Dictionary<string, string> payload = new Dictionary<string, string>
        {
            { "latitude", (latitude).ToString() },
            { "longitude", (longitude).ToString() },
            { "altitude", (altitude).ToString() },
            { "name", name },
            { "country", country },
            { "planet", planet.ToString() }
        };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.SetLocation(" + name + ") failed." + uwr.error);
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.SetLocation(" + name + "): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //Debug.Log("Location Form upload complete! Sent set: name=" + name + " etc. --> Received: " + uwr.downloadHandler.text);
                if (uwr.downloadHandler.text.StartsWith("ok"))
                {
                    yield return StartCoroutine(UpdateStelJson());
                }
                else
                {
                    Debug.LogWarning("StelController.SetLocation(" + name + "): Cannot set location via HTTP:" + uwr.downloadHandler.text);
                }
            }
        }
#endif
    }

    public IEnumerator DoAction(string actionName, bool updateJson = true)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (updateJson && !json) InitializeStelJson();

            string url = "http://localhost:" + stelPort + "/api/stelaction/do";
            Dictionary<string, string> payload = new Dictionary<string, string>
            {
                { "id", actionName }
            };
            UnityWebRequest www = UnityWebRequest.Post(url, payload);
            www.chunkedTransfer = false;
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogWarning("StelController.DoAction(" + actionName + ") failed." + www.error);
            }
            else
            {
                //Debug.Log(message: "DoAction() complete! Sent set: id=" + actionName + " --> Received: " + www.downloadHandler.text);
                if ((www.downloadHandler.text == "true") || (www.downloadHandler.text == "false") || (www.downloadHandler.text == "ok"))
                {
                    if (updateJson)
                        yield return StartCoroutine(UpdateStelJson());
                }
                else
                {
                    Debug.LogWarning(message: "Could not trigger action " + actionName + " via HTTP: " + www.downloadHandler.text);
                }
            }
        }
#endif
    }

    // return the cached property as string. Users of this method must know how to further process the propery, bool, float, etc.
    // may return "null" when json not initialized.
    public string GetPropertyValue(string propertyName)
    {
#if !UNITY_WEBGL
        if (jsonProperties)
        {
            JSONObject jsonPropChanges = jsonProperties.GetField("changes");
            try
            {
                return jsonPropChanges[propertyName].ToString(); 
            }
            catch (System.Exception)
            {
                return "null";
            }
        }
        else
#endif
            return "null";
    }

    public IEnumerator SetProperty(string propertyName, string newValue, bool updateJson=true)
    {
#if UNITY_WEBGL
        yield break;
#else
        if (connectToStellarium)
        {
            // This requires that we track the full state of Stellarium with the JSON. 
            if (updateJson && !json) InitializeStelJson();

            string url = "http://localhost:" + stelPort + "/api/stelproperty/set";
            Dictionary<string, string> payload = new Dictionary<string, string>
        {
            { "id", propertyName },
            { "value", newValue }
        };
            UnityWebRequest uwr = UnityWebRequest.Post(url, payload);
            uwr.chunkedTransfer = false;
            yield return uwr.SendWebRequest();

            //bool responseOK = false;
            if (uwr.isNetworkError)
            {
                Debug.LogWarning("StelController.SetProperty(" + propertyName + ") failed." + uwr.error);
                yield break;
            }
            else if (uwr.isHttpError)
            {
                Debug.LogWarning("StelController.SetProperty(" + propertyName + "): Problem with WWW answer: " + uwr.responseCode + "; wait a bit before retrying.");
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                //responseOK = true;
                //Debug.Log(message: "Form upload complete! Sent set:" + propertyName + "=" + newValue + "--> Received: " + uwr.responseCode + ": " +uwr.downloadHandler.text);
                if (uwr.downloadHandler.text == "ok")
                {
                    if (updateJson)
                        yield return StartCoroutine(UpdateStelJson());
                }
            }
        }
#endif
    }

    // This retrieves a JSON formatted info map of data for the object given. We need this mostly to get the light source, i.e., "Sun", "Moon" or "Venus".
    // The retrieved map can be accessed by getLastObjectInfo()
    // This requires Stellarium build 9125 (beta 0.90.9127 from 2017-02-05?) or later.
    private IEnumerator QueryObjectInfo(string objectName)
    {
#if !UNITY_WEBGL
        //if (!connectToStellarium)
        if (!spoutMode)
        {
#endif
        if (!streamingSkybox) Debug.LogError("no streamingSkybox defined???");
        if (objectName=="Sun") jsonSunInfo = streamingSkybox.GetSunInfo();
        if (objectName == "Moon") jsonMoonInfo = streamingSkybox.GetMoonInfo();
        if (objectName == "Venus") jsonVenusInfo = streamingSkybox.GetVenusInfo();
        yield break;
#if !UNITY_WEBGL
        }
        else
        {
            if (flagQueryObjectInfo)
            {
                //Debug.LogWarning("queryObjectInfo(" + objectName + "): a query is already running; wait .5s before retrying.");
                yield return new WaitForSecondsRealtime(.5f);
                yield break;
            }

            flagQueryObjectInfo = true;
            string url = "http://localhost:" + stelPort + "/api/objects/info?name=" + objectName + "&format=map";
            WWW www = new WWW(url);
            yield return www;

            bool responseOK = false;
            if (www.responseHeaders.Count > 0)
            {
                //Debug.Log("getObjectInfo(" + objectName + ") --> WWW answer:" + www.text);
                foreach (KeyValuePair<string, string> entry in www.responseHeaders)
                {
                    //Debug.Log(entry.Key + "=" + entry.Value);
                    if ((entry.Key == "STATUS") && entry.Value.Contains("200 OK"))
                        responseOK = true;
                }
            }

            if (responseOK)
            {
                //Debug.Log("getObjectInfo(" + objectName + ") --> WWW answer:" + www.text);

                // Put this into jsonObjInfo. In case it is also a light source, put it into the respective objects.
                jsonObjInfo = new JSONObject(www.text);
                if (objectName == "Sun")
                    jsonSunInfo = new JSONObject(www.text);
                else if (objectName == "Moon")
                    jsonMoonInfo = new JSONObject(www.text);
                else if (objectName == "Venus")
                    jsonVenusInfo = new JSONObject(www.text);
            }
            else
            {
                Debug.LogWarning("queryObjectInfo error. Headers:" + www.responseHeaders);
                if (www.responseHeaders["STATUS"] != null)
                {
                    Debug.LogWarning("queryObjectInfo(" + objectName + "): Problem with WWW answer: " + www.responseHeaders["STATUS"] + "; wait 2.5s before retrying.");
                }

                yield return new WaitForSecondsRealtime(2.5f);
            }
            flagQueryObjectInfo = false;
        }
#endif
    }

#if !UNITY_WEBGL
        // Retrieve a JSON object which contains all data about the last queried object retrieved by Stellarium's scripting function. May return null! 
        public JSONObject GetLastObjectInfo()
    {
        return jsonObjInfo;
    }
#endif

    // Update the JSON objects which contain all data about the possible 3 light source retrieved by Stellarium's scripting function.
    public IEnumerator UpdateLightObjInfo()
    {
        yield return QueryObjectInfo("Sun");
        if (jsonSunInfo == null)
            Debug.LogError("Sun not retrieved");
        //else
        //    Debug.Log("Sun at " + jsonSunInfo["altitude"].n + "°");
        yield return QueryObjectInfo("Moon");
        if (jsonMoonInfo == null)
            Debug.LogError("Moon not retrieved");
        //else
        //    Debug.Log("Moon at " + jsonMoonInfo["altitude"].n + "°");
        yield return QueryObjectInfo("Venus");
        if (jsonVenusInfo == null)
            Debug.LogError("Venus not retrieved");
        //else
        //    Debug.Log("Venus at " + jsonVenusInfo["altitude"].n + "°");
    }

    // Retrieve a JSON object which contains all data about the currently active light source.
    // The JSONObjects get an added entry to make sure the light info is only updated as needed. Else Unity makes a "Too many threads" crash.
    // NOTE: This may also return null. Check the validity of the returned object!
    public JSONObject GetLightObjInfo()
    {
        double alt = 0.0f;
        if (spoutMode)
        {
            if (jsonSunInfo == null) return null;
            alt = jsonSunInfo["altitude"].n;
            //Debug.Log("StelController:getLightObjInfo: sun alt=" + alt);
            if (alt > -3.0)
            {
                //jsonSunInfo.AddField("newLight", "true");
                return jsonSunInfo;
            }
            if (jsonMoonInfo == null) return null;
            alt = jsonMoonInfo["altitude"].n;
            //Debug.Log("StelController:getLightObjInfo: moon alt=" + alt);
            if (alt > 0.0)
            {
                //Debug.Log("StelController:getLightObjInfo: moon alt=" + alt);
                //jsonMoonInfo.AddField("newLight", "true");
                return jsonMoonInfo;
            }
            if (jsonVenusInfo == null) return null;
            alt = jsonVenusInfo["altitude"].n;
            //Debug.Log("StelController:getLightObjInfo: Venus alt=" + alt);
            if (alt > 0.0)
            {
                //Debug.Log("StelController:getLightObjInfo: Venus alt=" + alt);
                //jsonVenusInfo.AddField("newLight", "true");
                return jsonVenusInfo;
            }
            JSONObject ambientLightInfo = new JSONObject();
            ambientLightInfo.AddField("ambientInt", jsonSunInfo["ambientInt"].n);
            ambientLightInfo.AddField("name", "none");
            return ambientLightInfo;
        }
        else // Skybox Mode
        {
            //Debug.Log("updating light info from data file");
            // output.txt contains only key:value pairs after running our script.
            //string filePath; 

//#if !UNITY_WEBGL
//            if (stelSkybox.isActiveAndEnabled)
//            {
//                //filePath = stelSkybox.dataDirectory + "unityData.txt";
//                //Debug.Log("StelController: Retrieving light object from stelSkyBox.");
//                return stelSkybox.GetLightObject();
//            }
//            else 
//#endif            
            if (streamingSkybox.isActiveAndEnabled)
            {
                //filePath = System.IO.Path.Combine(streamingSkybox.SkydataPath , "unityData.txt");
                //Debug.Log("StelController: Retrieving light object from streamingSkyBox.");
                return streamingSkybox.GetLightObject();
            }
            else
            {
                Debug.LogWarning("StreamingSkybox not active/enabled. Scene setup wrong!");
                //filePath = "invalid path";
                return null;
            }
        }
    }

    public string GetTimeString()
    {
        /*#if UNITY_WEBGL
                StartCoroutine(UpdateStelJson());
        #else
                if (spoutMode)
                {
                    StartCoroutine( UpdateStelJson());
                }
                else
                { // we still need to update time string from streamingSkyBox
                    jsonTime = streamingSkybox.GetJsonTime();
                }
        #endif
        */
        StartCoroutine(UpdateStelJson());

        //if (json && json["time"] && json["time"]["local"])
        if (jsonTime && jsonTime["local"])
        {
            return jsonTime["local"].str;
        }
        else
        {
            Debug.LogWarning("StelController::GetTimeString() error");
            return "StelController::GetTimeString() error";
        }
    }

    //! Function intended for other scene objects like TerrainTextureChanger. These may query solar longitude to change their own properties. 
    public float GetSolarLongitude()
    {
        if (!jsonSunInfo)
        {
            Debug.LogWarning("StelController::GetSolarLongitude(): no jsonSunInfo!");
            return 0.0f;
        }
        else
        {
            //Debug.Log("StelController::GetSolarLongitude(): jsonSunInfo=" + jsonSunInfo.ToString());
            return (float) jsonSunInfo["elong"].n;
        }
    }

    //! Function intended for other scene objects. These may query solar altitude to change their own properties (daylight behaviour?). 
    public float GetSolarAltitude()
    {
        if (!jsonSunInfo)
        {
            Debug.LogWarning("StelController::GetSolarAltitude(): no jsonSunInfo!");
            return 30.0f;
        }
        else
        {
            //Debug.Log("StelController::GetSolarAltitude(): jsonSunInfo=" + jsonSunInfo.ToString());
            return (float) jsonSunInfo["altitude"].n;
        }
    }
}

