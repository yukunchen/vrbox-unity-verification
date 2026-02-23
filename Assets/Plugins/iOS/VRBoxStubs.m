/**
 * VRBoxStubs.m
 *
 * Phase 1-2 stub implementations.
 * Provides empty symbols so Xcode links successfully without the real
 * ATWPlugin / IMUBridge native plugins.
 * VideoTextureBridge has been implemented in VideoTextureBridge.mm (Phase 2).
 *
 * Replace individual stubs with real .mm files as each phase is implemented.
 */

#include <stdint.h>

// ---------------------------------------------------------------------------
// ATWPlugin stubs  (Phase 8 — not needed yet)
// ---------------------------------------------------------------------------

typedef void (*UnityRenderingEvent)(int);

UnityRenderingEvent ATWPlugin_GetRenderEventFunc(void) { return NULL; }
void ATWPlugin_SetRenderPose(float x, float y, float z, float w) {}
void ATWPlugin_SetEyeTexture(void* ptr) {}
void ATWPlugin_SetUITexture(void* ptr)  {}

// ---------------------------------------------------------------------------
// IMUBridge stubs  (Phase 7 — external IMU, not needed for phone-IMU path)
// ---------------------------------------------------------------------------

void IMUBridge_Start(void) {}
void IMUBridge_Stop(void)  {}

void IMUBridge_GetCurrentQuaternion(float* x, float* y, float* z, float* w)
{
    if (x) *x = 0.f;
    if (y) *y = 0.f;
    if (z) *z = 0.f;
    if (w) *w = 1.f;
}

void IMUBridge_GetPredictedQuaternion(double dt,
                                      float* x, float* y, float* z, float* w)
{
    if (x) *x = 0.f;
    if (y) *y = 0.f;
    if (z) *z = 0.f;
    if (w) *w = 1.f;
}

// VideoTextureBridge stubs removed — real implementation in VideoTextureBridge.mm
