# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VRBoxVerification is a Unity 2021.3.39f1c1 project implementing a mobile VR headset system (Phase 1-2) targeting iOS. It covers head tracking via phone IMU, stereo rendering, ATW (Asynchronous TimeWarp), lens distortion correction (Brown model), and 360° equirectangular video rendering.

## Build & Test Commands

All builds and tests go through the Unity Editor — there is no CLI build script currently.

**Run unit tests (EditMode):**
```
Window → General → Test Runner → EditMode → Run All
```

**Setup the Phase 1 scene from scratch:**
```
VRBox → Setup Phase 1 Scene   (creates VR rig, cameras, 360° sphere, reference cubes)
File → Save As → Assets/VRBox_Phase1.unity
```

**Add stereo rendering to an existing scene:**
```
VRBox → Add Stereo Cameras    (splits single camera into left/right viewports with IPD offset)
```

**Build for iOS:**
```
File → Build Settings → iOS → Build → open in Xcode → Product → Run on Device
```

## Architecture

### Core VR Pipeline (`Assets/VR/`)

The runtime is organized as a pipeline of pure functions plus MonoBehaviour wrappers:

- **Head Tracking**: `VRHeadTracking.cs` calls an `IIMUSource` each frame (OnPreRender). Two implementations: `PhoneIMUSource` (Unity InputSystem AttitudeSensor/Gyroscope) and `ExternalIMUSource` (native iOS plugin via DllImport). Strategy pattern — swappable in Inspector.

- **Pose Prediction**: `PosePrediction.cs` is a pure static class. First-order quaternion integration: `q_pred = q ⊗ exp(ω × Δt / 2)`. Zero Unity dependencies, fully unit-tested.

- **Stereo Rendering**: `StereoCameraRig.cs` configures left (viewport 0–0.5) and right (viewport 0.5–1) cameras. `StereoProjection` (nested pure class) builds off-axis frustum matrices with IPD shear. Right camera depth = left + 1.

- **ATW**: `ATWController.cs` posts a GL render event to the native plugin after each frame. `ATWMath.cs` computes `delta = q_now ⊗ q_render⁻¹`. Native plugin is currently a stub (returns NULL).

- **Lens Distortion**: `DistortionMath.cs` applies the Brown model `r' = r(1 + k1·r² + k2·r⁴)`. Inverse (RemoveDistortion) uses Newton-Raphson (4 iterations). Parameters come from `VRSettings` ScriptableObject.

- **Video**: `VideoTextureReceiver.cs` receives zero-copy MTLTexture frames from the native bridge each frame and attaches them to the 360° sphere renderer.

### Assembly Definitions

- `Assets/VR/VRBoxRuntime.asmdef` — runtime assembly, references InputSystem.
- `Assets/Tests/EditMode/EditModeTestAssembly.asmdef` — Editor-only tests, references VRBox.Runtime.

### Native iOS Stubs (`Assets/Plugins/iOS/VRBoxStubs.m`)

Three native plugin bridges, all currently stubbed for Phase 1-2 (return NULL / identity):
1. **ATWPlugin** — compute shader warp pass (Phase 8, not yet implemented)
2. **IMUBridge** — external MCU via EAAccessory/USB (Phase 7, MCU packet format TBD)
3. **VideoTextureBridge** — H.264/H.265 decoding to MTLTexture (Phase 2 stub)

All native calls in C# are guarded with `#if UNITY_IOS && !UNITY_EDITOR` so tests run in Editor without the plugin.

### VRSettings ScriptableObject

Created by the scene setup wizard, saved to `Assets/VR/VRSettings_Default.asset`. Fields: `k1` (0.2), `k2` (0.05), `ipd` (0.064 m), `fovDegrees` (90°), `displayLatencyMs` (4 ms).

### Shaders (`Assets/Shaders/`)

`EquirectangularShader.shader` — maps 360° equirectangular texture onto a sphere. `_Eye` property selects left (0) or right (1) half for stereo source. `_StereoMode`: 0=mono, 1=top-bottom, 2=side-by-side.

### Editor Tools (`Assets/Editor/`)

`VRBoxSceneSetup.cs` — "VRBox/Setup Phase 1 Scene" menu, one-click full scene creation.
`VRBoxStereoSetup.cs` — "VRBox/Add Stereo Cameras" menu.
`VRBoxTextureImporter.cs` — auto post-processor that configures texture import settings.

## Key Design Patterns

- **Pure functions for math**: `ATWMath`, `DistortionMath`, `PosePrediction`, `StereoProjection` have no Unity dependencies and are directly unit-testable with NUnit.
- **Strategy / Dependency Injection**: `IIMUSource` and `IVideoTextureProvider` interfaces allow swapping real vs. mock implementations.
- **Conditional compilation**: `#if UNITY_IOS && !UNITY_EDITOR` gates all DllImport calls so the Editor can run without native libs.
- **Namespace**: All runtime code lives in namespace `VRBox`.

## Package Dependencies

Key packages (`Packages/manifest.json`):
- `com.unity.inputsystem` 1.7.0
- `com.unity.test-framework` 1.1.33
- `com.unity.textmeshpro` 3.0.9
