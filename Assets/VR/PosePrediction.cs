using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Pure-function pose prediction — no Unity component dependencies.
    /// Predicts future orientation using first-order quaternion integration.
    ///
    ///   q_pred = q ⊗ exp(ω × Δt / 2)
    ///
    /// where exp(v) = (cos|v|, sin|v| * v/|v|) for quaternion exponential.
    /// </summary>
    public static class PosePrediction
    {
        /// <summary>
        /// Predict orientation after <paramref name="deltaSeconds"/> given current pose and
        /// angular velocity in body frame (rad/s).
        /// </summary>
        public static Quaternion Predict(Quaternion currentPose, Vector3 omega, float deltaSeconds)
        {
            // Half-angle integration
            Vector3 halfAngle = omega * (deltaSeconds * 0.5f);
            float magnitude = halfAngle.magnitude;

            Quaternion dq;
            if (magnitude < 1e-6f)
            {
                // Small angle: use linear approximation to avoid division by zero
                dq = new Quaternion(halfAngle.x, halfAngle.y, halfAngle.z, 1f);
                // Normalize to correct floating-point accumulation
                dq = Normalize(dq);
            }
            else
            {
                float sinM = Mathf.Sin(magnitude);
                float cosM = Mathf.Cos(magnitude);
                dq = new Quaternion(
                    halfAngle.x / magnitude * sinM,
                    halfAngle.y / magnitude * sinM,
                    halfAngle.z / magnitude * sinM,
                    cosM);
            }

            return Normalize(currentPose * dq);
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-9f) return Quaternion.identity;
            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }
    }
}
