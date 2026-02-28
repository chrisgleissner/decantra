/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class IosBuild
    {
        private const string DefaultXcodeProjectPath = "Builds/iOS/Xcode";
        private const string IosProductName = "Decantra";
        private const string IosBundleId = "uk.gleissner.decantra";

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
            BuildInfoGenerator.GenerateAndImport();

            EditorUserBuildSettings.development = developmentBuild;
            EditorUserBuildSettings.allowDebugging = developmentBuild;
            EditorUserBuildSettings.connectProfiler = false;

            PlayerSettings.productName = IosProductName;
            PlayerSettings.applicationIdentifier = IosBundleId;
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
            string parentDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }
            AssetDatabase.SaveAssets();

            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Decantra/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"IosBuild: exporting iOS {(sdkVersion == iOSSdkVersion.SimulatorSDK ? "simulator" : "device")} Xcode project to {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"iOS export failed: {report.summary.result}");
            }

            EnforceIosDisplayNameInInfoPlist(outputPath, IosProductName);
            Debug.Log($"IosBuild: iOS Xcode project exported at {outputPath}");
        }

        private static void EnforceIosDisplayNameInInfoPlist(string xcodeProjectPath, string displayName)
        {
            if (string.IsNullOrWhiteSpace(xcodeProjectPath) || string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            string plistPath = Path.Combine(xcodeProjectPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"IosBuild: Info.plist not found at {plistPath}; skipping display-name enforcement.");
                return;
            }

            string plistText;
            try
            {
                plistText = File.ReadAllText(plistPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"IosBuild: Failed to read Info.plist at {plistPath}: {ex.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(plistText))
            {
                Debug.LogWarning($"IosBuild: Info.plist is empty at {plistPath}; skipping display-name enforcement.");
                return;
            }

            var dict = new PlistStringMap(plistText);
            SetPlistString(dict, "CFBundleDisplayName", displayName);
            SetPlistString(dict, "CFBundleName", displayName);

            plistText = dict.ToPlistText();

            File.WriteAllText(plistPath, plistText);

            Debug.Log($"IosBuild: enforced CFBundleDisplayName/CFBundleName to '{displayName}'.");
        }

        private static void SetPlistString(PlistStringMap dict, string keyName, string value)
        {
            if (dict == null || string.IsNullOrWhiteSpace(keyName))
            {
                return;
            }

            dict.SetString(keyName, value);
        }

        private static string UpsertPlistString(string plistText, string keyName, string value)
        {
            if (string.IsNullOrWhiteSpace(plistText) || string.IsNullOrWhiteSpace(keyName))
            {
                return plistText;
            }

            string keyPattern = $"(<key>\\s*{Regex.Escape(keyName)}\\s*</key>\\s*<string>)([^<]*)(</string>)";
            if (Regex.IsMatch(plistText, keyPattern, RegexOptions.Singleline))
            {
                return Regex.Replace(
                    plistText,
                    keyPattern,
                    $"$1{EscapePlistString(value)}$3",
                    RegexOptions.Singleline);
            }

            int dictCloseIndex = plistText.LastIndexOf("</dict>", StringComparison.Ordinal);
            if (dictCloseIndex < 0)
            {
                return plistText;
            }

            string insertion = $"\n\t<key>{keyName}</key>\n\t<string>{EscapePlistString(value)}</string>\n";
            return plistText.Insert(dictCloseIndex, insertion);
        }

        private static string EscapePlistString(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private sealed class PlistStringMap
        {
            private string _plistText;

            public PlistStringMap(string plistText)
            {
                _plistText = plistText;
            }

            public void SetString(string keyName, string value)
            {
                _plistText = UpsertPlistString(_plistText, keyName, value);
            }

            public string ToPlistText()
            {
                return _plistText;
            }
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
            string buildNumber = Environment.GetEnvironmentVariable("VERSION_CODE");
            if (string.IsNullOrWhiteSpace(buildNumber))
            {
                buildNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            }

            string sanitizedBundleVersion = BuildIosBundleVersion(versionName, buildNumber);
            PlayerSettings.bundleVersion = sanitizedBundleVersion;
            Debug.Log($"IosBuild: bundleVersion={PlayerSettings.bundleVersion}");

            if (!string.IsNullOrWhiteSpace(buildNumber))
            {
                PlayerSettings.iOS.buildNumber = buildNumber.Trim();
                Debug.Log($"IosBuild: iOS buildNumber={PlayerSettings.iOS.buildNumber}");
            }
        }

        private static string BuildIosBundleVersion(string versionName, string buildNumber)
        {
            string[] digitGroups = Regex.Matches(versionName ?? string.Empty, "\\d+")
                .Cast<Match>()
                .Select(match => match.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(3)
                .ToArray();

            if (digitGroups.Length == 0)
            {
                string fallbackDigits = Regex.Match(buildNumber ?? string.Empty, "\\d+").Value;
                if (string.IsNullOrWhiteSpace(fallbackDigits))
                {
                    fallbackDigits = "0";
                }

                digitGroups = new[] { "0", "0", fallbackDigits };
            }

            string iosVersion = string.Join(".", digitGroups);
            if (iosVersion.Length > 18)
            {
                int keep = Math.Max(1, 18 - (digitGroups.Length - 1));
                iosVersion = new string(iosVersion.Take(keep).ToArray());

                while (iosVersion.EndsWith(".", StringComparison.Ordinal))
                {
                    iosVersion = iosVersion[..^1];
                }

                if (iosVersion.Length == 0)
                {
                    iosVersion = "0";
                }
            }

            return iosVersion;
        }
    }
}
