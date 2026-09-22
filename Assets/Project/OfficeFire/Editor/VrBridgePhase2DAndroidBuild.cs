#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Batchmode Android IL2CPP ARM64 APK build for Phase 2D verification.
    /// -executeMethod Woi.OfficeFire.Editor.VrBridgePhase2DAndroidBuild.Execute
    /// </summary>
    public static class VrBridgePhase2DAndroidBuild
    {
        const string DefaultApkPath = "Builds/Android/OfisISG-VRBridge-Phase2D.apk";
        const string VersionName = "0.2.0-phase2d";
        const int VersionCode = 2;

        [MenuItem("WOI/VR Bridge/Build Android APK (Phase 2D)")]
        public static void ExecuteFromMenu() => Execute();

        public static void Execute()
        {
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.Woi.OfficeEvacuation");
                PlayerSettings.bundleVersion = VersionName;
                PlayerSettings.Android.bundleVersionCode = VersionCode;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException("No enabled scenes in Build Settings.");
                }

                var apkPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultApkPath));
                Directory.CreateDirectory(Path.GetDirectoryName(apkPath) ?? "Builds/Android");

                Debug.Log(
                    $"[VRBridge-Build-2D] Starting Android APK. id={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)} " +
                    $"version={PlayerSettings.bundleVersion} code={PlayerSettings.Android.bundleVersionCode} " +
                    $"backend=IL2CPP arch=ARM64 scenes={scenes.Length} out={apkPath}");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log(
                    $"[VRBridge-Build-2D] Result={summary.result} sizeBytes={summary.totalSize} " +
                    $"errors={summary.totalErrors} warnings={summary.totalWarnings} output={summary.outputPath}");

                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            Debug.LogError($"[VRBridge-Build-2D] {msg.content}");
                        }
                    }
                }

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VRBridge-Build-2D] " + ex);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                throw;
            }
        }
    }
}
#endif
