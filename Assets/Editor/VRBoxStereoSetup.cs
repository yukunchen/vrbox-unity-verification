/**
 * VRBoxStereoSetup.cs — Editor only
 * 菜单：VRBox → Add Stereo Cameras
 *
 * 在已有场景上把单目摄像机改成左右双目，不需要重建场景。
 */
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBox;

public static class VRBoxStereoSetup
{
    [MenuItem("VRBox/Add Stereo Cameras")]
    public static void AddStereo()
    {
        // ── 找到现有摄像机 ──────────────────────────────────────────────
        Camera existingCam = null;
        VRHeadTracking[] trackers = Object.FindObjectsOfType<VRHeadTracking>();
        foreach (var t in trackers)
        {
            Camera c = t.GetComponent<Camera>();
            if (c != null)
            {
                existingCam = c;
                break;
            }
        }
        if (existingCam == null)
            existingCam = Camera.main;
        if (existingCam == null)
        {
            Debug.LogError("[VRBoxStereo] 找不到 Main Camera，请先运行 'VRBox → Setup Phase 1 Scene'");
            return;
        }

        GameObject camGO  = existingCam.gameObject;
        Transform  rigTr  = camGO.transform.parent;   // VR Rig

        if (rigTr == null)
        {
            Debug.LogError("[VRBoxStereo] Main Camera 没有父物体 (VR Rig)。场景结构不对。");
            return;
        }

        // ── 读取 VRSettings（IPD / FOV）─────────────────────────────────
        float ipd = 0.064f;
        float fov = 90f;
        VRHeadTracking ht = camGO.GetComponent<VRHeadTracking>();
        if (ht != null)
        {
            var settingsField = typeof(VRHeadTracking)
                .GetField("vrSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            VRSettings vs = settingsField?.GetValue(ht) as VRSettings;
            if (vs != null) { ipd = vs.ipd; fov = vs.fovDegrees; }
        }
        float halfIpd = ipd * 0.5f;

        // ── 配置左眼（复用现有 Main Camera）────────────────────────────
        camGO.name = "Left Camera";
        camGO.tag  = "MainCamera";
        existingCam.rect         = new Rect(0f, 0f, 0.5f, 1f);
        existingCam.fieldOfView  = fov;
        camGO.transform.localPosition = new Vector3(-halfIpd, 0f, 0f);
        camGO.transform.localRotation = Quaternion.identity;

        // ── 创建/复用右眼 ───────────────────────────────────────────────
        Transform rightTr = rigTr.Find("Right Camera");
        GameObject rightGO;
        Camera rightCam;
        if (rightTr != null && rightTr.GetComponent<Camera>() != null)
        {
            rightGO = rightTr.gameObject;
            rightCam = rightGO.GetComponent<Camera>();
        }
        else
        {
            rightGO = new GameObject("Right Camera");
            rightGO.transform.SetParent(rigTr, false);
            rightCam = rightGO.AddComponent<Camera>();
        }

        rightGO.transform.localPosition = new Vector3(halfIpd, 0f, 0f);
        rightGO.transform.localRotation = Quaternion.identity;
        rightCam.rect            = new Rect(0.5f, 0f, 0.5f, 1f);
        rightCam.fieldOfView     = fov;
        rightCam.nearClipPlane   = existingCam.nearClipPlane;
        rightCam.farClipPlane    = existingCam.farClipPlane;
        rightCam.clearFlags      = existingCam.clearFlags;
        rightCam.backgroundColor = existingCam.backgroundColor;
        rightCam.depth           = existingCam.depth + 1;
        rightGO.tag              = "Untagged";

        // 挂载 StereoCameraRig，统一双目参数入口
        StereoCameraRig stereoRig = rigTr.GetComponent<StereoCameraRig>();
        if (stereoRig == null)
            stereoRig = rigTr.gameObject.AddComponent<StereoCameraRig>();
        SetField(stereoRig, "leftCamera", existingCam);
        SetField(stereoRig, "rightCamera", rightCam);
        if (ht != null)
        {
            var settingsField = typeof(VRHeadTracking)
                .GetField("vrSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            VRSettings vs = settingsField?.GetValue(ht) as VRSettings;
            if (vs != null) SetField(stereoRig, "vrSettings", vs);
        }
        stereoRig.ApplyStereoCameraSetup();

        // VRHeadTracking 留在左眼即可（旋转 VR Rig 根节点，右眼跟着动）

        // ── 360° 球：右眼材质 _Eye = 1 ─────────────────────────────────
        // 找到场景里的 360_Sphere，给右眼设 _Eye = 1
        GameObject sphere = GameObject.Find("360_Sphere");
        if (sphere != null)
        {
            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                // 左眼用原材质（_Eye=0），右眼复制一份（_Eye=1）
                Material leftMat  = rend.sharedMaterial;
                const string rightMatPath = "Assets/VR/360_Right_Mat.mat";
                Material rightMat = AssetDatabase.LoadAssetAtPath<Material>(rightMatPath);
                if (rightMat == null)
                {
                    rightMat = new Material(leftMat);
                    AssetDatabase.CreateAsset(rightMat, rightMatPath);
                }
                rightMat.SetFloat("_Eye", 1f);

                // 右眼摄像机通过 Layer + Culling Mask 渲染独立材质
                // 简单方案：两眼 culling mask 全开（mono 图两眼看同一内容）
                // 如用 SideBySide 立体源图则在此处做区分
                leftMat.SetFloat("_Eye", 0f);

                Debug.Log("[VRBoxStereo] Sphere 材质已分配：左眼 _Eye=0，右眼 _Eye=1（mono 图两眼相同）");
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[VRBoxStereo] ✅ 双目设置完成。IPD={ipd*1000:F0}mm，FOV={fov}°。保存场景后 Build & Run。");
        Selection.activeGameObject = rightGO;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType()
              .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
              ?.SetValue(target, value);
    }

    // ── 辅助：把下载好的 360° 图赋给球 ──────────────────────────────────
    [MenuItem("VRBox/Set 360 Texture from Selection")]
    public static void Set360Texture()
    {
        Texture2D tex = Selection.activeObject as Texture2D;
        if (tex == null)
        {
            Debug.LogError("[VRBoxStereo] 请先在 Project 面板选中要用的 360° 图片");
            return;
        }

        GameObject sphere = GameObject.Find("360_Sphere");
        if (sphere == null) { Debug.LogError("[VRBoxStereo] 场景里没有 360_Sphere"); return; }

        Renderer rend = sphere.GetComponent<Renderer>();
        if (rend == null) return;

        // 左右眼材质都换贴图
        Material[] mats = rend.sharedMaterials;
        foreach (var m in mats)
            if (m != null) m.mainTexture = tex;

        // 如果只有一个材质（还没分双目），直接换
        if (rend.sharedMaterial != null)
            rend.sharedMaterial.mainTexture = tex;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[VRBoxStereo] ✅ 已将 {tex.name} 设为 360° 球贴图");
    }
}
#endif
