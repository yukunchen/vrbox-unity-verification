using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Manages left-eye and right-eye cameras for stereo VR rendering.
    /// Sets viewport rectangles (left/right half of screen) and applies IPD offset.
    ///
    /// Pure projection math extracted into StereoProjection static class for testability.
    /// </summary>
    public class StereoCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera leftCamera;
        [SerializeField] private Camera rightCamera;
        [SerializeField] private VRSettings vrSettings;

        private void OnEnable()
        {
            ApplyStereoCameraSetup();
        }

        private void OnValidate()
        {
            if (leftCamera != null && rightCamera != null && vrSettings != null)
                ApplyStereoCameraSetup();
        }

        /// <summary>
        /// Reconfigure cameras based on current VRSettings (call after settings change).
        /// </summary>
        public void ApplyStereoCameraSetup()
        {
            if (leftCamera == null || rightCamera == null || vrSettings == null) return;

            // Viewport: left eye = left half, right eye = right half
            leftCamera.rect  = new Rect(0f,    0f, 0.5f, 1f);
            rightCamera.rect = new Rect(0.5f,  0f, 0.5f, 1f);

            // FOV
            leftCamera.fieldOfView  = vrSettings.fovDegrees;
            rightCamera.fieldOfView = vrSettings.fovDegrees;

            // IPD offset — shift camera positions relative to rig root
            float halfIpd = vrSettings.ipd * 0.5f;
            leftCamera.transform.localPosition  = new Vector3(-halfIpd, 0f, 0f);
            rightCamera.transform.localPosition = new Vector3( halfIpd, 0f, 0f);

            // Asymmetric projection matrices for proper stereo convergence
            leftCamera.projectionMatrix  = StereoProjection.BuildProjectionMatrix(
                vrSettings.fovDegrees, leftCamera.aspect,  leftCamera.nearClipPlane, leftCamera.farClipPlane,  halfIpd);
            rightCamera.projectionMatrix = StereoProjection.BuildProjectionMatrix(
                vrSettings.fovDegrees, rightCamera.aspect, rightCamera.nearClipPlane, rightCamera.farClipPlane, -halfIpd);
        }
    }

    /// <summary>
    /// Pure-function stereo projection math — no Unity component dependencies.
    /// </summary>
    public static class StereoProjection
    {
        /// <summary>
        /// Build an off-axis (asymmetric frustum) projection matrix for one eye.
        /// </summary>
        /// <param name="fovDegrees">Vertical field of view in degrees.</param>
        /// <param name="aspect">Viewport width / height ratio.</param>
        /// <param name="near">Near clip plane distance.</param>
        /// <param name="far">Far clip plane distance.</param>
        /// <param name="eyeOffset">Horizontal eye offset (+halfIPD left, -halfIPD right).</param>
        public static Matrix4x4 BuildProjectionMatrix(
            float fovDegrees, float aspect, float near, float far, float eyeOffset)
        {
            float top    = near * Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            float bottom = -top;
            float right  =  top * aspect;
            float left   = -right;

            // Shift frustum by eye offset (shear projection)
            float shear = eyeOffset * near / (far * 0.5f + near * 0.5f);
            left  += shear;
            right += shear;

            return Matrix4x4.Frustum(left, right, bottom, top, near, far);
        }
    }
}
