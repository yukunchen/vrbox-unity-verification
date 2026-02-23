using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VRBox
{
    /// <summary>
    /// Abstraction for the video texture provider — enables mocking in tests.
    /// </summary>
    public interface IVideoTextureProvider
    {
        /// <summary>Returns the latest decoded video frame as a Texture.</summary>
        Texture GetCurrentTexture();
    }

    /// <summary>
    /// Receives video frames from the native VideoTextureBridge (zero-copy via MTLTexture).
    /// Attaches the texture to the 360° sphere renderer each frame.
    ///
    /// Attach to the 360° sphere GameObject alongside a Renderer component.
    ///
    /// Video URL examples:
    ///   Local StreamingAssets:  "file://" + Application.streamingAssetsPath + "/video360.mp4"
    ///   Remote HTTP:            "https://example.com/video360.mp4"
    /// </summary>
    public class VideoTextureReceiver : MonoBehaviour
    {
        [Tooltip("Material property name for the main video texture.")]
        [SerializeField] private string texturePropertyName = "_MainTex";

        [Tooltip("Video URL to play. Supports file:// (StreamingAssets) and http(s)://.\n" +
                 "Leave empty to skip native playback. Prefix 'streaming:' to resolve from StreamingAssets automatically.")]
        [SerializeField] private string videoUrl = "";

        [Tooltip("Inject a mock provider in tests; leave null to use native bridge.")]
        [SerializeField] private MonoBehaviour videoProviderOverride;

        private IVideoTextureProvider _provider;
        private NativeVideoTextureProvider _nativeProvider;
        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            if (videoProviderOverride != null)
            {
                _provider = videoProviderOverride as IVideoTextureProvider;
            }
            else
            {
                string resolvedUrl = ResolveUrl(videoUrl);
                _nativeProvider = new NativeVideoTextureProvider();
                _provider = _nativeProvider;
                if (!string.IsNullOrEmpty(resolvedUrl))
                    _nativeProvider.StartSession(resolvedUrl);
            }
        }

        private void Update()
        {
            if (_provider == null || _renderer == null) return;

            Texture tex = _provider.GetCurrentTexture();
            if (tex != null)
                _renderer.material.SetTexture(texturePropertyName, tex);
        }

        private void OnDestroy()
        {
            _nativeProvider?.StopSession();
        }

        // Convenience: prefix "streaming:" auto-resolves to StreamingAssets file:// URL
        private static string ResolveUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("streaming:"))
            {
                string filename = url.Substring("streaming:".Length);
                return "file://" + Application.streamingAssetsPath + "/" + filename;
            }
            return url;
        }
    }

    // -------------------------------------------------------------------------
    // Native bridge implementation (iOS only)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calls VideoTextureBridge.mm to get the latest CVPixelBuffer-backed MTLTexture (zero-copy).
    /// </summary>
    internal sealed class NativeVideoTextureProvider : IVideoTextureProvider
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void VideoTextureBridge_StartSession(string url);

        [DllImport("__Internal")]
        private static extern void VideoTextureBridge_StopSession();

        [DllImport("__Internal")]
        private static extern IntPtr VideoTextureBridge_GetCurrentTexture();

        [DllImport("__Internal")]
        private static extern int VideoTextureBridge_GetVideoWidth();

        [DllImport("__Internal")]
        private static extern int VideoTextureBridge_GetVideoHeight();
#endif

        private Texture2D _tex;

        public void StartSession(string url)
        {
#if UNITY_IOS && !UNITY_EDITOR
            VideoTextureBridge_StartSession(url);
#endif
        }

        public void StopSession()
        {
#if UNITY_IOS && !UNITY_EDITOR
            VideoTextureBridge_StopSession();
#endif
        }

        public Texture GetCurrentTexture()
        {
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr nativePtr = VideoTextureBridge_GetCurrentTexture();
            if (nativePtr == IntPtr.Zero) return null;

            if (_tex == null)
            {
                int w = VideoTextureBridge_GetVideoWidth();
                int h = VideoTextureBridge_GetVideoHeight();
                if (w == 0 || h == 0) return null;   // first frame not decoded yet

                _tex = Texture2D.CreateExternalTexture(w, h,
                    TextureFormat.BGRA32, false, false, nativePtr);
            }
            else
            {
                _tex.UpdateExternalTexture(nativePtr);
            }
            return _tex;
#else
            return null;
#endif
        }
    }
}
