using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Abstraction over IMU data sources.
    /// Swap PhoneIMUSource ↔ ExternalIMUSource without touching upper layers.
    /// </summary>
    public interface IIMUSource
    {
        /// <summary>Returns the latest measured orientation.</summary>
        Quaternion GetCurrentPose();

        /// <summary>
        /// Returns orientation predicted <paramref name="predictionSeconds"/> into the future
        /// using gyroscope angular velocity integration.
        /// </summary>
        Quaternion GetPredictedPose(float predictionSeconds);
    }
}
