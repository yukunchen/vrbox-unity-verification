/**
 * VideoTextureBridge.mm
 *
 * Phase 2: AVPlayer → CVPixelBuffer → MTLTexture (zero-copy) bridge for Unity.
 *
 * Supports both local file:// URLs (StreamingAssets) and remote http(s):// URLs.
 * Frames are delivered as BGRA MTLTexture pointers via CVMetalTextureCache —
 * no CPU memcpy, no format conversion on the CPU side.
 *
 * Unity C# side calls:
 *   VideoTextureBridge_StartSession(urlCStr)   — start/restart playback
 *   VideoTextureBridge_StopSession()            — stop and release all resources
 *   VideoTextureBridge_GetCurrentTexture()      — returns id<MTLTexture> as void*, NULL until first frame
 *   VideoTextureBridge_GetVideoWidth()          — pixel width of the decoded video
 *   VideoTextureBridge_GetVideoHeight()         — pixel height of the decoded video
 */

#import <AVFoundation/AVFoundation.h>
#import <Metal/Metal.h>
#import <CoreVideo/CoreVideo.h>

// Unity Metal device accessor (provided by the Unity iOS runtime)
extern "C" id<MTLDevice> UnityGetMetalDevice(void);

// ---------------------------------------------------------------------------
// Module-level state
// ---------------------------------------------------------------------------

static AVPlayer*                 g_player        = nil;
static AVPlayerItemVideoOutput*  g_output         = nil;
static CVMetalTextureCacheRef    g_textureCache   = NULL;
static CVMetalTextureRef         g_cvTexture      = NULL;
static id<MTLTexture>            g_mtlTexture     = nil;
static CVPixelBufferRef          g_pixelBuf       = NULL;
static int                       g_videoW         = 0;
static int                       g_videoH         = 0;
static id                        g_loopObserver   = nil;

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

static void ReleaseCurrentFrame(void)
{
    if (g_cvTexture) { CFRelease(g_cvTexture); g_cvTexture = NULL; }
    if (g_pixelBuf)  { CVPixelBufferRelease(g_pixelBuf); g_pixelBuf = NULL; }
    g_mtlTexture = nil;
}

static void DestroyTextureCache(void)
{
    ReleaseCurrentFrame();
    if (g_textureCache)
    {
        CVMetalTextureCacheFlush(g_textureCache, 0);
        CFRelease(g_textureCache);
        g_textureCache = NULL;
    }
}

// ---------------------------------------------------------------------------
// Public C API (called from C# via DllImport("__Internal"))
// ---------------------------------------------------------------------------

extern "C"
{

// Forward declaration so StartSession can call StopSession defined below
void VideoTextureBridge_StopSession(void);

void VideoTextureBridge_StartSession(const char* urlCStr)
{
    if (!urlCStr) return;

    // Stop any existing session first
    VideoTextureBridge_StopSession();

    // ── Metal texture cache ─────────────────────────────────────────────
    id<MTLDevice> device = UnityGetMetalDevice();
    if (!device) { NSLog(@"[VideoTextureBridge] Metal device unavailable"); return; }

    CVReturn cacheResult = CVMetalTextureCacheCreate(
        kCFAllocatorDefault, NULL, device, NULL, &g_textureCache);
    if (cacheResult != kCVReturnSuccess)
    {
        NSLog(@"[VideoTextureBridge] CVMetalTextureCacheCreate failed: %d", cacheResult);
        return;
    }

    // ── AVPlayerItemVideoOutput ─────────────────────────────────────────
    NSDictionary* outputAttrs = @{
        (NSString*)kCVPixelBufferPixelFormatTypeKey:    @(kCVPixelFormatType_32BGRA),
        (NSString*)kCVPixelBufferMetalCompatibilityKey: @YES,
    };
    g_output = [[AVPlayerItemVideoOutput alloc] initWithPixelBufferAttributes:outputAttrs];

    // ── AVPlayer ────────────────────────────────────────────────────────
    NSString* urlStr = [NSString stringWithUTF8String:urlCStr];
    NSURL*    url    = [NSURL URLWithString:urlStr];
    if (!url)
    {
        NSLog(@"[VideoTextureBridge] Invalid URL: %s", urlCStr);
        DestroyTextureCache();
        g_output = nil;
        return;
    }

    AVPlayerItem* item = [AVPlayerItem playerItemWithURL:url];
    [item addOutput:g_output];

    g_player = [AVPlayer playerWithPlayerItem:item];
    g_player.volume = 0.0f;   // VR typically mutes the internal speaker

    // Loop: seek to zero when playback reaches the end
    __weak AVPlayer* weakPlayer = g_player;
    g_loopObserver = [[NSNotificationCenter defaultCenter]
        addObserverForName:AVPlayerItemDidPlayToEndTimeNotification
                    object:item
                     queue:[NSOperationQueue mainQueue]
                usingBlock:^(NSNotification*) {
        AVPlayer* p = weakPlayer;
        if (p) {
            [p seekToTime:kCMTimeZero toleranceBefore:kCMTimeZero toleranceAfter:kCMTimeZero];
            [p play];
        }
    }];

    [g_player play];
    NSLog(@"[VideoTextureBridge] Session started: %s", urlCStr);
}

void VideoTextureBridge_StopSession(void)
{
    if (g_player) [g_player pause];

    if (g_loopObserver)
    {
        [[NSNotificationCenter defaultCenter] removeObserver:g_loopObserver];
        g_loopObserver = nil;
    }

    g_player = nil;
    g_output  = nil;

    DestroyTextureCache();
    g_videoW = g_videoH = 0;

    NSLog(@"[VideoTextureBridge] Session stopped.");
}

void* VideoTextureBridge_GetCurrentTexture(void)
{
    if (!g_output || !g_player || !g_textureCache) return NULL;

    CMTime presentationTime = [g_player currentTime];

    // If no new frame is available, return the previous texture (avoid rebuild cost)
    if (![g_output hasNewPixelBufferForItemTime:presentationTime])
        return g_mtlTexture ? (__bridge void*)g_mtlTexture : NULL;

    // Acquire the new pixel buffer
    CVPixelBufferRef newBuf =
        [g_output copyPixelBufferForItemTime:presentationTime itemTimeForDisplay:NULL];
    if (!newBuf)
        return g_mtlTexture ? (__bridge void*)g_mtlTexture : NULL;

    // Release previous frame resources before creating the new texture
    ReleaseCurrentFrame();
    g_pixelBuf = newBuf;   // retain; released in ReleaseCurrentFrame

    size_t w = CVPixelBufferGetWidth(newBuf);
    size_t h = CVPixelBufferGetHeight(newBuf);
    g_videoW  = (int)w;
    g_videoH  = (int)h;

    CVReturn result = CVMetalTextureCacheCreateTextureFromImage(
        kCFAllocatorDefault,
        g_textureCache,
        newBuf,
        NULL,                       // texture attributes
        MTLPixelFormatBGRA8Unorm,
        w, h,
        0,                          // plane index (0 for packed BGRA)
        &g_cvTexture);

    if (result != kCVReturnSuccess)
    {
        NSLog(@"[VideoTextureBridge] CVMetalTextureCacheCreateTextureFromImage failed: %d", result);
        return NULL;
    }

    g_mtlTexture = CVMetalTextureGetTexture(g_cvTexture);
    return (__bridge void*)g_mtlTexture;
}

int VideoTextureBridge_GetVideoWidth(void)  { return g_videoW; }
int VideoTextureBridge_GetVideoHeight(void) { return g_videoH; }

} // extern "C"
