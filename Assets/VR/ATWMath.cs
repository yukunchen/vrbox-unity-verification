using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Pure-function ATW (Asynchronous TimeWarp) delta quaternion calculation.
    /// No Unity component dependencies.
    ///
    /// Computes the rotation that corrects for head motion that occurred
    /// between when the frame was rendered and when it is displayed.
    ///
    ///   delta = q_now ⊗ q_render⁻¹
    /// </summary>
    public static class ATWMath
    {
        /// <summary>
        /// Compute the correction rotation between render-time pose and current pose.
        /// Apply this delta to each pixel direction to warp the rendered image.
        /// </summary>
        /// <param name="qNow">Current IMU orientation at display time.</param>
        /// <param name="qRender">Orientation that was used when the frame was rendered.</param>
        /// <returns>Delta rotation to apply in the ATW warp pass.</returns>
        public static Quaternion ComputeDelta(Quaternion qNow, Quaternion qRender)
        {
            // delta = q_now ⊗ q_render⁻¹
            return qNow * Quaternion.Inverse(qRender);
        }

        /// <summary>
        /// Rotate a view direction vector by the ATW delta rotation.
        /// Used per-pixel in the warp compute shader (CPU-side equivalent for testing).
        /// </summary>
        public static Vector3 WarpDirection(Vector3 direction, Quaternion delta)
        {
            return delta * direction;
        }
    }
}
