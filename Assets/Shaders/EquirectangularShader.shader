Shader "VRBox/Equirectangular360"
{
    Properties
    {
        _MainTex     ("360 Texture (Equirectangular)", 2D) = "white" {}
        _Eye         ("Eye (0=Left, 1=Right)", Float) = 0
        _StereoMode  ("Stereo Mode (0=Mono, 1=TopBottom, 2=SideBySide)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        Cull Front          // Render inside of sphere
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _Eye;
            float     _StereoMode;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 dir    : TEXCOORD0;   // Object-space direction = world direction for unit sphere
            };

            static const float PI = 3.14159265358979f;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Use object-space vertex position as view direction (sphere is unit radius)
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            float2 DirToEquirect(float3 dir)
            {
                // Equirectangular mapping:
                //   u ∈ [0,1]: longitude (yaw)  — atan2(z, x)
                //   v ∈ [0,1]: latitude (pitch) — acos(y)
                float u = (atan2(dir.z, dir.x) + PI) / (2.0 * PI);
                float v = acos(clamp(dir.y, -1.0, 1.0)) / PI;
                return float2(u, v);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = DirToEquirect(normalize(i.dir));

                // Over-Under stereo: top half = left eye, bottom half = right eye
                if (_StereoMode > 0.5)  // TopBottom
                {
                    uv.y = uv.y * 0.5 + _Eye * 0.5;
                }
                // Side-by-side stereo: left half = left eye, right half = right eye
                else if (_StereoMode > 1.5)  // SideBySide
                {
                    uv.x = uv.x * 0.5 + _Eye * 0.5;
                }

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
