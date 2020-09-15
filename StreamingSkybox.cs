/* Interfacing Unity3D with Stellarium. Joint project (started 2015-12) of Georg Zotti (LBI ArchPro) and John Fillwalk (IDIA Lab).
// Authors of this script: 
// Neil Zehr (IDIA Lab)
// David Rodriguez (IDIA Lab)
// Georg Zotti (LBI ArchPro)
// A Stellarium script (by G. Zotti) available in Stellarium 0.18.1 and later creates 6 tiles of a skybox plus data file "unityData.txt" to get information about light etc.
// This Unity3D script loads prepared skybox textures and light information from StreamingAssets folder, i.e., skybox data can be created even after publication.

// Interfacing with a running instance of Stellarium by using skyboxes has largely been superseded by the Spout mode, but may still be useful e.g. for environmental illumination (reflection maps, larger water bodies, mirroring glass, etc.) or when the small delay is really unacceptable (immersive VR applications?) 
// In addition, skyboxes do not require a running Stellarium and can even be used in a WebGL build for online publication.

   INSTRUCTIONS:
   Create folder "Stellarium" in your Assets folder
   Put StreamingSkybox.cs and the other scripts there
   Create an empty GameObject "Stellarium" in your scene, attach "Stel"* scripts to it. 

   Put Skybox textures (512^2) into subdirectories in Assets/StreamingAssets/SkyBoxes folder and configure in skyboxes.json there. 
   NEW VARIANT: A set of prepared skyboxes should be in Assets/StreamingAssets/<name>/Unity*.png + unityData.txt. 

   2018-02-02: Allow Skybox Rotation angle to compensate for meridian convergence issues. This is a site property and should be set (or value retrieved) from StelController. Requires Unity5+.
   2020-04-08: Restore auto-loading of "live" skybox.
 */
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(requiredComponent: typeof(StelController))]
public class StreamingSkybox : MonoBehaviour {

    [Tooltip("Subdirectory name in StreamingAssets folder")]
    public string defaultSkyname = "live";
    private string skyName = "live";  // subdir of StreamingAssets/SkyBoxes/ where the images written by Stellarium have been moved. 

    private Dictionary<string, Texture2D> sides = new Dictionary<string, Texture2D>(); // private dictionary for Material creation.

    private string pathBase;          // will contain the path (directory or URL) to the SkyBoxes folder. Usually, use SkydataPath to construct file paths/URLs.
    private JSONObject jsonTime;      // a small JSON format-ident to StelController*s JSONtime that represents the time data from the unityData.txt;
    private JSONObject jsonLightInfo; // a small JSON that contains data about the current luminaire from the unityData.txt. 
    private JSONObject jsonSunInfo;   // a small JSON that contains data about Sun   from the unityData.txt. 
    private JSONObject jsonMoonInfo;  // a small JSON that contains data about Moon  from the unityData.txt. 
    private JSONObject jsonVenusInfo; // a small JSON that contains data about Venus from the unityData.txt. 
    private StelController controller; // needed for location details (rotation)
                                       // Currently we don't use it, but this can change. 
                                       // It would be better to add the light info handling to the skyboxes and just get them in the StelController.

#if !UNITY_WEBGL
    FileSystemWatcher watcher;          // The watcher is used to check for updates in the skybox tiles directory. As soon as a new skybox has been generated, Skybox and SunLight will be updated. 
#endif

    private void Awake()
    {
        skyName = defaultSkyname;
        // prepare Material dictionary
        sides.Add("Unity1-north.png", null);
        sides.Add("Unity2-east.png", null);
        sides.Add("Unity3-south.png", null);
        sides.Add("Unity4-west.png", null);
        sides.Add("Unity5-top.png", null);
        sides.Add("Unity6-bottom.png", null);
        pathBase = System.IO.Path.Combine(Application.streamingAssetsPath, "SkyBoxes/");
        Debug.Log("StreamingSkybox: pathbase=" + pathBase);
        controller = gameObject.GetComponent<StelController>();
#if !UNITY_WEBGL
        watcher = new FileSystemWatcher();
#endif
    }

    private void Start()
    {
        StartCoroutine(ParseDataFile());
    }

