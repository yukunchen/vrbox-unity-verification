# VRBoxVerification 项目进展

## 整体架构

Unity 2021.3.39f1c1 / iOS / Metal。实现手机 VR 头显的核心渲染管线：

```
PhoneIMUSource → VRHeadTracking → StereoCameraRig
                                       ↓ (左右各半屏)
                               EyeDistortionBlit (OnRenderImage)
                                       ↓
                           LensDistortionBlit.shader
                           1. 圆形镜片光圈遮罩（黑色圆外）
                           2. Brown 桶形畸变预校正（Newton-Raphson）
```

---

## Phase 完成状态

| Phase | 内容 | 状态 | 验证方式 |
|-------|------|------|---------|
| 1 | IMU 头部追踪 + 姿态预测 + 双目立体渲染 | ✅ 完成 | 设备运行 |
| 2 | VideoTextureBridge (AVPlayer→CVMetal 零拷贝) | ✅ 完成 | 设备运行，NYC 360°视频正常播放 |
| 3 | 镜头畸变后处理 Pass (圆形遮罩 + 桶形预校正) | ✅ 完成 | 设备运行 |
| 7 | IMUBridge (外部 MCU via EAAccessory) | ⏳ 存根 | MCU 协议 TBD |
| 8 | ATWPlugin (Metal compute shader warp) | ⏳ 存根 | — |

---

## Phase 3 实现细节

### 关键文件
- `Assets/Shaders/LensDistortionBlit.shader` — 单 Pass fragment blit shader
- `Assets/VR/EyeDistortionBlit.cs` — MonoBehaviour，挂在左右相机上
- `Assets/VR/LensDistortionBlit_Mat.mat` — 运行场景设置向导时自动生成

### shader 参数
| 参数 | 来源 | 说明 |
|------|------|------|
| `_K1` | VRSettings.k1（运行时设置） | Brown 模型径向畸变系数 |
| `_K2` | VRSettings.k2（运行时设置） | Brown 模型高阶系数 |
| `_LensRadius` | Material 默认 0.9 | 镜片圆形半径（NDC 垂直单位） |
| `_EyeAspect` | `cam.pixelWidth/cam.pixelHeight`（每帧计算） | 眼睛视口宽高比，使圆形在屏幕上是正圆 |

### 生产 vs 测试值
- **测试**：k1=1.0，k2=0.0（Setup 向导写入 VRSettings，视觉效果极明显）
- **生产**：k1=0.2，k2=0.05（在 Inspector 手动修改 `Assets/VR/VRSettings_Default.asset`）

---

## 踩坑记录

### 坑 1：Unity iOS 构建会剥离 `[SerializeField] Shader` 字段
**现象**：`EyeDistortionBlit` 在 Editor 中 Inspector 显示 shader 已引用，但设备上 `distortionShader == null`，`_material` 为 null，`OnRenderImage` 退化为透传，零畸变。

**根本原因**：Unity 的 iOS build 只通过 **Material 资源** 追踪 shader 依赖。直接用 `[SerializeField] Shader` 字段引用的 shader 可能被构建系统剥离（不保证包含在 build 中）。

**修复**：
1. `EyeDistortionBlit.cs` 改为 `[SerializeField] Material distortionMaterial`
2. `VRBoxSceneSetup.cs` 创建并保存 `Assets/VR/LensDistortionBlit_Mat.mat` 材质资源
3. 引用材质资源而非 shader 资源 → Unity 构建系统必然包含该 shader

**规则**：Unity iOS 项目中，**shader 必须通过 Material 资源间接引用**，不要直接序列化 Shader 字段。

---

### 坑 2：k1=0.2 的桶形畸变在 360° 视频中视觉上不明显
**现象**：shader 实际已在运行，但截图看起来"没有畸变"，边界仍是矩形。

**根本原因**（数学推导）：
- Brown 模型的反变换（Undistort）在 k1=0.2 时，所有输出像素的 srcUV 仍在 [0,1] 内，**不产生黑色边框**
- 边缘位移量仅约 7%（k1=0.2 时 NDC 边缘处 srcUV 从 1.0 变为 0.928）
- 360° 球面投影的内容本身已有透视形变，额外 7% 的内容扭曲肉眼难以察觉

