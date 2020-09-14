/* DO NOT USE!  OLD CODE LEFT FOR DOCUMENTATION! USE StreamingSkybox now.
 * Interfacing Unity3D with Stellarium. Joint project (started 2015-12) of Georg Zotti (LBI ArchPro) and John Fillwalk (IDIA Lab).
// Authors of this script: 
// Neil Zehr (IDIA Lab)
// David Rodriguez (IDIA Lab)
// Georg Zotti (LBI ArchPro)
// A Stellarium script (by G. Zotti) creates 6 tiles of a skybox plus data file "unityData.txt" to get information about light etc.
// This Unity3D script auto-detects changes in the image directory, and reloads skybox and light information.

// The skybox version has largely been superseded by the Spout mode, but may still be useful e.g. for environmental illumination (reflection maps, larger water bodies, mirroring glass, etc.)
// For WebGL Builds, this script has been practically disabled. Use StreamingSkybox with a few pre-build skyboxes in this case.

   INSTRUCTIONS:
   Create folder "Stellarium" in your Assets folder
   Put StelSkybox.cs and the other scripts there
   Create an empty GameObject "Stellarium" in your scene, attach "Stel"* scripts to it. 
   Set the directories to those used by Stellarium (Usually, C:\Users\YOU\Pictures\Stellarium\ and C:\Users\YOU\AppData\Roaming\Stellarium)
 */
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(requiredComponent: typeof(StelController))]
public class StelSkybox : MonoBehaviour {

    private StelController controller;   
    public string imgDirectory;  // where the images are written to by the script. Usually C:\Users\YOU\Pictures\Stellarium
    public string dataDirectory; // where output.txt is written to. Usually C:\Users\YOU\AppData\Roaming\Stellarium
    private JSONObject jsonTime;      // a small JSON format-ident to StelController*s JSONtime that represents the time data from the unityData.txt;
    private JSONObject jsonLightInfo; // a small JSON that contains data about the current luminaire from the unityData.txt. 

#if !UNITY_WEBGL
    private Dictionary<string, Texture2D> sides = new Dictionary<string, Texture2D>();
    //private Dictionary<string, string> info = new Dictionary<string, string>(); // info from output.txt comes here in key/value pairs.


    // The watcher is used to check for updates in the skybox tiles directory. As soon as a new skybox has been generated, Skybox and SunLight will be updated. 
    FileSystemWatcher watcher;


    void Awake() {
        controller = gameObject.GetComponent<StelController>();
        if (!controller)
        {
            Debug.LogError("StelSkyBox: Cannot find StelController. Scene setup wrong!");
        }
        // GZ: I find this not necessary. Maybe move this to StelController?
        // Application.runInBackground = true;
        sides.Add("Unity1-north.png", null);
        sides.Add("Unity2-east.png", null);
        sides.Add("Unity3-south.png", null);
        sides.Add("Unity4-west.png", null);
        sides.Add("Unity5-top.png", null);
        sides.Add("Unity6-bottom.png", null);
        watcher = new FileSystemWatcher();
    }

    void OnEnable() {
        // TODO: Decide to maybe watch directory of the unityData.txt file?
        watcher.Path = imgDirectory;
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Filter = "Unity6-bottom.png";
        watcher.Changed += OnDirectoryChanged;
        watcher.Created += OnDirectoryChanged;
        watcher.EnableRaisingEvents = true;
    }

    void Start()
    {
        StartCoroutine(DoGetImages(imgDirectory));
    }

    void OnDisable() {
        watcher.Changed -= OnDirectoryChanged;
        watcher.Created -= OnDirectoryChanged;
        watcher.EnableRaisingEvents = false;
    }

    void OnDirectoryChanged(object source, FileSystemEventArgs e) {
        DoOnMainThread.ExecuteOnMainThread.Enqueue(() => { StartCoroutine(DoGetImages(imgDirectory)); });
        StartCoroutine(ParseDataFile());
    }

