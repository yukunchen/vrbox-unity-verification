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
    /// </summary>
    public class VideoTextureReceiver : MonoBehaviour
    {
        [Tooltip("Material property name for the main video texture.")]
        [SerializeField] private string texturePropertyName = "_MainTex";

        [Tooltip("Inject a mock provider in tests; leave null to use native bridge.")]
        [SerializeField] private MonoBehaviour videoProviderOverride;

        private IVideoTextureProvider _provider;
        private Renderer _renderer;
        private Texture2D _externalTex;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            if (videoProviderOverride != null)
            {
                _provider = videoProviderOverride as IVideoTextureProvider;
            }
            else
            {
                _provider = new NativeVideoTextureProvider();
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
            if (_externalTex != null)
                Destroy(_externalTex);
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
        private static extern IntPtr VideoTextureBridge_GetCurrentTexture();
#endif

        private Texture2D _tex;

        public Texture GetCurrentTexture()
        {
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr nativePtr = VideoTextureBridge_GetCurrentTexture();
            if (nativePtr == IntPtr.Zero) return null;

            if (_tex == null)
            {
                // Dimensions will be updated by the external texture; start with 1x1
                _tex = Texture2D.CreateExternalTexture(1, 1,
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
