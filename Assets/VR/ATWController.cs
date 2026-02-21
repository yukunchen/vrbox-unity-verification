using System.Runtime.InteropServices;
using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Triggers the ATW (Asynchronous TimeWarp) warp pass after rendering completes.
    ///
    /// OnPostRender: issues a GL plugin event that signals ATWPlugin.mm to:
    ///   1. Read current IMU pose (qNow)
    ///   2. Compute delta = qNow × renderPose⁻¹
    ///   3. Run Compute Shader warp on eyeRT
    ///   4. Present the warped result before display
    ///
    /// Attach to the main VR camera (or camera rig root with a Camera component).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ATWController : MonoBehaviour
    {
        private const int ATW_EVENT_ID = 1;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern System.IntPtr ATWPlugin_GetRenderEventFunc();

        [DllImport("__Internal")]
        private static extern void ATWPlugin_SetRenderPose(
            float qx, float qy, float qz, float qw);
#endif

        private void OnPostRender()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Pass the render-time pose to native before issuing the event
            Quaternion rp = VRHeadTracking.RenderPose;
            ATWPlugin_SetRenderPose(rp.x, rp.y, rp.z, rp.w);

            // Queue the native warp event into the Unity render loop
            // Null-check: stub returns NULL during Phase 1-7
            System.IntPtr funcPtr = ATWPlugin_GetRenderEventFunc();
            if (funcPtr != System.IntPtr.Zero)
                GL.IssuePluginEvent(funcPtr, ATW_EVENT_ID);
#endif
        }
    }
}
