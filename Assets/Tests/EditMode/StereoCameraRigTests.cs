using NUnit.Framework;
using UnityEngine;
using VRBox;

namespace VRBox.Tests.EditMode
{
    /// <summary>
    /// Unit tests for StereoProjection pure-function class.
    /// </summary>
    public class StereoCameraRigTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void BuildProjectionMatrix_ZeroEyeOffset_IsSymmetric()
        {
            Matrix4x4 m = StereoProjection.BuildProjectionMatrix(
                fovDegrees: 90f, aspect: 1f, near: 0.1f, far: 1000f, eyeOffset: 0f);

            // For symmetric frustum: m[0,2] should be 0
            Assert.AreEqual(0f, m[0, 2], Tol, "Zero eye offset should produce symmetric frustum.");
        }

        [Test]
        public void BuildProjectionMatrix_LeftAndRight_AreHorizontalMirrors()
        {
            float fov = 90f, aspect = 1.5f, near = 0.1f, far = 1000f, halfIpd = 0.032f;

            Matrix4x4 left  = StereoProjection.BuildProjectionMatrix(fov, aspect, near, far,  halfIpd);
            Matrix4x4 right = StereoProjection.BuildProjectionMatrix(fov, aspect, near, far, -halfIpd);

            // The shear term (column 2, row 0) should flip sign between eyes
            Assert.AreEqual(-left[0, 2], right[0, 2], Tol,
                "Left/right projection shear should be equal and opposite.");
        }

        [Test]
        public void BuildProjectionMatrix_FovAffectsScale()
        {
            Matrix4x4 wide   = StereoProjection.BuildProjectionMatrix(120f, 1f, 0.1f, 1000f, 0f);
            Matrix4x4 narrow = StereoProjection.BuildProjectionMatrix( 60f, 1f, 0.1f, 1000f, 0f);

            // Wider FOV → smaller projection scale (m[1,1] smaller)
            Assert.Less(wide[1, 1], narrow[1, 1],
                "Wider FOV should produce smaller projection Y scale.");
        }

        [Test]
        public void BuildProjectionMatrix_AspectAffectsXScale()
        {
            Matrix4x4 wide   = StereoProjection.BuildProjectionMatrix(90f, 2f, 0.1f, 1000f, 0f);
            Matrix4x4 square = StereoProjection.BuildProjectionMatrix(90f, 1f, 0.1f, 1000f, 0f);

            // Wider aspect → smaller X scale (m[0,0] smaller)
            Assert.Less(wide[0, 0], square[0, 0],
                "Wider aspect ratio should reduce horizontal projection scale.");
        }

        [Test]
        public void BuildProjectionMatrix_NearFarAffectDepthRange()
        {
            Matrix4x4 m1 = StereoProjection.BuildProjectionMatrix(90f, 1f, 0.1f,  100f, 0f);
            Matrix4x4 m2 = StereoProjection.BuildProjectionMatrix(90f, 1f, 0.1f, 1000f, 0f);

            // Far plane affects m[2,2] (depth range encoding)
            Assert.AreNotEqual(m1[2, 2], m2[2, 2],
                "Different far planes should produce different depth encoding.");
        }
    }
}
