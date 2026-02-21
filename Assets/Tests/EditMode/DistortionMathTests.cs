using NUnit.Framework;
using UnityEngine;
using VRBox;

namespace VRBox.Tests.EditMode
{
    /// <summary>
    /// Unit tests for DistortionMath pure-function class.
    /// </summary>
    public class DistortionMathTests
    {
        private const float Tol = 1e-4f;

        // --- ApplyDistortion --------------------------------------------------

        [Test]
        public void ApplyDistortion_Origin_ReturnsOrigin()
        {
            Vector2 result = DistortionMath.ApplyDistortion(Vector2.zero, 0.2f, 0.05f);
            Assert.AreEqual(0f, result.x, Tol);
            Assert.AreEqual(0f, result.y, Tol);
        }

        [Test]
        public void ApplyDistortion_ZeroCoefficients_ReturnsUnchanged()
        {
            Vector2 p = new Vector2(0.5f, 0.3f);
            Vector2 result = DistortionMath.ApplyDistortion(p, 0f, 0f);
            Assert.AreEqual(p.x, result.x, Tol);
            Assert.AreEqual(p.y, result.y, Tol);
        }

        [Test]
        public void ApplyDistortion_PositiveK1_ExpandsPoint()
        {
            // Positive k1 → barrel expansion (r' > r for r > 0)
            Vector2 p      = new Vector2(0.5f, 0f);
            Vector2 result = DistortionMath.ApplyDistortion(p, 0.2f, 0f);
            Assert.Greater(result.x, p.x, "Positive k1 should expand the point radially.");
        }

        [Test]
        public void ApplyDistortion_DirectionPreserved()
        {
            // Distortion should not change the direction of a point, only its magnitude
            Vector2 p      = new Vector2(0.3f, 0.4f);
            Vector2 result = DistortionMath.ApplyDistortion(p, 0.2f, 0.05f);
            Vector2 dirIn  = p.normalized;
            Vector2 dirOut = result.normalized;
            Assert.AreEqual(dirIn.x, dirOut.x, Tol, "Direction X should be preserved.");
            Assert.AreEqual(dirIn.y, dirOut.y, Tol, "Direction Y should be preserved.");
        }

        // --- RemoveDistortion (inverse) ---------------------------------------

        [Test]
        public void RemoveDistortion_RoundTrip_RecoverOriginalPoint()
        {
            Vector2 original  = new Vector2(0.4f, 0.3f);
            float k1 = 0.2f, k2 = 0.05f;

            Vector2 distorted = DistortionMath.ApplyDistortion(original, k1, k2);
            Vector2 recovered = DistortionMath.RemoveDistortion(distorted, k1, k2);

            Assert.AreEqual(original.x, recovered.x, 1e-3f,
                $"Round-trip X failed: {original.x} → {distorted.x} → {recovered.x}");
            Assert.AreEqual(original.y, recovered.y, 1e-3f,
                $"Round-trip Y failed: {original.y} → {distorted.y} → {recovered.y}");
        }

        [Test]
        public void RemoveDistortion_Origin_ReturnsOrigin()
        {
            Vector2 result = DistortionMath.RemoveDistortion(Vector2.zero, 0.2f, 0.05f);
            Assert.AreEqual(0f, result.x, Tol);
            Assert.AreEqual(0f, result.y, Tol);
        }
    }
}
