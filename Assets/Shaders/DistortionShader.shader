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
                float2 distUV    : TEXCOORD0;  // Distorted UV to sample source texture
            };

            // Distort a UV from lens-centre-relative NDC [-1,1] and return distorted UV
            float2 Distort(float2 ndc)
            {
                float r2    = dot(ndc, ndc);
                float r4    = r2 * r2;
                float scale = 1.0 + _K1 * r2 + _K2 * r4;
                return ndc * scale;
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Convert UV [0,1] → NDC [-1,1] relative to eye centre
                float2 ndc = v.uv * 2.0 - 1.0;

                // Apply barrel distortion in vertex stage
                float2 distorted = Distort(ndc);

                // Place vertex at distorted clip position (blit-quad approach)
                o.pos = float4(distorted, 0.0, 1.0);

                // Sample the original un-distorted position in source texture
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
    }
}
