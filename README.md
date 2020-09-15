# stellarium-unity
## Bridge tools to combine Stellarium's skies with the interactivity of the Unity game engine


Georg Zotti Feb. 2017, updated May..August 2018, finalized April 2020.

This is a collaboration between Georg Zotti (LBI ArchPro Vienna) and John Fillwalk, David Rodriguez and Neil Zehr (IDIA Lab, Ball State University) suggested by Bernard Frischer (Indiana University).

We want to explore possibilities to link the high-quality sky simulation of Stellarium with the high quality and immersion factor of Unity virtual environments. 
While Stellarium's Scenery3D plugin itself can already load a static OBJ landscape model with a few million triangles, there are applications 
which require more interaction with the scene, or just more eye candy.

The .unitypackage contains the JSONobject package described below, changed to include the described changes. 

The .unitypackage contains parts of the Spout4Unity package as of 2016-12-9 with the changes decribed below.

The description below largely follows what has to be done to set up a game environment. 

Instructions for use
==============

As shortcut for projects, most elements have been combined into just two prefabs, StellariumBridge and StelFPSController. Just drag them into the scene and deactivate the default MainCamera and Light.

## Model Axes

We assume your model has been built with axes aligned to the cardinals, X-axis is pointing North, Y up to the zenith, and Z towards West.

Usually, scene modellers for real-world scenes take a digital elevation model from survey authorities which have grid axes aligned to the "grid cardinals". 
The problem is that those axes usually deviate slightly from astronomical north by the effect of meridian convergence. 
Unity's terrain cannot be rotated. To compensate for mismatches caused by meridian convergence or some other reason to have grid north deviating from geographic north, we must rotate our view into the sky. This works with the Spout mode, or by rotation of the skybox available in Unity 2017. 


## 3 Modes of Operation

### (A) Skybox Mode (Original, developed slowly 2015-12..2016-11)

A Stellarium script (which is included with Stellarium 0.18.1 in late June 2018) creates 6 skybox tiles and a text file with information about Sun, Moon and Venus.
Unity Module StelSkybox.cs detects new skybox tiles and updates Skybox and sunlight (or moonlight) shadow caster.

Advantage: Complete Skybox, allows e.g. observing reflections on water surfaces, and works with LightProbes. Also works on simple hardware or Stellarium ANGLE or Mesa mode. 

Disadvantage: Manual updates, could be auto-triggered with 0.25fps or so, but far from realtime.

A collection of skybox tiles can be procured, so that Unity can run without a running instance of Stellarium.

It is also possible to extend this idea with a repository of skyboxes and data files to select during running the Unity application.

### (B) Spout Mode (developed 2016-11..2017-02)

Stellarium 0.15.1+ on Windows acts as SpoutSender when launched with the --spout=sky command-line option. Requires hardware with the OpenGL-DirectX interoperability extension.
A 3rd-party Unity module includes Spout processing capabilities.
We use a background rectangle like a screen which is linked to the camera and render the Spout texture on top of this. 

Advantages: 

* Immediate feedback, celestial events like sunsets can be observed. 
* You can use Stellarium's web interface in a webbrowser or commands which have to be implemented in StelController.cs

Disadvantages: 

* Stellarium/Spout must run before launching the Unity Application. 
* A small lag between Unity and Stellarium's view direction. May look bad with VR headsets! Use of Stereo may work with 2 such cameras, but was not tested.
* Spout is interrupted when Spout source is resized, e.g. Stellarium changed from fullscreen to window or vice versa. You must restart the Unity Game then. 
* Works on Windows only. 

### (C) Preconfigured Skybox Mode (also usable for WebGL)
This mode basically replaces Mode (A) by also allowing the manual loading of preconfigured skyboxes. Use StreamingSkybox.cs instead of StelSkybox.cs


## Stellarium Script

skybox.ssc, available in Stellarium 0.18.2 and later, writes 6 tiles plus a text file with easily parseable information about Sun, Moon and Venus positions and brightness.


Simple setup
===========

After installation of the unitypackage, we have a few new directories:

 - The stellarium-unity folder resides in your Assets directory (as Assets/stellarium-unity). It requires
 - The Assets/Spout folder and its parts in the Plugins folder (BSD-2 license, please see its project website for details.)
 - The Assets/JSONobject folder (3rd party, MIT license. Please see it for details.) 

Drag the Stellarium and FPSController prefabs from Assets/stellarium-unity into your scene.