    private void OnEnable()
    {
#if UNITY_WEBGL
        StartCoroutine(DoGetImages(SkydataPath));
#else
        watcher.Path = pathBase + "live/";
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Filter = "unityData.txt"; // "Unity6-bottom.png"; // maybe even better: unityData.txt ?
        watcher.Changed += OnLiveDirectoryChanged;
        watcher.Created += OnLiveDirectoryChanged;
        watcher.EnableRaisingEvents = true;

        DoOnMainThread.ExecuteOnMainThread.Enqueue(() => { StartCoroutine(DoGetImages(SkydataPath)); });
#endif
        StartCoroutine(ParseDataFile());
    }

    void OnDisable()
    {
#if !UNITY_WEBGL
        watcher.Changed -= OnLiveDirectoryChanged;
        watcher.Created -= OnLiveDirectoryChanged;
        watcher.EnableRaisingEvents = false;
#endif
    }

    void OnLiveDirectoryChanged(object source, FileSystemEventArgs e)
    {
        if (skyName == "live")
        {
            DoOnMainThread.ExecuteOnMainThread.Enqueue(() => { StartCoroutine(DoGetImages(SkydataPath)); });
            DoOnMainThread.ExecuteOnMainThread.Enqueue(() => { StartCoroutine(ParseDataFile()); });
        }
    }

    private Material CreateSkyboxMaterial(Dictionary<string, Texture2D> sides)
    {
        // Check if we really received the wanted textures. It seems some HTTP 304/no-change?
        //Debug.Log("Setting texture1 with " + sides["Unity4-west.png"].width + " pixels width.");
        //Debug.Log("Setting texture2 with " + sides["Unity2-east.png"].width + " pixels width.");
        //Debug.Log("Setting texture3 with " + sides["Unity3-south.png"].width + " pixels width.");
        //Debug.Log("Setting texture4 with " + sides["Unity4-west.png"].width + " pixels width.");
        //Debug.Log("Setting texture5 with " + sides["Unity5-top.png"].width + " pixels width.");
        //Debug.Log("Setting texture6 with " + sides["Unity6-bottom.png"].width + " pixels width.");
        Shader skyboxShader = Shader.Find("Skybox/6 Sided");
        if (!skyboxShader) Debug.LogError("Shader not found!");
        Material mat = new Material(skyboxShader);
        mat.SetTexture("_FrontTex", sides["Unity4-west.png"]);
        mat.SetTexture("_BackTex", sides["Unity2-east.png"]);
        mat.SetTexture("_LeftTex", sides["Unity1-north.png"]);
        mat.SetTexture("_RightTex", sides["Unity3-south.png"]);
        mat.SetTexture("_UpTex", sides["Unity5-top.png"]);
        mat.SetTexture("_DownTex", sides["Unity6-bottom.png"]);
        float rot = -controller.northAngle; if (rot < 0) rot += 360.0f;
        mat.SetFloat("_Rotation", Mathf.Clamp(rot, 0, 360));
        return mat;
    }

    public JSONObject GetSunInfo()
    {
        if (!jsonSunInfo) Debug.LogError("StreamingSkybox: sunInfo not initialized!");
        return jsonSunInfo;
    }
    public JSONObject GetMoonInfo()
    {
        if (!jsonMoonInfo) Debug.LogError("StreamingSkybox: moonInfo not initialized!");
        return jsonMoonInfo;
    }
    public JSONObject GetVenusInfo()
    {
        if (!jsonVenusInfo) Debug.LogError("StreamingSkybox: venusInfo not initialized!");
        return jsonVenusInfo;
    }

    // Readonly: path (directory or URL) to current skybox textures and info file.
    public string SkydataPath
    {
        get
        {
            Debug.Log("SkyDataPath: " + System.IO.Path.Combine(pathBase, skyName));
            return System.IO.Path.Combine(pathBase , skyName );
        }
    }

    public string SkyName
    {
        get
        {
            return skyName;
        }

        set
        {
            skyName = value;
            StartCoroutine(DoGetImages(SkydataPath));
            StartCoroutine(ParseDataFile());
        }
    }

