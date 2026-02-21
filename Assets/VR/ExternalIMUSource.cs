using System.Runtime.InteropServices;
using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// IMU source that reads from an external VR box MCU via USB (EAAccessory).
    /// Bridges to IMUBridge.mm native plugin.
    ///
    /// NOTE: MCU data packet format is TBD. The native plugin must be updated once
    /// the packet format (quaternion + angular velocity + timestamp) is finalised.
    /// </summary>
    public class ExternalIMUSource : MonoBehaviour, IIMUSource
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void IMUBridge_Start();

        [DllImport("__Internal")]
        private static extern void IMUBridge_Stop();

        [DllImport("__Internal")]
        private static extern void IMUBridge_GetCurrentQuaternion(
            out float x, out float y, out float z, out float w);

        [DllImport("__Internal")]
        private static extern void IMUBridge_GetPredictedQuaternion(
            double predictionSeconds,
            out float x, out float y, out float z, out float w);
#endif

        private void OnEnable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            IMUBridge_Start();
#endif
        }

        private void OnDisable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            IMUBridge_Stop();
#endif
        }

        /// <inheritdoc/>
        public Quaternion GetCurrentPose()
        {
#if UNITY_IOS && !UNITY_EDITOR
            IMUBridge_GetCurrentQuaternion(out float x, out float y, out float z, out float w);
            return new Quaternion(x, y, z, w);
#else
            return Quaternion.identity;
#endif
        }

        /// <inheritdoc/>
        public Quaternion GetPredictedPose(float predictionSeconds)
        {
#if UNITY_IOS && !UNITY_EDITOR
            IMUBridge_GetPredictedQuaternion(predictionSeconds,
                out float x, out float y, out float z, out float w);
            return new Quaternion(x, y, z, w);
#else
            return Quaternion.identity;
#endif
        }
    }
}