    Material CreateSkyboxMaterial(Dictionary<string, Texture2D> sides) {
        Material mat = new Material(Shader.Find("Skybox/6 Sided")); // Find("RenderFX/Skybox"));
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

    void SetSkybox(Material material) {
        RenderSettings.skybox = material;
    }

    private IEnumerator DoGetImages(string directory) {
        List<string> filenames = new List<string>(sides.Keys);
        //int sampleHeight = 0;
        foreach(string filename in filenames) {
            Debug.Log("StelSkyBox::DoGetImages: Trying to get " + "file:///" + directory + filename);
            WWW www = new WWW("file://" + directory + filename);
            yield return www;
            if (!www.texture)
                Debug.LogWarning(message: "CANNOT RETRIEVE TEXTURE!" + www.error);
            // The screen snapshots are in screen layout, which we assume is "landscape" oriented, height FoV=90°.
            // Crop the central square of each side.
            sides[filename] = CropTexture(www.texture,new Rect((www.texture.width*.5f)-(www.texture.height*.5f),0f,www.texture.height,www.texture.height));
            //sampleHeight = sides[filename].height;
        }
        SetSkybox(CreateSkyboxMaterial(sides));
    }
#endif
    public static Texture2D CropTexture(Texture2D originalTexture, Rect cropRect) {
        // Make sure the crop rectangle stays within the original Texture dimensions
        cropRect.x = Mathf.Clamp(cropRect.x, 0, originalTexture.width);
        cropRect.width = Mathf.Clamp(cropRect.width, 0, originalTexture.width - cropRect.x);
        cropRect.y = Mathf.Clamp(cropRect.y, 0, originalTexture.height);
        cropRect.height = Mathf.Clamp(cropRect.height, 0, originalTexture.height - cropRect.y);
        if(cropRect.height <= 0 || cropRect.width <= 0) return null; // dont create a Texture with size 0

        Texture2D newTexture = new Texture2D((int)cropRect.width, (int)cropRect.height, TextureFormat.RGBA32, false);
        Color[] pixels = originalTexture.GetPixels((int)cropRect.x, (int)cropRect.y, (int)cropRect.width, (int)cropRect.height, 0);
        newTexture.SetPixels(pixels);
        newTexture.Apply();
        newTexture.wrapMode = TextureWrapMode.Clamp;
        return newTexture;
    }

    public JSONObject GetLightObject()
    {
        return jsonLightInfo;
    }

    private IEnumerator ParseDataFile()
    {
        // Construct a JSONObject from the output.txt file.
        Dictionary<string, string> info = new Dictionary<string, string>(); // info from output.txt comes here in key/value pairs.
        //info.Clear();
        // yield return info;
        string filePath = dataDirectory + "unityData.txt";
        try
        {
            // Create an instance of StreamReader to read from a file.
            // The using statement also closes the StreamReader.
            //Debug.Log("Reading data from" + filePath);
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
                        info.Add(keyValPair[0], keyValPair[1]);
                }
            }
        }
        catch (Exception e)
        {
            // Let the user know what went wrong.
            Debug.LogWarning("StelSkybox::ParseDataFile(): The file could not be read:" + e.Message);
        }
        // At this point all data have been read.

        // info dump:
        //Debug.Log("Info (output.txt) has " + info.Count + " values: ");
        //foreach (var entry in info)
        //    Debug.Log("|" + entry.Key + "| -- " + entry.Value);

        jsonLightInfo = new JSONObject();
        yield return jsonLightInfo;
        try
        {
            if (float.Parse(info["Sun Altitude"]) > -3.0f)
            {
                jsonLightInfo.AddField("name", "Sun");
                jsonLightInfo.AddField("altitude", info["Sun Altitude"]);
                jsonLightInfo.AddField("azimuth", info["Sun Azimuth"]);
                jsonLightInfo.AddField("vmag", info["Sun Magnitude"]);
                jsonLightInfo.AddField("vmage", info["Sun Magnitude (after extinction)"]);
            }
            else if (float.Parse(info["Moon Altitude"]) > 0.0f)
            {
                jsonLightInfo.AddField("name", "Moon");
                jsonLightInfo.AddField("altitude", info["Moon Altitude"]);
                jsonLightInfo.AddField("azimuth", info["Moon Azimuth"]);
                jsonLightInfo.AddField("illumination", info["Moon illumination"]);
                jsonLightInfo.AddField("vmag", info["Moon Magnitude"]);
                jsonLightInfo.AddField("vmage", info["Moon Magnitude (after extinction)"]);
            }
            else if (float.Parse(info["Venus Altitude"]) > 0.0f)
            {
                jsonLightInfo.AddField("name", "Venus");
                jsonLightInfo.AddField("altitude", info["Venus Altitude"]);
                jsonLightInfo.AddField("azimuth", info["Venus Azimuth"]);
                jsonLightInfo.AddField("vmag", info["Venus Magnitude"]);
                jsonLightInfo.AddField("vmage", info["Venus Magnitude (after extinction)"]);
            }
            else
            {
                jsonLightInfo.AddField("name", "none");
            }
            // The following field can only be in the file with Stellarium r9130+ (2017-02-05).
            if (info.ContainsKey("Landscape Brightness"))
                jsonLightInfo.AddField("ambientInt", info["Landscape Brightness"]);
            else
                jsonLightInfo.AddField("ambientInt", "0.05");
        }
        catch (Exception e)
        {
            // Let the user know what went wrong.
            Debug.LogWarning("info lookup failed:" + e.Message);
            jsonLightInfo.AddField("name", "none");
            jsonLightInfo.AddField("ambientInt", "0.05");
        }
    }

}