**黑色圆形边框的正确来源**：不是畸变参数，而是**镜片光圈遮罩**（圆外像素直接返回黑色）。
即附件图中的"VR 桶形畸变"外观，本质是：镜片光圈遮罩 + 畸变内容 + 黑色四角，三者共同呈现。

**修复**：在 shader 中添加圆形遮罩：
```glsl
float2 circleCoord = float2(ndc.x * _EyeAspect, ndc.y);
if (dot(circleCoord, circleCoord) > _LensRadius * _LensRadius)
    return fixed4(0, 0, 0, 1);
```

**调试方法**：将 k1 临时调大到 1.0（边缘位移 ~29%），效果明显可见，确认 shader 运行后再调回生产值。

---

### 坑 3：CVMetal API 命名 —— 前缀是 `CVMetal`，不是 `CVMTL`
**现象**：Xcode 编译报错，`CVMTLTextureCacheRef`、`CVMTLTextureCacheCreate` 等类型/函数未定义。

**根本原因**：CoreVideo Metal API 的正确前缀是 `CVMetal`，非 `CVMTL`。

**正确 API**：
```objc
CVMetalTextureCacheRef
CVMetalTextureRef
CVMetalTextureCacheCreate(...)
CVMetalTextureCacheCreateTextureFromImage(...)
CVMetalTextureGetTexture(...)
CVMetalTextureCacheFlush(...)
```

---

### 坑 4：`extern "C"` 块内前向调用需要提前声明
**现象**：`StartSession` 调用 `StopSession`，但 `StopSession` 定义在后面，Xcode 报"use of undeclared identifier"。

**修复**：在 `extern "C"` 块顶部加前向声明：
```objc
extern "C" {
    void VideoTextureBridge_StopSession(void);  // forward declaration

    void VideoTextureBridge_StartSession(const char* url) {
        VideoTextureBridge_StopSession();  // can now call it
        ...
    }
    void VideoTextureBridge_StopSession(void) { ... }
}
```

---

### 坑 5：VRBoxSceneSetup 重新运行后视频消失
**现象**：重新运行 "VRBox → Setup Phase 1 Scene" 后，360° 球体上的 VideoTextureReceiver 组件丢失。

**根本原因**：之前的 Setup 向导代码只创建了球体并附加 Equirectangular shader 材质，**没有**同时添加 `VideoTextureReceiver` 组件。

**修复**：在 Setup 向导的球体创建段末尾添加：
```csharp
var videoReceiver = sphere.AddComponent<VideoTextureReceiver>();
SetField(videoReceiver, "videoUrl", "streaming:video360.mp4");
```

---

### 坑 6：每次 Unity iOS Build 后 Xcode 需要重新选 Provisioning Profile
**现象**：Unity 的 "Build"（非 "Append"）会重新生成整个 Xcode 工程，丢失 signing 配置。

**修复**：`Assets/Editor/VRBoxXcodePatcher.cs` — `PostProcessBuild` 脚本自动注入 signing 配置。
填写 `TEAM_ID`（10位，Apple Developer → Account → Membership → Team ID）。
**注意**：日常使用优先用 "Append" 模式（增量更新），避免重新生成。

---

### 坑 7：`DrawMeshNow` 在 Metal/iOS 上不可靠
**早期方案**：`EyeDistortionBlit` 使用 `DrawMeshNow` + 稠密 quad mesh 在 `OnRenderImage` 内绘制。

**问题**：Metal 上的 render target 和 viewport 管理由 `DrawMeshNow` 自行处理，与 Unity 的 `OnRenderImage` 内部机制冲突，导致结果不可预期（渲染错误或无输出）。

**修复**：改用 `Graphics.Blit(src, dest, material)`，将所有 render target + viewport 管理完全交给 Unity，Metal 上稳定可靠。

---

## 重要约定

### VRBoxSceneSetup 向导行为
- 每次运行都会创建全新场景（`NewScene`），清除旧场景
- 每次运行都会强制更新 VRSettings（包括 k1/k2），可用于切换测试/生产值
- `LensDistortionBlit_Mat.mat` 每次重建（先删除旧的再创建）

