using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Drives head tracking: reads predicted pose from IIMUSource each frame (OnPreRender),
    /// applies it to the camera rig, and stores the render-time pose for ATW.
    ///
    /// Attach to the VR Camera Rig root GameObject.
    /// </summary>
    public class VRHeadTracking : MonoBehaviour
    {
        [Tooltip("IMU source component (PhoneIMUSource or ExternalIMUSource).")]
        [SerializeField] private MonoBehaviour imuSourceComponent;

        [Tooltip("VR settings asset (K1/K2/IPD/displayLatencyMs).")]
        [SerializeField] private VRSettings vrSettings;

        [Tooltip("Transform to apply head rotation to (usually the camera rig root).")]
        [SerializeField] private Transform cameraRigTransform;

        // The last predicted pose used for rendering — read by ATWController
        public static Quaternion RenderPose { get; private set; } = Quaternion.identity;

        private IIMUSource _imuSource;

        private void Awake()
        {
            _imuSource = imuSourceComponent as IIMUSource;
            if (_imuSource == null)
                Debug.LogError("[VRHeadTracking] imuSourceComponent must implement IIMUSource.");

            if (cameraRigTransform == null)
                cameraRigTransform = transform;
        }

        private void OnPreRender()
        {
            if (_imuSource == null || vrSettings == null) return;

            float targetFrameRate = Application.targetFrameRate > 0
                ? Application.targetFrameRate
                : 60f;

            // Predict ahead by one frame + display pipeline latency
            float deltaT = (1.0f / targetFrameRate) + vrSettings.displayLatencyMs / 1000f;

            Quaternion predicted = _imuSource.GetPredictedPose(deltaT);
            ApplyPoseToRig(predicted);
            RenderPose = predicted;  // Store for ATW
        }

        private void ApplyPoseToRig(Quaternion pose)
        {
            cameraRigTransform.localRotation = pose;
        }
    }
}
