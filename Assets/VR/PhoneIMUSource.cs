using UnityEngine;
using UnityEngine.InputSystem;

namespace VRBox
{
    /// <summary>
    /// IMU source using the phone's built-in sensors via Unity Input System.
    /// Used during early development / when no external hardware is connected.
    ///
    /// Requires: com.unity.inputsystem package.
    /// AttitudeSensor  → device orientation quaternion.
    /// Gyroscope       → angular velocity (rad/s) for pose prediction.
    /// </summary>
    public class PhoneIMUSource : MonoBehaviour, IIMUSource
    {
        // Cached sensor references
        private AttitudeSensor                    _attitudeSensor;
        private UnityEngine.InputSystem.Gyroscope _gyroSensor;

        private void OnEnable()
        {
            _attitudeSensor = AttitudeSensor.current;
            _gyroSensor     = UnityEngine.InputSystem.Gyroscope.current;

            if (_attitudeSensor != null)
                InputSystem.EnableDevice(_attitudeSensor);
            if (_gyroSensor != null)
                InputSystem.EnableDevice(_gyroSensor);
        }

        private void OnDisable()
        {
            if (_attitudeSensor != null)
                InputSystem.DisableDevice(_attitudeSensor);
            if (_gyroSensor != null)
                InputSystem.DisableDevice(_gyroSensor);    // UnityEngine.InputSystem.Gyroscope
        }

        /// <inheritdoc/>
        public Quaternion GetCurrentPose()
        {
            if (_attitudeSensor == null || !_attitudeSensor.enabled)
                return Quaternion.identity;

            // Unity InputSystem AttitudeSensor returns device-orientation quaternion.
            // Remap from iOS sensor frame (portrait, right-handed) to Unity camera frame.
            Quaternion q = _attitudeSensor.attitude.ReadValue();
            return RemapToUnityFrame(q);
        }

        /// <inheritdoc/>
        public Quaternion GetPredictedPose(float predictionSeconds)
        {
            Quaternion current = GetCurrentPose();
            Vector3 omega = GetAngularVelocity();
            return PosePrediction.Predict(current, omega, predictionSeconds);
        }

        // --- Private helpers -------------------------------------------------

        private Vector3 GetAngularVelocity()
        {
            if (_gyroSensor == null || !_gyroSensor.enabled)
                return Vector3.zero;
            // Gyroscope.angularVelocity is in rad/s, body frame
            return _gyroSensor.angularVelocity.ReadValue();
        }

        /// <summary>
        /// Remap iOS AttitudeSensor quaternion to Unity world-space camera orientation.
        /// iOS: X right, Y up, Z toward user (portrait). Unity: X right, Y up, Z forward.
        /// </summary>
        private static Quaternion RemapToUnityFrame(Quaternion q)
        {
            // Flip Z and W to convert right-handed → left-handed (Unity) convention
            return new Quaternion(-q.x, -q.y, q.z, q.w);
        }
    }
}