Prepare Assets/StreamingAssets/SkyBoxes directory with subdirectories for each preconfigured skybox snapshots, esp. a "live" folder, or install some StelSkyboxes.unitypackage.


# Fully Manual setup
If you want to recreate a more manual Installation, to learn how the things work together, read on.

##Retrieve these sources

Use git directly in your Assets Folder:

>git clone https://github.com/Stellarium/stellarium-unity

## Setting up JSON

Since V0.15, settings in Stellarium can be changed via an HTTP control interface. 
The API is documented at http://www.stellarium.org/doc/head/remoteControlApi.html

Stellarium 0.18.1+ includes a skybox generation script designed to work together with the StelSkybox.cs and StreamingSkybox.cs Unity scripts.
To properly work with the HTTP, we also need a JSON parser class. 
For Unity, we use this free one:  <https://assetstore.unity.com/packages/tools/input-management/json-object-710>, also available at <https://github.com/mtschoen/JSONObject>

This has to be installed to Assets/JSONobject. Again in your Assets directory:

>git clone https://github.com/mtschoen/JSONObject

CAUTION: You must deactivate the #define USEFLOAT in Assets/JSONobject/JSONObject.cs line 3 -  we need double!

In Addition, we must add two Methods:

    public static JSONObject Create(double val)
    {
        JSONObject obj = Create();
        obj.type = Type.NUMBER;
        obj.n = val;
        return obj;
    }
    public void AddField(string name, double val) {
        AddField(name, Create(val));
    }

## Setting up the Stellarium Controller

Most classes should be added to a dedicated GameObject in the scene root:

Create New: Light: DirectionalLight called "Stellarium"

Attach the following classes from the Assets/Stellarium folder:

### StelLight: 

Responsible for configuring the Light component to represent Sun, Moon or Venus as light sources.

Assets->Import Package: Effects: We need the LightFlares (or make your own)

Set some Flares for Sun, Moon and Venus (optional).

### StelController: 

* Communicates with the Stellarium instance which has to run on the local machine. 
* Configure port number for communication with Stellarium's RemoteControl plugin.
* Set details about geolocation of your scene. 

### StelGaze: 

Used in Spout mode to transfer the view direction from Unity to Stellarium. 
Stellarium does not like to look into the zenith/nadir. In your First Person Controller (e.g. Unity's FPSController prefab), limit the MouseLook X range to -89..+89. 

### StelMouseZoom: 

Zooms the camera with mouse wheel, and can transfer the Field of View to Stellarium (in Spout mode). 


### StelSkyBox: (DEPRECATED. Not good for new projects.)

* Detects updates in the rendered Skybox tiles, updates the skybox. 
* Slow interface, because we are using files (6xPNG plus a text file) written by Stellarium.
* Set the paths to the files written by Stellarium. Usually imgDirectory would be C:\Users\YOU\Pictures\Stellarium\, and dataDirectory C:\Users\YOU\AppData\Roaming\Stellarium\.

### StreamingSkybox: 

* Can load skybox textures created after compilation of the Unity project.
* Detects updates in the rendered Skybox tiles, updates the skybox. 
* Slow interface, because we are using files (6xPNG plus a text file) written by Stellarium.
* Stellarium writes to %STEL\_SKYBOX\_DIR%, which may be something like D:\MyWorks\StellariumUnitySkybox\StellariumSkybox\Assets\StreamingAssets\SkyBoxes\live* 
 * Preconfigured skyboxes should be located in other subdirs of D:\MyWorks\StellariumUnitySkybox\StellariumSkybox\Assets\StreamingAssets\SkyBoxes
 * At runtime, change env. variable %STEL\_SKYBOX\_DIR% to something like D:\MyUnityApp\StreamingAssets\SkyBoxes\live
* **Note** You must run the skybox.ssc script once from Stellarium to have working skybox data. 
* **Note** Make sure you have configured Stellarium to create PNG screenshots.


### StelKeyboardTriggers:  

Examples for hotkey configuration: 

* 1, 2, ...9: Switch skybox to prepared tiles in StreamingAssets\SkyBoxes\1|2|...|9
* 0: Switch skybox to prepared tiles in StreamingAssets\SkyBoxes\live
* F8:  One hour earlier. Just sets time in Stellarium, does not update skybox.
* F9:  One hour later. Just sets time in Stellarium, does not update skybox.
* F11: Render new skybox tiles into the "live" directory. To see that sky, use "0". 
* F12: Toggle Spout/Skybox mode. 
* Like in Stellarium: 
   - J slower 
   - K stop
   - L faster
   - U Toggle ArchaeoLines
   - T Toggle aTmosphere (A in Stellarium, but WASD are motion keys.)
   - Q Toggle Cardinals
   - G Toggle Ground
   - C Constellation lines
   - V Constellation labels
   - R Constellation artwork 
   - B Constellation Boundaries
   - E Equatorial Grid
   - Z Horizontal Grid
   - H Horizon Line
   - M Meridian Line
   - Comma Ecliptic Line
   - Period Equator Line
* Num4 Toggle enlarging the Moon
* Num8 Time="Now"


## Setting up a Spout-filled Background

Setup Spout from <https://spout.zeal.co>. Maybe not required but helpful to have it for testing Stellarium's output with a "neutral" SpoutReceiver.
Download Spout4Unity from <https://github.com/sloopidoopi/Spout4Unity>. Just make a ZIP snapshot from the git, or clone with git outside of your Unity Project,

> git clone https://github.com/sloopidoopi/Spout4Unity

Close Unity and Visual Studio. Make a backup of your Unity project, just in case :-) 
From the Spout repository Spout4Unity, copy Assets/Plugins and Assets/Spout to the Assets Folder. Delete/do not copy Assets/Spout/Scenes.

