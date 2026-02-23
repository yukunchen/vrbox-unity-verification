// VRBox/LensDistortionBlit
//
// Per-pixel inverse barrel distortion for VR lens pre-correction.
// Used by EyeDistortionBlit.cs via Graphics.Blit(src, dest, material).
//
// Pipeline per output pixel:
//   1. Circular lens aperture mask  — pixels outside the circle → black
//      (replicates the physical lens boundary seen in VR headsets)
//   2. Inverse barrel distortion via Newton-Raphson (4 iterations)
//      — pre-warps the image so the headset's pincushion lens cancels it out
//
// _EyeAspect (pixelWidth/pixelHeight of the eye camera) is required so the
// circle appears round on screen rather than oval.

Shader "VRBox/LensDistortionBlit"
{
    Properties
    {
        _MainTex    ("Source Eye Render Texture", 2D) = "white" {}
        _K1         ("Radial Distortion k1",      Float) = 0.2
        _K2         ("Radial Distortion k2",      Float) = 0.05
        // Radius of the lens circle in NDC *vertical* units (1.0 = full half-height).
        // 0.9 leaves ~10 % black border at top/bottom; corners clip sooner.
        _LensRadius ("Lens Circle Radius",        Float) = 0.9
        // pixelWidth / pixelHeight of the eye camera — set by EyeDistortionBlit.cs
        _EyeAspect  ("Eye Aspect (w/h)",          Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _K1;
            float     _K2;
            float     _LensRadius;
            float     _EyeAspect;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                return o;
            }

            // Inverse via Newton-Raphson: find p_src such that Distort(p_src) = p_out
            float2 Undistort(float2 pOut)
            {
                float2 r = pOut;
                for (int i = 0; i < 4; i++)
                {
                    float r2 = dot(r, r);
                    float f  = 1.0 + _K1 * r2 + _K2 * r2 * r2;
                    float df = 1.0 + 3.0 * _K1 * r2 + 5.0 * _K2 * r2 * r2;
                    r = r - (r * f - pOut) / df;
                }
                return r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV [0,1] → NDC [-1,1]
                float2 ndc = i.uv * 2.0 - 1.0;

                // ── Circular lens aperture mask ──────────────────────────────────
                // Scale x by aspect ratio so the boundary is a visual circle on screen.
                // For each-eye pixel space: circle is round when
                //   (ndc.x * aspect)² + ndc.y² == _LensRadius²
                float2 circleCoord = float2(ndc.x * _EyeAspect, ndc.y);
                if (dot(circleCoord, circleCoord) > _LensRadius * _LensRadius)
                    return fixed4(0.0, 0.0, 0.0, 1.0);

                // ── Inverse barrel distortion ────────────────────────────────────
                float2 srcNdc = Undistort(ndc);

                // NDC [-1,1] → UV [0,1]
                float2 srcUV = srcNdc * 0.5 + 0.5;

                // Black for pixels that fall outside the rendered eye texture
                if (srcUV.x < 0.0 || srcUV.x > 1.0 || srcUV.y < 0.0 || srcUV.y > 1.0)
                    return fixed4(0.0, 0.0, 0.0, 1.0);

                return tex2D(_MainTex, srcUV);
            }
            ENDCG
        }
    }
}
