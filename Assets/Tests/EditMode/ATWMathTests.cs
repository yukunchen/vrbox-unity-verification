using NUnit.Framework;
using UnityEngine;
using VRBox;

namespace VRBox.Tests.EditMode
{
    /// <summary>
    /// Unit tests for ATWMath pure-function class.
    /// </summary>
    public class ATWMathTests
    {
        private const float Tol = 1e-4f;

        // --- ComputeDelta -----------------------------------------------------

        [Test]
        public void ComputeDelta_SamePose_ReturnsIdentity()
        {
            Quaternion q = Quaternion.Euler(30f, 45f, 0f);
            Quaternion delta = ATWMath.ComputeDelta(q, q);
            AssertQuatApproxIdentity(delta);
        }

        [Test]
        public void ComputeDelta_NoPoseChange_ReturnsIdentity()
        {
            Quaternion delta = ATWMath.ComputeDelta(Quaternion.identity, Quaternion.identity);
            AssertQuatApproxIdentity(delta);
        }

        [Test]
        public void ComputeDelta_KnownRotation_ReturnsCorrectDelta()
        {
            // Render at 0°, now at 10° yaw → delta should be 10° yaw
            Quaternion qRender = Quaternion.identity;
            Quaternion qNow    = Quaternion.Euler(0f, 10f, 0f);
            Quaternion delta   = ATWMath.ComputeDelta(qNow, qRender);

            // Apply delta to forward vector
            Vector3 forward  = Vector3.forward;
            Vector3 warped   = ATWMath.WarpDirection(forward, delta);

            // Expect ≈ 10° clockwise yaw: x-component should be non-zero positive
            Assert.Greater(warped.x, 0f, "Expected positive X after 10° yaw warp.");
            Assert.AreEqual(0f, warped.y, Tol, "Y should be unaffected by yaw.");
        }

        [Test]
        public void ComputeDelta_IsAntiCommutative()
        {
            Quaternion q1 = Quaternion.Euler(10f, 20f, 5f);
            Quaternion q2 = Quaternion.Euler(15f, 25f, 10f);

            Quaternion d12 = ATWMath.ComputeDelta(q1, q2);
            Quaternion d21 = ATWMath.ComputeDelta(q2, q1);

            // d12 and d21 should be inverses of each other
            Quaternion combined = d12 * d21;
            AssertQuatApproxIdentity(combined);
        }

        // --- WarpDirection ----------------------------------------------------

        [Test]
        public void WarpDirection_IdentityDelta_ReturnsUnchanged()
        {
            Vector3 dir    = new Vector3(0.5f, 0.2f, 0.8f).normalized;
            Vector3 warped = ATWMath.WarpDirection(dir, Quaternion.identity);
            Assert.AreEqual(dir.x, warped.x, Tol);
            Assert.AreEqual(dir.y, warped.y, Tol);
            Assert.AreEqual(dir.z, warped.z, Tol);
        }

        [Test]
        public void WarpDirection_PreservesVectorMagnitude()
        {
            Vector3 dir    = new Vector3(1f, 2f, 3f).normalized;
            Quaternion delta = Quaternion.Euler(15f, 30f, 5f);
            Vector3 warped = ATWMath.WarpDirection(dir, delta);
            Assert.AreEqual(1f, warped.magnitude, Tol, "Unit vector magnitude must be preserved.");
        }

        // --- Helper -----------------------------------------------------------

        private static void AssertQuatApproxIdentity(Quaternion q, float tol = 1e-4f)
        {
            // |dot(q, identity)| ≈ 1 means they represent the same rotation
            Quaternion id = Quaternion.identity;
            float dot = Mathf.Abs(q.x * id.x + q.y * id.y + q.z * id.z + q.w * id.w);
            Assert.AreEqual(1f, dot, tol, $"Expected identity quaternion, got: {q}");
        }
    }
}
