/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class IosBuild
    {
        private const string DefaultXcodeProjectPath = "Builds/iOS/Xcode";

        [MenuItem("Decantra/Build/iOS Simulator Xcode Project")]
        public static void BuildSimulatorXcodeProject()
        {
            BuildXcodeProject(iOSSdkVersion.SimulatorSDK, developmentBuild: true);
        }

        [MenuItem("Decantra/Build/iOS Device Xcode Project")]
        public static void BuildDeviceXcodeProject()
        {
            BuildXcodeProject(iOSSdkVersion.DeviceSDK, developmentBuild: false);
        }

        private static void BuildXcodeProject(iOSSdkVersion sdkVersion, bool developmentBuild)
        {
            ConfigureVersioningFromEnv();

            EditorUserBuildSettings.development = developmentBuild;
            EditorUserBuildSettings.allowDebugging = developmentBuild;
            EditorUserBuildSettings.connectProfiler = false;

            PlayerSettings.productName = "Cantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = !developmentBuild;
            PlayerSettings.iOS.sdkVersion = sdkVersion;
            PlayerSettings.iOS.targetOSVersionString = ResolveIosMinVersion();

            string outputPath = ResolveBuildPath();
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
            Directory.CreateDirectory(outputPath);
            AssetDatabase.SaveAssets();

            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Decantra/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.AcceptExternalModificationsToPlayer | BuildOptions.StrictMode
            };

            Debug.Log($"IosBuild: exporting iOS {(sdkVersion == iOSSdkVersion.SimulatorSDK ? "simulator" : "device")} Xcode project to {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"iOS export failed: {report.summary.result}");
            }

            Debug.Log($"IosBuild: iOS Xcode project exported at {outputPath}");
        }

        private static string ResolveBuildPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outputPath = DefaultXcodeProjectPath;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildPath", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath = args[i + 1];
                }
            }

            return string.IsNullOrWhiteSpace(outputPath) ? DefaultXcodeProjectPath : outputPath;
        }

        private static string ResolveIosMinVersion()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-iosMinVersion", StringComparison.OrdinalIgnoreCase))
                {
                    string cliMinVersion = args[i + 1]?.Trim();
                    if (!string.IsNullOrWhiteSpace(cliMinVersion))
                    {
                        return cliMinVersion;
                    }
                }
            }

            string configured = PlayerSettings.iOS.targetOSVersionString;
            return string.IsNullOrWhiteSpace(configured) ? "15.0" : configured;
        }

        private static void ConfigureVersioningFromEnv()
        {
            string versionName = Environment.GetEnvironmentVariable("VERSION_NAME");
            if (!string.IsNullOrWhiteSpace(versionName))
            {
                PlayerSettings.bundleVersion = versionName.Trim();
                Debug.Log($"IosBuild: bundleVersion={PlayerSettings.bundleVersion}");
            }

            string buildNumber = Environment.GetEnvironmentVariable("VERSION_CODE");
            if (string.IsNullOrWhiteSpace(buildNumber))
            {
                buildNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            }

            if (!string.IsNullOrWhiteSpace(buildNumber))
            {
                PlayerSettings.iOS.buildNumber = buildNumber.Trim();
                Debug.Log($"IosBuild: iOS buildNumber={PlayerSettings.iOS.buildNumber}");
            }
        }
    }
}