using NUnit.Framework;
using UnityEngine;
using VRBox;

namespace VRBox.Tests.EditMode
{
    /// <summary>
    /// Unit tests for PosePrediction pure-function class.
    /// Run in Unity Edit Mode (no MonoBehaviour / scene required).
    /// </summary>
    public class PosePredictionTests
    {
        private const float Tolerance = 1e-4f;

        // --- Identity cases ---------------------------------------------------

        [Test]
        public void Predict_ZeroOmega_ReturnsUnchangedPose()
        {
            Quaternion q = Quaternion.Euler(30f, 45f, 0f);
            Quaternion result = PosePrediction.Predict(q, Vector3.zero, 0.02f);
            AssertQuatApproxEqual(q, result);
        }

        [Test]
        public void Predict_ZeroDeltaT_ReturnsUnchangedPose()
        {
            Quaternion q = Quaternion.Euler(10f, 20f, 0f);
            Vector3 omega = new Vector3(0.1f, 0.2f, 0.3f);
            Quaternion result = PosePrediction.Predict(q, omega, 0f);
            AssertQuatApproxEqual(q, result);
        }

        [Test]
        public void Predict_IdentityPose_ZeroOmega_ReturnsIdentity()
        {
            Quaternion result = PosePrediction.Predict(Quaternion.identity, Vector3.zero, 0.016f);
            AssertQuatApproxEqual(Quaternion.identity, result);
        }

        // --- Correctness cases ------------------------------------------------

        [Test]
        public void Predict_PureYawRotation_ProducesExpectedYawIncrement()
        {
            // Rotate around Y axis at π rad/s for 100 ms → should rotate 18 degrees
            float omegaY = Mathf.PI;           // rad/s
            float dt     = 0.1f;               // seconds
            float expected = omegaY * dt * Mathf.Rad2Deg;  // 18°

            Quaternion result = PosePrediction.Predict(Quaternion.identity,
                new Vector3(0f, omegaY, 0f), dt);

            // Extract yaw from result
            float yaw = result.eulerAngles.y;
            if (yaw > 180f) yaw -= 360f;

            Assert.AreEqual(expected, yaw, 1f,  // 1° tolerance for float precision
                $"Expected yaw ≈ {expected}° but got {yaw}°");
        }

        [Test]
        public void Predict_OutputIsNormalisedQuaternion()
        {
            Quaternion q = Quaternion.Euler(45f, 30f, 15f);
            Vector3 omega = new Vector3(1f, 2f, 3f);

            Quaternion result = PosePrediction.Predict(q, omega, 0.05f);
            float magnitude = Mathf.Sqrt(result.x * result.x + result.y * result.y
                                       + result.z * result.z + result.w * result.w);

            Assert.AreEqual(1f, magnitude, Tolerance, "Result quaternion must be unit-length.");
        }

        [Test]
        public void Predict_MultipleSmallSteps_ApproximatesSingleLargeStep()
        {
            // One large step vs. accumulated small steps should be close
            Quaternion q0 = Quaternion.identity;
            Vector3 omega = new Vector3(0f, 1f, 0f);  // 1 rad/s yaw
            float totalDt = 0.2f;
            int steps = 10;
            float stepDt = totalDt / steps;

            // Large step
            Quaternion large = PosePrediction.Predict(q0, omega, totalDt);

            // Accumulated small steps
            Quaternion accum = q0;
            for (int i = 0; i < steps; i++)
                accum = PosePrediction.Predict(accum, omega, stepDt);

            AssertQuatApproxEqual(large, accum, 1e-3f);
        }

        // --- Helper -----------------------------------------------------------

        private static void AssertQuatApproxEqual(Quaternion expected, Quaternion actual,
            float tol = 1e-4f)
        {
            // Quaternions q and -q represent the same rotation
            float dot = Mathf.Abs(expected.x * actual.x + expected.y * actual.y
                                + expected.z * actual.z + expected.w * actual.w);
            Assert.AreEqual(1f, dot, tol,
                $"Quaternions not approximately equal.\n  expected: {expected}\n  actual:   {actual}");
        }
    }
}
