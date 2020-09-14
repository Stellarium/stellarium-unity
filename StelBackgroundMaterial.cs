/* StelBackgroundMaterial: Assign a render queue close to background to the game object.
 * (c) 2017 Georg Zotti
 * Part of the Stellarium Unity Bridge tools by Georg Zotti (LBI ArchPro Vienna) and John Fillwalk (IDIA Lab). 
 * 
 * USAGE:
 * Construct a Child object for the object with the First-Person Camera  (Create Empty->3D Object->Quad). Name it "StelBackground". Delete any MeshCollider.
 * In the Transform, set position.Z to 10, Scale to 16/-9/0. This gives a rough idea of a "screen".
 * In the MeshRenderer, disable all shadows. 
 * Add this script and the SpoutReceiver script. Set SpoutReceiver's sender to "stellarium". 
 * Assign Spout's Material Unlit.0 to the Mesh Renderer.
 * Replace the shader by Stellarium/Unlit-TextureBackground.
 * When running Unity, the screen should now be behind all scene objects, and should show output of Stellarium.
 * 
 * 2018: For WebGL builds, this script deactivates its GameObject at startup.
 * 2018-09: For Non-WebGL builds, we must explicitly set Spout sender name on startup.
 * 
 */
using UnityEngine;
using System.Collections;
using Spout;

public class StelBackgroundMaterial : MonoBehaviour {
#if !UNITY_WEBGL
    private float width;  // track texture size
    private float height; // track texture size
    private float fov;    // field of view of the main camera. We need this to decide whether our plane has to be resized.
                          //    private StelController controller;
#endif
    // Inits before Start().
    private void Awake()
    {
#if UNITY_WEBGL
        gameObject.SetActive(false);
#else        
        width = 0.0f;
        height = 0.0f;
        fov = 0.0f;
#endif
    }

    // Use this for initialization
    private void Start()
    {
#if !UNITY_WEBGL
        gameObject.GetComponent<SpoutReceiver>().sharingName = "stellarium";
#endif
    }

    // Update is called once per frame
    private void Update () {
#if !UNITY_WEBGL
        /*        if (!(controller.spoutMode) && (gameObject.activeInHierarchy))
                {
                    gameObject.SetActive(false);
                    return;
                }
        */
        Texture texture=gameObject.GetComponent<Renderer>().sharedMaterial.GetTexture("_MainTex");
        if ((texture != null) && ((width != texture.width) || (height != texture.height)))
        {
            width = texture.width;
            height = texture.height;
            Debug.Log("Spout Texture Size:" + width + "x" + height);
        }
        // At this point we can adapt the panel size and scale it according to the scene and camera fov.
        if (width > 40) // The texture starts with 32px size.
        {
            float newfov = Camera.main.fieldOfView; //vertical FoV
            // Scale the vertical extent of the canvas so that the canvas in 10m distance fills the view completely.
            if (newfov != fov)
            {
                fov = newfov;
                //Debug.Log("Spout: new FoV =" + fov);
                // Canvas is transform.localPosition.z =10m away. Tan(fov/2)=h/2 / z.
                //Debug.Log("Spout: localPos.z=" + transform.localPosition.z);
                float h = 2.0f * transform.localPosition.z * Mathf.Tan(fov * 0.5f * Mathf.PI / 180.0f); // vertical scale value.
                //Debug.Log("Spout: h =" + h);
                float w = h * (width / height);
                //Debug.Log("Spout: w =" + h + "*"+"("+width+"/"+height+")=" + w);
                // The negative Y is needed because OpenGL textures are inverted w.r.t. DirectX textures.
                transform.localScale= new Vector3(w, -h, 1.0f);
                //Debug.Log("Spout Texture Canvas scaled to " + w + "x" + h);
            }
        }
#endif
    }
}
