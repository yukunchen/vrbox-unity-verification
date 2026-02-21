using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Pure-function Brown lens distortion model — no Unity component dependencies.
    ///
    ///   r' = r(1 + k1*r² + k2*r⁴)
    ///
    /// Input/output in NDC (Normalised Device Coordinates), range [-1, 1].
    /// </summary>
    public static class DistortionMath
    {
        /// <summary>
        /// Apply barrel distortion to an NDC point.
        /// Used to pre-warp rendered pixels so the lens un-distorts them back to straight lines.
        /// </summary>
        /// <param name="ndcPoint">Input 2-D NDC coordinate (x, y).</param>
        /// <param name="k1">First radial distortion coefficient.</param>
        /// <param name="k2">Second radial distortion coefficient.</param>
        /// <returns>Distorted NDC coordinate.</returns>
        public static Vector2 ApplyDistortion(Vector2 ndcPoint, float k1, float k2)
        {
            float r2 = ndcPoint.x * ndcPoint.x + ndcPoint.y * ndcPoint.y;
            float r4 = r2 * r2;
            float scale = 1f + k1 * r2 + k2 * r4;
            return ndcPoint * scale;
        }

        /// <summary>
        /// Inverse distortion: given a distorted NDC point, find the undistorted radius.
        /// Solved with 4 iterations of Newton-Raphson.
        /// </summary>
        public static Vector2 RemoveDistortion(Vector2 distortedNdc, float k1, float k2,
            int iterations = 4)
        {
            // Initial guess: identity
            Vector2 p = distortedNdc;
            float targetR = distortedNdc.magnitude;
            if (targetR < 1e-9f) return distortedNdc;

            for (int i = 0; i < iterations; i++)
            {
                float r2 = p.sqrMagnitude;
                float r4 = r2 * r2;
                float f  = p.magnitude * (1f + k1 * r2 + k2 * r4) - targetR;
                float df = 1f + 3f * k1 * r2 + 5f * k2 * r4;  // df/dr
                float correction = f / df;
                p = p.normalized * (p.magnitude - correction);
            }
            return p;
        }
    }
}