    private void SetSkybox(Material material) {
        RenderSettings.skybox = material;
        DynamicGI.UpdateEnvironment();
    }

    // Load images from a directory (either system dir or URL), and set skybox to the resulting material. 
    private IEnumerator DoGetImages(string directory) {
        List<string> filenames = new List<string>(sides.Keys);
        //int sampleHeight = 0;
        Texture2D texture = Texture2D.whiteTexture; // new Texture2D(2, 2, TextureFormat.BGRA32, true);
        //Debug.Log("Filenames dictionary has " + filenames.Count + " entries");
        foreach (string filename in filenames) {
            string filePath = System.IO.Path.Combine(directory, filename);
            //Debug.Log("Trying to get image " + filePath);
			
			if (filePath.Contains("://")) {
                UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(filePath);
                uwr.chunkedTransfer = false;
                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError || uwr.isHttpError)
                {
                    Debug.LogError(message: "Texture file download problem: " + uwr.error + " for file: " + uwr.url);
                }
                else
                {
                    // Get downloaded asset bundle
                    texture = DownloadHandlerTexture.GetContent(uwr);
                    Debug.Log("HTTP: " + uwr.responseCode  + " --> Retrieved Texture " + uwr.url + " with " + uwr.downloadedBytes.ToString() + "Bytes");
                    if (texture == null)
                        texture = Texture2D.blackTexture;
                }
            }
            else
            {
                byte[] data= File.ReadAllBytes(filePath);
                texture = new Texture2D(width: 512, height: 512, format: TextureFormat.BGRA32, mipmap: true, linear: false);
                if (!texture.LoadImage(data))
                {
                    Debug.LogWarning(message: "CANNOT RETRIEVE TEXTURE FROM FILE" + filePath);
                }                  
            }
#if !UNITY_WEBGL
            if (texture.width != texture.height) // Likely in "live Skybox mode"
            {
                texture = CropTexture(texture);
            }
#endif
            texture.wrapMode = TextureWrapMode.Clamp;
            sides[filename] = texture;
        }
        SetSkybox(CreateSkyboxMaterial(sides));
    }

#if !UNITY_WEBGL
    // Cut central square from landscape-oriented texture. 
    public static Texture2D CropTexture(Texture2D originalTexture) {
        Rect cropRect=new Rect((originalTexture.width*.5f)-(originalTexture.height * .5f),0f,originalTexture.height,originalTexture.height);
        // Make sure the crop rectangle stays within the original Texture dimensions
        //cropRect.x = Mathf.Clamp(cropRect.x, 0, originalTexture.width);
        //cropRect.width = Mathf.Clamp(cropRect.width, 0, originalTexture.width - cropRect.x);
        //cropRect.y = Mathf.Clamp(cropRect.y, 0, originalTexture.height);
        //cropRect.height = Mathf.Clamp(cropRect.height, 0, originalTexture.height - cropRect.y);
        if(cropRect.height <= 0 || cropRect.width <= 0) return null; // dont create a Texture with size 0

        Texture2D newTexture = new Texture2D((int)cropRect.width, (int)cropRect.height, TextureFormat.RGBA32, false);
        //Texture2D newTexture = new Texture2D((int)cropRect.width, (int)cropRect.height, TextureFormat.BGRA32, false); // NOTE new BGRA!
        Color[] pixels = originalTexture.GetPixels((int)cropRect.x, (int)cropRect.y, (int)cropRect.width, (int)cropRect.height, 0);
        newTexture.SetPixels(pixels);

        //TextureScale.Bilinear(newTexture, 256, 256);
        //Debug.Log("Supported Texture Formats:");
        //if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) Debug.Log(" RGBA32");
        
        newTexture.Apply();
        newTexture.wrapMode = TextureWrapMode.Clamp;
        return newTexture;
    }
#endif

    public JSONObject GetJsonTime()
    {
        if (!jsonTime) StartCoroutine(ParseDataFile());
        return jsonTime;
    }

