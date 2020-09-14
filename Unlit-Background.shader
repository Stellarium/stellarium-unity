// Unlit shader for the Background. Simplest possible textured shader.
// - no lighting
// - no lightmap support
// - no per-material color
// - Renders to Background. Can be assigned to cam-facing quads as scene background canvas.
// Author: Georg Zotti, LBI ArchPro modified this for the Stellarium Unity Bridge Tools (c) 2017 Georg Zotti.
// Original shader: Unlit-Normal.shader from Unity 5.4 shader sources.   
// Changes: Just changed the queue and name, and added originally the line ZWrite Off.
// Then I saw that the skybox makes the background object invisible. Described in
// https://forum.unity3d.com/threads/subshader-with-zwrite-off-visible-in-scene-view-but-not-in-game-preview.269379/ 
// Solution; write Z, but a very high value. Now skybox does not overwrite.

Shader "Stellarium/Unlit-TextureBackground" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

SubShader {
	Tags { "RenderType" = "Background" // "Opaque" 
	       "Queue" = "Background+500" } // This should be rendered after the skybox.

	LOD 100
	//ZWrite On // Actually off would make more sense, but Skybox would overwrite! We must push this to the very end.
	ZWrite Off

	Pass {  
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				UNITY_FOG_COORDS(1)
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				// push the screen space position to just less than the far plane. Trick from the website above.
				//o.vertex.z = 0.9999; 
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.texcoord);
				UNITY_APPLY_FOG(i.fogCoord, col);
				UNITY_OPAQUE_ALPHA(col.a);
				return col;
			}
		ENDCG
	}
}

}
