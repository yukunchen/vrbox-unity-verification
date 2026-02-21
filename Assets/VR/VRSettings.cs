using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Lens and display parameters — edit in Inspector, serialized as ScriptableObject asset.
    /// </summary>
    [CreateAssetMenu(menuName = "VRBox/VRSettings", fileName = "VRSettings")]
    public class VRSettings : ScriptableObject
    {
        [Header("Lens Distortion (Brown model)")]
        [Tooltip("Radial distortion coefficient k1")]
        public float k1 = 0.2f;

        [Tooltip("Radial distortion coefficient k2")]
        public float k2 = 0.05f;

        [Header("Stereo")]
        [Tooltip("Inter-pupillary distance in meters")]
        public float ipd = 0.064f;

        [Tooltip("Horizontal field-of-view in degrees (per eye)")]
        public float fovDegrees = 90f;

        [Header("Timing")]
        [Tooltip("Display pipeline latency in milliseconds (used for pose prediction)")]
        public float displayLatencyMs = 4f;
    }
}