### Ad-Hoc Bugfixes in a Unity5.4 environment:

Scripts/Spout.cs: Enclose lines 107 with:

    #if ! UNITY_EDITOR
                   DontDestroyOnLoad(_instance.gameObject);
    #endif

For a 2017 Version is was necessary to also replace line 22 in Scripts/InvertCamera.csby:

    //GL.SetRevertBackfacing(true);
    GL.invertCulling = true;
 and line 26 by:

    //GL.SetRevertBackfacing(false);
    GL.invertCulling = false;


### Aligning a "background canvas" with the camera

Based on using the Default: Import Unity Package: Characters : FirstPersonCharacter

 * Create a new GameObject as child of your FPS camera: 
 * Create Empty 3D Object: Quad. Call it "StelBackground".
 * Transform: Pos 0/0/10, Scale 16/-9/1. (Assume 16:9 screen 10m in front of the camera for now. We need to invert Y! scale.Z is likely irrelevant.)
 * Remove the automatically created Mesh Collider. 
 * MeshRenderer settings: Cast shadows: Off. Receive Shadows: No. Motion Vectors: No(?)  
 * Add the SpoutReceiver script (from Assets/Spout/Scripts), select sender: stellarium (or "any" if you don't have another).  
 * Replace the default Material by Unlit.0 from Assets/Spout/Materials. Then change the Shader to Stellarium/Unlit-TextureBackground.
 * Add the Stellarium/StelBackgroundMaterial script to the StelBackground GameObject. This ensures that the panel is scaled to fill the vertical field of view to be the same as Stellarium. 
 * In the usual application case, a fullscreen Stellarium will be mapped to a fullscreen Unity, seamlessly and without resampling losses. Changing view direction with the mouse will show a slight lag (0.15s?). This should be acceptable for a research-grade application. 


<https://forum.unity3d.com/threads/subshader-with-zwrite-off-visible-in-scene-view-but-not-in-game-preview.269379/> describes issues with rendering a background object with the skybox. 

Works in editor, but not in Game view, because skybox is apparently rendered after the scene (fills pixels with empty zbuffer values only).
Therefore we have a modified shader rendering the Spout texture to Background, and the camera must be switched to CameraClearFlags.Depth.


## Setting up a Graphical User Interface

The hotkeys defined in StelKeyboardTriggers.cs work, but are limited and need to be learned. A GUI can be built e.g. for time adjustments or graphical toggles like emulating the Stellarium GUI on top of the Unity game. This will be situation dependent and must be decided per application.


References
========

Publications should be listed here.
Possible submissions:

- Georg Zotti. A Virtual Park of Astronomical Instruments. Proc. SEAC 2018, to appear.
- Georg Zotti, Bernard Frischer, John Fillwalk. Serious Gaming for Virtual Archaeoastronomy. Studies in Digital Heritage 4(1), 2020.

License
======

We make this package available for collaborating research in archaeoastronomy and cultural astronomy, which should lead to collaborative publications. 

Released September 15, 2020 under GPLv3. 