#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

/// <summary>
/// PostProcessBuild: automatically sets Xcode signing so provisioning profile
/// doesn't need to be re-selected after every Unity iOS build.
///
/// Fill in TEAM_ID below (10-character alphanumeric, found at
/// developer.apple.com → Account → Membership → Team ID).
/// </summary>
public static class VRBoxXcodePatcher
{
    // ── Set your Apple Team ID here ──────────────────────────────────────────
    private const string TEAM_ID = "";   // e.g. "AB12CD34EF"
    // ────────────────────────────────────────────────────────────────────────

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
#if UNITY_IOS
        if (target != BuildTarget.iOS) return;
        if (string.IsNullOrEmpty(TEAM_ID))
        {
            UnityEngine.Debug.LogWarning(
                "[VRBoxXcodePatcher] TEAM_ID is empty — skipping auto-signing patch.\n" +
                "Fill in TEAM_ID in Assets/Editor/VRBoxXcodePatcher.cs");
            return;
        }

        string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(pbxPath);

        // Patch main app target
        string mainTarget = project.GetUnityMainTargetGuid();
        PatchTarget(project, mainTarget);

        // Patch UnityFramework target (Unity 2019.3+)
        string frameworkTarget = project.GetUnityFrameworkTargetGuid();
        if (!string.IsNullOrEmpty(frameworkTarget))
            PatchTarget(project, frameworkTarget);

        project.WriteToFile(pbxPath);
        UnityEngine.Debug.Log("[VRBoxXcodePatcher] Xcode signing patched. Team: " + TEAM_ID);
#endif
    }

#if UNITY_IOS
    private static void PatchTarget(PBXProject project, string targetGuid)
    {
        project.SetBuildProperty(targetGuid, "DEVELOPMENT_TEAM",             TEAM_ID);
        project.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE",              "Automatic");
        project.SetBuildProperty(targetGuid, "PROVISIONING_PROFILE_SPECIFIER", "");
        project.SetBuildProperty(targetGuid, "CODE_SIGN_IDENTITY",           "Apple Development");
    }
#endif
}
#endif
