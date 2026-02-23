using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Applies Brown barrel distortion pre-correction to one eye's rendered output.
    /// Attach to Left Camera and Right Camera (done automatically by VRBoxSceneSetup).
    ///
    /// Pipeline (LensDistortionBlit shader):
    ///   1. Circular lens aperture mask — black outside the circle (replicates VR lens boundary)
    ///   2. Inverse barrel distortion (Newton-Raphson 4 iterations) — pre-warps the image
    ///      so the headset's pincushion lens cancels it out
    ///
    /// distortionMaterial must reference a saved Material asset so Unity's iOS build system
    /// includes the shader. VRBoxSceneSetup creates Assets/VR/LensDistortionBlit_Mat.mat.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EyeDistortionBlit : MonoBehaviour
    {
        [SerializeField] private VRSettings  vrSettings;
        [SerializeField] private MonoBehaviour imuSourceComponent;

        // Must be a saved Material asset — raw Shader fields are stripped by iOS build.
        [SerializeField] private Material distortionMaterial;

        private Material   _mat;
        private Camera     _camera;
        private IIMUSource _imuSource;

        private void Awake()
        {
            _camera    = GetComponent<Camera>();
            _imuSource = imuSourceComponent as IIMUSource;
            if (imuSourceComponent != null && _imuSource == null)
                Debug.LogWarning("[EyeDistortionBlit] imuSourceComponent does not implement IIMUSource — ATW disabled.");

            Debug.Log($"[EyeDistortionBlit] Awake on '{name}'. " +
                      $"material={(distortionMaterial != null ? distortionMaterial.name : "NULL")}, " +
                      $"vrSettings={(vrSettings != null ? "OK" : "NULL")}, " +
                      $"atw={((_imuSource != null) ? "enabled" : "disabled (no IMU source)")}");

            if (distortionMaterial == null)
            {
                Debug.LogError("[EyeDistortionBlit] distortionMaterial is null. " +
                               "Re-run VRBox → Setup Phase 1 Scene to fix the reference.");
                return;
            }

            // Instance so per-frame SetFloat doesn't dirty the shared asset
            _mat = new Material(distortionMaterial) { hideFlags = HideFlags.HideAndDontSave };
            Debug.Log($"[EyeDistortionBlit] Ready on '{name}'. k1={K1():F3} k2={K2():F3}");
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (_mat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            // Eye aspect ratio — must be recomputed each frame in case resolution changes
            float eyeAspect = (float)_camera.pixelWidth / _camera.pixelHeight;

            _mat.SetFloat("_K1",         K1());
            _mat.SetFloat("_K2",         K2());
            _mat.SetFloat("_EyeAspect",  eyeAspect);
            // _LensRadius stays at the material default (0.9) unless overridden in Inspector

            // ── ATW warp uniforms ────────────────────────────────────────────
            if (_imuSource != null)
            {
                Quaternion qNow    = _imuSource.GetCurrentPose();
                Quaternion qRender = VRHeadTracking.RenderPose;
                // warpQ: maps display-time view direction → render-time source direction
                // = Inverse(qRender) * qNow  (NOT ATWMath.ComputeDelta which is the opposite)
                Quaternion warpQ = Quaternion.Inverse(qRender) * qNow;
                _mat.SetMatrix("_ATWRotation", Matrix4x4.Rotate(warpQ));
            }
            else
            {
                _mat.SetMatrix("_ATWRotation", Matrix4x4.identity);
            }

            float fovV       = vrSettings != null ? vrSettings.fovDegrees : 90f;
            float tanHalfFovV = Mathf.Tan(fovV * 0.5f * Mathf.Deg2Rad);
            _mat.SetFloat("_TanHalfFovV", tanHalfFovV);
            _mat.SetFloat("_TanHalfFovH", tanHalfFovV * eyeAspect);

            // Single-pass blit — Unity manages Metal render-target + viewport for each eye
            Graphics.Blit(src, dest, _mat);
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private float K1() => vrSettings != null ? vrSettings.k1 : 0.2f;
        private float K2() => vrSettings != null ? vrSettings.k2 : 0.05f;
    }
}
