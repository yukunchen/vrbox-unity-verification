/**
 * VRBoxSceneSetup.cs  —  Editor-only
 * 菜单：VRBox → Setup Phase 1 Scene
 *
 * 一键建立 Phase 1 最小验证场景：
 *   - VR Rig root + PhoneIMUSource
 *   - Left/Right Camera (子物体) + VRHeadTracking
 *   - StereoCameraRig（默认双目）
 *   - 棋盘格 360° 球（验证旋转方向）
 *   - 四个彩色方块（方位参考）
 */
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBox;

public static class VRBoxSceneSetup
{
    [MenuItem("VRBox/Setup Phase 1 Scene")]
    public static void SetupPhase1()
    {
        // Always start from a clean scene so no legacy Main Camera remains.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. VRSettings ScriptableObject ─────────────────────────────
        const string settingsPath = "Assets/VR/VRSettings_Default.asset";
        VRSettings settings = AssetDatabase.LoadAssetAtPath<VRSettings>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<VRSettings>();
            settings.k1             = 0.2f;
            settings.k2             = 0.05f;
            settings.ipd            = 0.064f;
            settings.fovDegrees     = 90f;
            settings.displayLatencyMs = 4f;
            AssetDatabase.CreateAsset(settings, settingsPath);
            AssetDatabase.SaveAssets();
        }

        // ── 2. VR Rig root ──────────────────────────────────────────────
        var rig = new GameObject("VR Rig");
        var imuSource = rig.AddComponent<PhoneIMUSource>();

        // ── 3. Stereo Cameras (children of Rig) ────────────────────────────
        var leftGO = new GameObject("Left Camera");
        leftGO.tag = "MainCamera";
        leftGO.transform.SetParent(rig.transform, false);

        var leftCam = leftGO.AddComponent<Camera>();
        leftCam.fieldOfView   = 90f;
        leftCam.nearClipPlane = 0.01f;
        leftCam.farClipPlane  = 500f;
        leftCam.clearFlags    = CameraClearFlags.SolidColor;
        leftCam.backgroundColor = Color.black;
        leftCam.depth = 0f;

        var rightGO = new GameObject("Right Camera");
        rightGO.tag = "Untagged";
        rightGO.transform.SetParent(rig.transform, false);

        var rightCam = rightGO.AddComponent<Camera>();
        rightCam.fieldOfView   = 90f;
        rightCam.nearClipPlane = leftCam.nearClipPlane;
        rightCam.farClipPlane  = leftCam.farClipPlane;
        rightCam.clearFlags    = leftCam.clearFlags;
        rightCam.backgroundColor = leftCam.backgroundColor;
        rightCam.depth = leftCam.depth + 1f;

        // VRHeadTracking on camera object (OnPreRender only fires on Camera components)
        var headTracking = leftGO.AddComponent<VRHeadTracking>();
        SetField(headTracking, "imuSourceComponent", imuSource);
        SetField(headTracking, "vrSettings",         settings);
        SetField(headTracking, "cameraRigTransform", rig.transform);

        // Centralized stereo setup from VRSettings
        var stereoRig = rig.AddComponent<StereoCameraRig>();
        SetField(stereoRig, "leftCamera",  leftCam);
        SetField(stereoRig, "rightCamera", rightCam);
        SetField(stereoRig, "vrSettings",  settings);
        stereoRig.ApplyStereoCameraSetup();

        // ── 4. 360° Sphere ──────────────────────────────────────────────
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "360_Sphere";
        sphere.transform.localScale = Vector3.one * 200f;
        Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

        var eqShader = Shader.Find("VRBox/Equirectangular360");
        if (eqShader != null)
        {
            var mat = new Material(eqShader);
            mat.SetFloat("_StereoMode", 0f);
            mat.SetFloat("_Eye",        0f);
            mat.mainTexture = MakeCheckerTexture();
            AssetDatabase.CreateAsset(mat, "Assets/VR/360_Test_Mat.mat");
            sphere.GetComponent<Renderer>().sharedMaterial = mat;
        }
        else
        {
            Debug.LogWarning("[VRBoxSetup] Shader VRBox/Equirectangular360 not found yet — " +
                "it will appear after Unity re-imports shaders. Re-run this menu item.");
        }

        // ── 5. Cardinal reference cubes ─────────────────────────────────
        MakeCube("North [Blue]",  new Vector3( 0, 0,  8), new Color(0.2f, 0.4f, 1f));
        MakeCube("South [Red]",   new Vector3( 0, 0, -8), new Color(1f, 0.2f, 0.2f));
        MakeCube("East  [Green]", new Vector3( 8, 0,  0), new Color(0.2f, 0.9f, 0.3f));
        MakeCube("West  [Yellow]",new Vector3(-8, 0,  0), new Color(1f, 0.9f, 0.1f));
        MakeCube("Up    [White]", new Vector3( 0, 8,  0), Color.white);

        // ── 6. Light ────────────────────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 7. Save ─────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = rig;
        Debug.Log("[VRBoxSetup] ✅ Phase 1 scene ready (stereo enabled). " +
                  "File → Save As → Assets/VRBox_Phase1.unity  →  Build & Run on iPhone.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        target.GetType()
              .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
              ?.SetValue(target, value);
    }

    private static void MakeCube(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 1.5f;

        var mat = new Material(Shader.Find("Standard")) { color = color };
        go.GetComponent<Renderer>().sharedMaterial = mat;

        string safeName = name.Split('[')[0].Trim().Replace(" ", "_");
        AssetDatabase.CreateAsset(mat, $"Assets/VR/Cube_{safeName}_Mat.mat");
    }

    /// <summary>Creates a simple checkerboard texture so rotation direction is obvious.</summary>
    private static Texture2D MakeCheckerTexture(int w = 512, int h = 256, int cells = 8)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var pixels = new Color[w * h];
        int cw = w / cells, ch = h / cells;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool xb = (x / cw) % 2 == 0;
            bool yb = (y / ch) % 2 == 0;
            pixels[y * w + x] = (xb ^ yb) ? Color.white : new Color(0.15f, 0.45f, 0.9f);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        // Save as PNG so it persists in the project
        byte[] png = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(
            System.IO.Path.Combine(Application.dataPath, "VR/Checker360.png"), png);
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VR/Checker360.png");
    }
}
#endif
