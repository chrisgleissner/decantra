/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class AndroidBuild
    {
        private const string DefaultApkPath = "Builds/Android/Decantra.apk";
        private const string DefaultAabPath = "Builds/Android/Decantra.aab";

        [MenuItem("Decantra/Build/Android Debug APK")]
        public static void BuildDebugApk()
        {
            BuildApk(BuildOptions.Development);
        }

        [MenuItem("Decantra/Build/Android Release APK")]
        public static void BuildReleaseApk()
        {
            // ---- Build mode ----
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            // ---- Target device: Galaxy S21+ ----
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;

            // ---- Scripting backend & stripping ----
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android,
                ScriptingImplementation.IL2CPP
            );

            PlayerSettings.SetManagedStrippingLevel(
                BuildTargetGroup.Android,
                ManagedStrippingLevel.High
            );

            PlayerSettings.stripEngineCode = true;

            // ---- Graphics ----
            // Lock to GLES3, exclude Vulkan entirely
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 }
            );

            // ---- Android API level ----
            PlayerSettings.Android.minSdkVersion =
                AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion =
                AndroidSdkVersions.AndroidApiLevelAuto;

            // ---- Disable unused services ----
            PlayerSettings.enableCrashReportAPI = false;
            PlayerSettings.usePlayerLog = false;

            // ---- Identity ----
            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";

            // ---- Ensure settings persist before IL2CPP build ----
            AssetDatabase.SaveAssets();

            // ---- Build ----
            BuildApk(BuildOptions.None);
        }

        [MenuItem("Decantra/Build/Android Release AAB")]
        public static void BuildReleaseAab()
        {
            // ---- Build mode ----
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            // ---- Target device: Galaxy S21+ ----
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;

            // ---- Scripting backend & stripping ----
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android,
                ScriptingImplementation.IL2CPP
            );

            PlayerSettings.SetManagedStrippingLevel(
                BuildTargetGroup.Android,
                ManagedStrippingLevel.High
            );

            PlayerSettings.stripEngineCode = true;

            // ---- Graphics ----
            // Lock to GLES3, exclude Vulkan entirely
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 }
            );

            // ---- Android API level ----
            PlayerSettings.Android.minSdkVersion =
                AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion =
                AndroidSdkVersions.AndroidApiLevelAuto;

            // ---- Disable unused services ----
            PlayerSettings.enableCrashReportAPI = false;
            PlayerSettings.usePlayerLog = false;

            // ---- Identity ----
            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";

            // ---- Ensure settings persist before IL2CPP build ----
            AssetDatabase.SaveAssets();

            // ---- Build ----
            BuildAab(BuildOptions.None);
        }

        private static void BuildApk(BuildOptions options)
        {
            BuildAndroidArtifact(options, DefaultApkPath, false, "APK");
        }

        private static void BuildAab(BuildOptions options)
        {
            BuildAndroidArtifact(options, DefaultAabPath, true, "AAB");
        }

        private static void BuildAndroidArtifact(
            BuildOptions options,
            string defaultPath,
            bool buildAppBundle,
            string artifactLabel
        )
        {
            ConfigureAndroidToolchainFromEnv();
            string[] args = Environment.GetCommandLineArgs();
            string outputPath = defaultPath;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildPath")
                {
                    outputPath = args[i + 1];
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = defaultPath;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";

            bool previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Decantra/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Android build failed: {report.summary.result}");
                throw new Exception("Android build failed");
            }

            Debug.Log($"Android {artifactLabel} built at {outputPath}");
        }

        private static void ConfigureAndroidToolchainFromEnv()
        {
            var sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
                ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
            var ndkRoot = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            var jdkRoot = Environment.GetEnvironmentVariable("JAVA_HOME");

            var settingsType = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            if (settingsType == null)
            {
                return;
            }

            TrySetStringProperty(settingsType, "sdkRootPath", sdkRoot);
            TrySetStringProperty(settingsType, "ndkRootPath", ndkRoot);
            bool jdkSet = TrySetStringProperty(settingsType, "jdkRootPath", jdkRoot);

            SetBoolProperty(settingsType, "UseEmbeddedSdk", false, sdkRoot);
            SetBoolProperty(settingsType, "UseEmbeddedNdk", false, ndkRoot);
            if (jdkSet)
            {
                SetBoolProperty(settingsType, "UseEmbeddedJdk", false, jdkRoot);
            }
            else
            {
                SetBoolProperty(settingsType, "UseEmbeddedJdk", true);
            }
        }

        private static bool TrySetStringProperty(Type settingsType, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var prop = settingsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                try
                {
                    prop.SetValue(null, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AndroidBuild: Unable to set {propertyName} from env: {ex.Message}");
                }
            }

            return false;
        }

        private static void SetBoolProperty(Type settingsType, string propertyName, bool value, string guardValue)
        {
            if (string.IsNullOrWhiteSpace(guardValue))
            {
                return;
            }

            var prop = settingsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
            {
                prop.SetValue(null, value);
            }
        }

        private static void SetBoolProperty(Type settingsType, string propertyName, bool value)
        {
            var prop = settingsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
            {
                prop.SetValue(null, value);
            }
        }
    }
}
