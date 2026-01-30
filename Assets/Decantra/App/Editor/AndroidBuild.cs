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

        public static void BuildDebugApk()
        {
            BuildApk(BuildOptions.Development);
        }

        public static void BuildReleaseApk()
        {
            BuildApk(BuildOptions.None);
        }

        private static void BuildApk(BuildOptions options)
        {
            ConfigureAndroidToolchainFromEnv();
            string[] args = Environment.GetCommandLineArgs();
            string outputPath = DefaultApkPath;
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
                outputPath = DefaultApkPath;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Decantra/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Android build failed: {report.summary.result}");
                throw new Exception("Android build failed");
            }

            Debug.Log($"Android APK built at {outputPath}");
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

            SetStringProperty(settingsType, "sdkRootPath", sdkRoot);
            SetStringProperty(settingsType, "ndkRootPath", ndkRoot);
            SetStringProperty(settingsType, "jdkRootPath", jdkRoot);

            SetBoolProperty(settingsType, "UseEmbeddedSdk", false, sdkRoot);
            SetBoolProperty(settingsType, "UseEmbeddedNdk", false, ndkRoot);
            SetBoolProperty(settingsType, "UseEmbeddedJdk", false, jdkRoot);
        }

        private static void SetStringProperty(Type settingsType, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var prop = settingsType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(null, value);
            }
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
    }
}