/*    // return a JSON for lightObject=Sun|Moon|Venus, else return null.
    public JSONObject GetObjectInfo(string lightObject)
    {

        return null;
    }
*/
    public JSONObject GetLightObject()
    {
        if (!jsonLightInfo)
        {
            Debug.LogWarning("StreamingSkybox::GetLightObject: Light object not defined!");
        }
        //Debug.Log("StreamingSkybox::GetLightObject: " + jsonLightInfo.ToString());
        return jsonLightInfo;
    }

    private IEnumerator ParseDataFile()
    {
        // Construct a JSONObject from the output.txt file.
        Dictionary<string, string> fileInfo = new Dictionary<string, string>(); // info from output.txt comes here in key/value pairs.
        fileInfo.Clear();

        string filePath = System.IO.Path.Combine(SkydataPath, "unityData.txt");

        //Debug.Log("Trying to get " + filePath);

        if (filePath.Contains("://") )
        {
            // Mostly WebGL path...
            Debug.Log("Reading data via WebRequest: " + filePath);
            UnityWebRequest www = UnityWebRequest.Get(filePath);
            yield return www.SendWebRequest();

            string text;
            Debug.Log("Asked for text file at URL:" + www.url);
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogWarning(message: "CANNOT RETRIEVE TEXT!" + www.error);
            }
            else
            {
                // Show results as text
                Debug.Log(www.downloadHandler.text);

                // Or retrieve results as binary data
                //byte[] results = www.downloadHandler.data;
            }
            text = www.downloadHandler.text;
            // TODO: Parse into fileInfo dictionary
            try
            {
                // Create an instance of StringReader to read from the returned text.
                // The using statement also closes the StringReader.
                using (StringReader sr = new StringReader(text))
                {
                    String line;
                    // Read and display lines from the file until end of string.
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Debug.Log(line);
                        String[] keyValPair = line.Split(new Char[] { ':' }, 2);
                        //Debug.Log(keyValPair);
                        if (keyValPair.Length == 2)
                            fileInfo.Add(keyValPair[0], keyValPair[1]);
                    }
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Debug.LogWarning("The www.text string could not be parsed:" + e.Message);
            }
            // At this point all data from the URL path have been read.
        }
        else
        {
            // LOCAL FILE
            //Debug.Log("Reading data from local file " + filePath);
            try
            {
                // Create an instance of StreamReader to read from a file.
                // The using statement also closes the StreamReader.
                using (StreamReader sr = new StreamReader(filePath))
                {
                    String line;
                    // Read and display lines from the file until the end of 
                    // the file is reached.
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Debug.Log(line);
                        String[] keyValPair = line.Split(new Char[] { ':' }, 2);
                        //Debug.Log(keyValPair);
                        if (keyValPair.Length == 2)
                            fileInfo.Add(keyValPair[0], keyValPair[1]);
                    }
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Debug.LogWarning("The file could not be read:" + e.Message);
            }
            // At this point all data have been read.
            // Prepare the light info object. They are currently not used, but maybe later?
        }

        jsonLightInfo = new JSONObject();
        jsonSunInfo = new JSONObject();
        jsonMoonInfo = new JSONObject();
        jsonVenusInfo = new JSONObject();
        try
        {
            jsonSunInfo.AddField("name", "Sun");
            jsonSunInfo.AddField("altitude", float.Parse(fileInfo["Sun Altitude"]));
            jsonSunInfo.AddField("azimuth",  float.Parse(fileInfo["Sun Azimuth"]));
            jsonSunInfo.AddField("vmag",     float.Parse(fileInfo["Sun Magnitude"]));
            jsonSunInfo.AddField("vmage",    float.Parse(fileInfo["Sun Magnitude (after extinction)"]));
            jsonSunInfo.AddField("elong",    float.Parse(fileInfo["Sun Longitude"]));
            if (fileInfo.ContainsKey("Sun Size"))
                jsonSunInfo.AddField("diameter", float.Parse(fileInfo["Sun Size"]));
            else
                jsonSunInfo.AddField("diameter", 0.5f);
            jsonMoonInfo.AddField("name", "Moon");
            jsonMoonInfo.AddField("altitude", float.Parse(fileInfo["Moon Altitude"]));
            jsonMoonInfo.AddField("azimuth",  float.Parse(fileInfo["Moon Azimuth"]));
            jsonMoonInfo.AddField("illumination", float.Parse(fileInfo["Moon illumination"]));
            jsonMoonInfo.AddField("vmag",     float.Parse(fileInfo["Moon Magnitude"]));
            jsonMoonInfo.AddField("vmage",    float.Parse(fileInfo["Moon Magnitude (after extinction)"]));
            //jsonMoonInfo.AddField("elong",  float.Parse(fileInfo["Moon Longitude"]));
            if (fileInfo.ContainsKey("Moon Size"))
                jsonMoonInfo.AddField("diameter", float.Parse(fileInfo["Moon Size"]));
            else
                jsonMoonInfo.AddField("diameter", 0.5f);
            jsonVenusInfo.AddField("name", "Venus");
            jsonVenusInfo.AddField("altitude", float.Parse(fileInfo["Venus Altitude"]));
            jsonVenusInfo.AddField("azimuth",  float.Parse(fileInfo["Venus Azimuth"]));
            jsonVenusInfo.AddField("vmag",     float.Parse(fileInfo["Venus Magnitude"]));
            jsonVenusInfo.AddField("vmage",    float.Parse(fileInfo["Venus Magnitude (after extinction)"]));
            //jsonVenusInfo.AddField("elong", fileInfo["Venus Longitude"]);
            jsonVenusInfo.AddField("diameter", 0.0f); // For the naked eye, we assume point source (to switch off the impostor sphere!)
            // The following field can only be in the file with Stellarium r9130+ (2017-02-05).
            if (fileInfo.ContainsKey("Landscape Brightness"))
            {
                jsonSunInfo.AddField("ambientInt",   float.Parse(fileInfo["Landscape Brightness"]));
                jsonMoonInfo.AddField("ambientInt",  float.Parse(fileInfo["Landscape Brightness"]));
                jsonVenusInfo.AddField("ambientInt", float.Parse(fileInfo["Landscape Brightness"]));
            }


            if (float.Parse(fileInfo["Sun Altitude"]) > -3.0f)
            {
                jsonLightInfo = jsonSunInfo.Copy();
            }
            else if (float.Parse(fileInfo["Moon Altitude"]) > 0.0f)
            {
                jsonLightInfo = jsonMoonInfo.Copy();
            }
            else if (float.Parse(fileInfo["Venus Altitude"]) > 0.0f)
            {
                jsonLightInfo = jsonVenusInfo.Copy();
            }
            else
            {
                jsonLightInfo.AddField("name", "none");
                // The following field can only be in the file with Stellarium r9130+ (2017-02-05).
                if (fileInfo.ContainsKey("Landscape Brightness"))
                    jsonLightInfo.AddField("ambientInt", float.Parse(fileInfo["Landscape Brightness"]));
                else
                    jsonLightInfo.AddField("ambientInt", 0.05f);
            }
        }
        catch (Exception e)
        {
            // Let the user know what went wrong.
            Debug.LogWarning("info lookup failed:" + e.Message);
            jsonLightInfo.AddField("name", "none");
            jsonLightInfo.AddField("ambientInt", 0.05f);
            //jsonLightInfo.AddField("landscape-brt", "0.05");
        }

        jsonTime = new JSONObject();
        //jsonTime.Clear();
        jsonTime.AddField("jday",      float.Parse(fileInfo["JD"])); //current Julian day
        jsonTime.AddField("deltaT",    "unknown");      //current deltaT as determined by the current dT algorithm  --> TODO: Make float! Or fix in skybox.ssc
        jsonTime.AddField("gmtShift",  "unknown");      //the timezone shift to GMT                                 --> TODO: Make float! Or fix in skybox.ssc
        jsonTime.AddField("timeZone",  "unknown");      //the timezone name                                         --> TODO: Fix in skybox.ssc and retrieve as string
        jsonTime.AddField("utc",       fileInfo["Date (UTC)"]); //the time in UTC time zone as ISO8601 time string
        jsonTime.AddField("local",     fileInfo["Date"]); //the time in local time zone as ISO8601 time string
        jsonTime.AddField("isTimeNow", false);        //if true, the Stellarium time equals the current real-world time
        jsonTime.AddField("timerate",  0.0f);          //the current time rate (in secs). Obviously 0 for a static skybox...
    }
        
}
