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
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
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
                AndroidSdkVersions.AndroidApiLevel35;

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
                AndroidSdkVersions.AndroidApiLevel35;

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
            PlayerSettings.Android.targetSdkVersion =
                AndroidSdkVersions.AndroidApiLevel35;
            ConfigureAndroidToolchainFromEnv();
            ConfigureVersioningFromEnv();
            ConfigureAndroidSigningFromEnv(ShouldRequireKeystore(options));
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

        private static bool ShouldRequireKeystore(BuildOptions options)
        {
            if ((options & BuildOptions.Development) == BuildOptions.Development)
            {
                return false;
            }

            if (IsTruthyEnv("DECANTRA_REQUIRE_KEYSTORE")
                || IsTruthyEnv("REQUIRE_ANDROID_KEYSTORE"))
            {
                return true;
            }

            if (IsTruthyEnv("CI") && IsTagBuild())
            {
                return true;
            }

            return false;
        }

        private static bool IsTagBuild()
        {
            string refType = Environment.GetEnvironmentVariable("GITHUB_REF_TYPE");
            if (string.Equals(refType, "tag", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string refName = Environment.GetEnvironmentVariable("GITHUB_REF");
            return !string.IsNullOrWhiteSpace(refName)
                && refName.StartsWith("refs/tags/", StringComparison.OrdinalIgnoreCase);
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

        private static void ConfigureAndroidSigningFromEnv(bool requireKeystore)
        {
            string keystorePath = Environment.GetEnvironmentVariable("KEYSTORE_STORE_FILE");
            string keystorePass = Environment.GetEnvironmentVariable("KEYSTORE_STORE_PASSWORD");
            string keyAlias = Environment.GetEnvironmentVariable("KEYSTORE_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("KEYSTORE_KEY_PASSWORD");

            if (!string.IsNullOrWhiteSpace(keystorePath)
                && File.Exists(keystorePath)
                && !string.IsNullOrWhiteSpace(keystorePass)
                && !string.IsNullOrWhiteSpace(keyAlias))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyAlias;
                PlayerSettings.Android.keyaliasPass = string.IsNullOrWhiteSpace(keyPass)
                    ? keystorePass
                    : keyPass;
                Debug.Log("AndroidBuild: Using custom keystore for release signing.");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("AndroidBuild: Keystore env vars missing or file not found; using default signing.");
                if (requireKeystore)
                {
                    throw new InvalidOperationException(
                        "AndroidBuild: Release signing required but keystore env vars are missing."
                    );
                }
            }
        }

        private static void ConfigureVersioningFromEnv()
        {
            string resolvedVersionName = ResolveVersionName();
            if (!string.IsNullOrWhiteSpace(resolvedVersionName))
            {
                PlayerSettings.bundleVersion = resolvedVersionName.Trim();
            }

            int? resolvedVersionCode = ResolveVersionCode();
            if (resolvedVersionCode.HasValue && resolvedVersionCode.Value > 0)
            {
                PlayerSettings.Android.bundleVersionCode = resolvedVersionCode.Value;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"AndroidBuild: VersionName={PlayerSettings.bundleVersion}, VersionCode={PlayerSettings.Android.bundleVersionCode}"
            );
        }

        private static string ResolveVersionName()
        {
            string envVersion = FirstNonEmptyEnv("VERSION_NAME", "DECANTRA_VERSION_NAME", "VITE_APP_VERSION");
            if (!string.IsNullOrWhiteSpace(envVersion))
            {
                return envVersion;
            }

            string refType = Environment.GetEnvironmentVariable("GITHUB_REF_TYPE");
            string refName = Environment.GetEnvironmentVariable("GITHUB_REF_NAME");
            if (string.Equals(refType, "tag", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(refName))
            {
                return refName;
            }

            string gitTag = TryRunGit("describe", "--tags", "--abbrev=0");
            if (!string.IsNullOrWhiteSpace(gitTag))
            {
                return gitTag;
            }

            return PlayerSettings.bundleVersion;
        }

        private static int? ResolveVersionCode()
        {
            string envVersionCode = FirstNonEmptyEnv("VERSION_CODE", "DECANTRA_VERSION_CODE");
            if (TryParsePositiveInt(envVersionCode, out int code))
            {
                return ClampVersionCode(code);
            }

            string runNumberRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (TryParsePositiveInt(runNumberRaw, out int runNumber))
            {
                int runAttempt = 1;
                string runAttemptRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT");
                if (TryParsePositiveInt(runAttemptRaw, out int attempt))
                {
                    runAttempt = attempt;
                }

                long computed = 1000L + (long)runNumber * 10L + Math.Max(0, runAttempt - 1);
                return ClampVersionCode(computed);
            }

            string commitCountRaw = TryRunGit("rev-list", "--count", "HEAD");
            if (TryParsePositiveInt(commitCountRaw, out int commitCount))
            {
                return ClampVersionCode(1000L + commitCount);
            }

            int existing = PlayerSettings.Android.bundleVersionCode;
            return existing > 0 ? existing : 1;
        }

        private static int ClampVersionCode(long value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < 1)
            {
                return 1;
            }

            return (int)value;
        }

        private static string FirstNonEmptyEnv(params string[] names)
        {
            foreach (string name in names)
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static bool TryParsePositiveInt(string value, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return int.TryParse(trimmed, out result) && result > 0;
        }

        private static string TryRunGit(string command, params string[] args)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"{command} {string.Join(" ", args)}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);
                    if (process.ExitCode != 0)
                    {
                        return null;
                    }

                    return output.Trim();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsTruthyEnv(string name)
        {
            return IsTruthy(Environment.GetEnvironmentVariable(name));
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            return string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase);
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
