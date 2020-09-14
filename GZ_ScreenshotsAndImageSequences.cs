// GZ20130416, based on the script found at: http://wiki.unity3d.com/index.php?title=ScreenShotMovie
// Updated and renamed 2018-07-29.

using UnityEngine;

public class GZ_ScreenshotsAndImageSequences : MonoBehaviour
{
#if UNITY_WEBGL
    // No screenshots in WebGL...
    // Would be nice to say something in the Editor GUI.
    [Header("No screenshots in WebGL!")]
    [Tooltip("Just leave it alone.")]
    public bool blu;
    public readonly bool dummy = false;
    private void Start()
    {
        
    }
#else
    [Tooltip("Use the User's 'Pictures' directory or give another directory in the field below.")]
    public bool useDefaultPicturesDir = true;
    [Tooltip("The folder we place all screenshots inside. Leave empty and enable default (above) for useful default. If the folder exists we will append numbers to create an empty folder.")]
    public string BaseFolder;

    public bool enableScreenshots;
    [Tooltip("Key to press to create screenshot. Sorry, no modifiers.")]
    public KeyCode stillKeycode = KeyCode.F3;
    public bool enableSequenceRecording;
    [Tooltip("Key to press to start stop image sequence. Sorry, no modifiers.")]
    public KeyCode movieKeycode = KeyCode.F4;
    [Tooltip("Frames per second for movie capturing.")]
    public int frameRate = 25;


    private bool recordSeries = false; // toggle to trace recording state
    private string realFolder = "";
    private bool recordSeriesPathHasBeenSetup = false;

    private void Awake()
    {
        // Initialize this at runtime to the actual user's Picture directory!
        if (useDefaultPicturesDir)
            BaseFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
    }
    //	void Start()
    //	{
    //		// Set the playback framerate!
    //		// (real time doesn't influence time anymore)
    //		// Time.captureFramerate = frameRate;
    // 
    //	}
    // 
    void LateUpdate()
    {
        if (recordSeries && recordSeriesPathHasBeenSetup)
        {
            // name is "realFolder/0005 shot.png"
            var name = string.Format("{0}/{1:D05}.png", realFolder, Time.frameCount);

            // Capture the screenshot
            ScreenCapture.CaptureScreenshot(name);
        }


        // Make single screenshot with e.g. F3 
        if (!recordSeries && enableScreenshots && Input.GetKeyUp(stillKeycode))
        {
            string filename = BaseFolder + "/UnityStill_" + System.DateTime.Now.ToString("yyyyMMdd-HHmm");
            string realFilename = filename;
            int count = 1;
            while (System.IO.File.Exists(realFilename + ".png"))
            {
                realFilename = filename + "-" + count;
                count++;
            }
            ScreenCapture.CaptureScreenshot(realFilename + ".png", 2); // double resolution!
        }

        // toggle recording with e.g. F4
        if (enableSequenceRecording && Input.GetKeyUp(movieKeycode))
        {
            recordSeries = !recordSeries;
            if (!recordSeries)
            {
                recordSeriesPathHasBeenSetup = false; // really stop recording  
                Time.captureFramerate = 0; // reactivate fastest-possible gameplay
            }
            else
            {
                // Find a folder that doesn't exist yet by appending numbers!
                string folder = BaseFolder + "/UnityCapture_" + System.DateTime.Now.ToString("yyyyMMdd-HHmm");
                realFolder = folder;
                int count = 1;
                while (System.IO.Directory.Exists(realFolder))
                {
                    realFolder = folder + "-" + count;
                    count++;
                }
                // Create the folder
                System.IO.Directory.CreateDirectory(realFolder);


                Time.captureFramerate = frameRate; // reduce fps to configured movie rate
                recordSeriesPathHasBeenSetup = true; // signal "all set up for new capture seqence" 
            }

        }
    }
#endif
}
