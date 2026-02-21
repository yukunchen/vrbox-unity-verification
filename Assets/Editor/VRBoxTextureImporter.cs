#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动把 Equirectangular360.jpg 导入设置改为 4096，
/// 并赋给场景里的 360_Sphere。
/// 在 Unity 完成导入后会自动触发一次。
/// </summary>
class VRBoxTexturePostprocessor : AssetPostprocessor
{
    private const string TargetAssetPath = "Assets/VR/Equirectangular360.jpg";

    // 导入前：调整纹理设置
    void OnPreprocessTexture()
    {
        if (assetPath != TargetAssetPath) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.maxTextureSize  = 4096;
        importer.textureShape    = TextureImporterShape.Texture2D;
        importer.wrapMode        = TextureWrapMode.Repeat;
        importer.filterMode      = FilterMode.Bilinear;
        importer.mipmapEnabled   = false;   // 360° 球不需要 mipmap

        Debug.Log("[VRBoxImporter] Equirectangular360.jpg 导入设置已应用 (4096, Repeat)");
    }

    // 导入后：赋给球材质
    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path != TargetAssetPath) continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;

            // 找左眼材质
            Material leftMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VR/360_Test_Mat.mat");
            if (leftMat != null)
            {
                leftMat.mainTexture = tex;
                EditorUtility.SetDirty(leftMat);
            }

            // 找右眼材质（Add Stereo 之后才存在）
            Material rightMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VR/360_Right_Mat.mat");
            if (rightMat != null)
            {
                rightMat.mainTexture = tex;
                EditorUtility.SetDirty(rightMat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[VRBoxImporter] ✅ Equirectangular360.jpg 已自动赋给 360° 球材质");
        }
    }
}
#endif