### VideoTextureReceiver 的 URL 写法
```
streaming:video360.mp4   ← 自动展开为 file:// + StreamingAssets 路径
file:///path/to/file.mp4 ← 绝对路径（本地文件）
https://...              ← 远程 HTTP
```

### 测试值 vs 生产值
| 参数 | 测试 | 生产 |
|------|------|------|
| VRSettings.k1 | 1.0 | 0.2 |
| VRSettings.k2 | 0.0 | 0.05 |
| LensRadius | 0.9（默认） | 根据镜片标定 |

---

## Unity iOS 开发规则（本项目总结）

### Shader 引用：必须通过 Material 资源，不能用 Shader 字段
Unity iOS build 只通过 **Material 资源**追踪 shader 依赖。`[SerializeField] Shader` 字段可能被构建系统静默剥离。

**错误写法：**
```csharp
[SerializeField] private Shader myShader;
// Awake: _mat = new Material(myShader);  → myShader 在设备上是 NULL
```

**正确写法：**
```csharp
[SerializeField] private Material myMaterial;  // 保存的 .mat 资源
// Awake: _mat = new Material(myMaterial);  → 始终有效
```
向导/Setup 脚本必须用 `AssetDatabase.CreateAsset()` 保存 `.mat` 文件，再引用该材质。

### `OnRenderImage` 后处理：只用 `Graphics.Blit`
Metal/iOS 上**不要**在 `OnRenderImage` 内用 `DrawMeshNow`，会与 Unity 内部 render target 管理冲突。
始终用 `Graphics.Blit(src, dest, material)` — Unity 负责所有 Metal viewport + render target 的设置。

### Xcode 每次 Unity "Build" 后会丢失 Provisioning Profile
Unity 的 "Build"（非 "Append"）会重新生成整个 Xcode 工程，丢失 signing 配置。两种缓解方案：
1. 日常迭代优先用 "Append" 模式
2. 添加 `PostProcessBuild` 脚本自动注入 signing 配置（见 `VRBoxXcodePatcher.cs`）

### 分屏视口相机的 `Camera.pixelWidth` / `pixelHeight`
对于分屏视口相机（如左眼 `rect(0,0,0.5,1)`）：
- `cam.pixelWidth = Screen.width * rect.width`（视口的实际像素宽度，非全屏宽度）
- 用 `cam.pixelWidth / cam.pixelHeight` 获取眼睛视口的宽高比，用于 shader 计算

### CVMetal API 命名（Objective-C++）
CoreVideo Metal API 前缀是 `CVMetal`，**不是** `CVMTL`：
- `CVMetalTextureCacheRef`、`CVMetalTextureRef`
- `CVMetalTextureCacheCreate`、`CVMetalTextureCacheCreateTextureFromImage`
- `CVMetalTextureGetTexture`、`CVMetalTextureCacheFlush`

### `extern "C"` 前向声明
函数 A 调用函数 B，若 B 定义在 A 之后且都在同一 `extern "C"` 块内，需在块顶部加 B 的前向声明，否则 Xcode 拒绝编译。

---

## 视觉效果调试指南（Unity）

### "效果不可见"排查清单
设备上后处理效果无输出时，依次检查：
1. 运行时序列化的 material/shader 引用是否为 NULL（见上方 shader 引用规则）
2. `Awake` 中加 `Debug.Log` 打印 material 是否为 null，确认 shader 是否实际运行
3. 参数值太小视觉上难以察觉——临时用极端值（如 10×）确认 shader 在执行
4. 黑色边角或圆形边框**不是**确认桶形畸变 shader 运行的必要条件，效果是视口内的内容扭曲

### VR 镜头畸变的屏幕外观
经典 VR Cardboard 截图（黑色背景上的圆形眼睛视图）由**两个独立部分**构成：
1. **圆形镜片光圈遮罩** — 圆外像素→黑色。这是"VR 护目镜"外观的来源。
2. **桶形预畸变** — 内容扭曲，使针形镜片抵消后呈现无畸变图像。

k1=0.2（典型镜片标定值）的桶形畸变独立作用时，边缘位移约 7%，**不产生黑色边框**，在 360° 视频中视觉上极不明显。圆形遮罩才是让 VR 外观立刻显现的关键。
