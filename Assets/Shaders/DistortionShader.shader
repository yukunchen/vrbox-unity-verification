Shader "VRBox/LensDistortion"
{
    // Brown barrel distortion model: r' = r(1 + k1*r² + k2*r⁴)
    // Applied as a vertex displacement on a dense quad mesh so the
    // GPU rasterizer interpolates between displaced vertices.
    // For highest accuracy, use a sufficiently tessellated mesh (≥ 32x32 quads per eye).

    Properties
    {
        _MainTex ("Source Eye Render Texture", 2D) = "white" {}
        _K1      ("Radial Distortion k1", Float) = 0.2
        _K2      ("Radial Distortion k2", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        // Pass 0: Vertex-displacement on a dense quad mesh (legacy, kept for reference)
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float     _K1;
            float     _K2;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 distUV    : TEXCOORD0;
            };

            float2 Distort(float2 ndc)
            {
                float r2    = dot(ndc, ndc);
                float scale = 1.0 + _K1 * r2 + _K2 * r2 * r2;
                return ndc * scale;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float2 ndc      = v.uv * 2.0 - 1.0;
                float2 distorted = Distort(ndc);
                o.pos    = float4(distorted, 0.0, 1.0);
                o.distUV = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, TRANSFORM_TEX(i.distUV, _MainTex));
            }
            ENDCG
        }

        // Pass 1: Per-pixel inverse distortion — works with Graphics.Blit (no mesh needed).
        // For each output pixel p_out, finds source sample p_src via Newton-Raphson so that
        // Distort(p_src) = p_out, producing a barrel-pre-distorted image.
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_blit
            #pragma fragment frag_blit
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _K1;
            float     _K2;

            struct v2f_blit
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f_blit vert_blit(appdata_img v)
            {
                v2f_blit o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                return o;
            }

            // Forward barrel: r_out = r_in * (1 + k1*r_in² + k2*r_in⁴)
            float2 Distort(float2 ndc)
            {
                float r2 = dot(ndc, ndc);
                return ndc * (1.0 + _K1 * r2 + _K2 * r2 * r2);
            }

            // Inverse via Newton-Raphson (4 iterations): find p_src given p_out
            float2 Undistort(float2 pOut)
            {
                float2 r = pOut;
                for (int i = 0; i < 4; i++)
                {
                    float r2   = dot(r, r);
                    float f    = 1.0 + _K1 * r2 + _K2 * r2 * r2;
                    float df   = 1.0 + 3.0 * _K1 * r2 + 5.0 * _K2 * r2 * r2;
                    r = r - (r * f - pOut) / df;
                }
                return r;
            }

            fixed4 frag_blit(v2f_blit i) : SV_Target
            {
                float2 ndc    = i.uv * 2.0 - 1.0;          // [0,1] → [-1,1]
                float2 srcNdc = Undistort(ndc);              // inverse barrel
                float2 srcUV  = srcNdc * 0.5 + 0.5;         // [-1,1] → [0,1]

                // Black border outside the source eye texture
                if (any(srcUV < 0.0) || any(srcUV > 1.0))
                    return fixed4(0.0, 0.0, 0.0, 1.0);

                return tex2D(_MainTex, srcUV);
            }
            ENDCG
        }
    }
}
