/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
            Debug.Log($"========================================");
            Debug.Log($"AndroidBuild: Starting {artifactLabel} build");
            Debug.Log($"========================================");

            PlayerSettings.Android.targetSdkVersion =
                AndroidSdkVersions.AndroidApiLevel35;
            ConfigureAndroidToolchainFromEnv();
            ConfigureVersioningFromEnv();
            bool requireKeystore = ShouldRequireKeystore(options);

            Debug.Log($"AndroidBuild: Release signing required: {requireKeystore}");

            KeystoreConfig keystoreConfig = ConfigureAndroidSigningFromEnv(requireKeystore);

            // Final signing state verification before build
            if (requireKeystore)
            {
                Debug.Log("========================================");
                Debug.Log("PRE-BUILD SIGNING STATE VERIFICATION");
                Debug.Log("========================================");
                Debug.Log($"  useCustomKeystore: {PlayerSettings.Android.useCustomKeystore}");
                Debug.Log($"  keystoreName: {PlayerSettings.Android.keystoreName}");
                Debug.Log($"  keyaliasName: {PlayerSettings.Android.keyaliasName}");
                Debug.Log($"  keystorePass set: {!string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)}");
                Debug.Log($"  keyaliasPass set: {!string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass)}");
                Debug.Log("========================================");

                if (!PlayerSettings.Android.useCustomKeystore)
                {
                    FailBuild("AndroidBuild: FATAL - useCustomKeystore is false immediately before build. Release signing will not be applied.");
                }

                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
                {
                    FailBuild("AndroidBuild: FATAL - keystoreName is empty immediately before build.");
                }

                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                {
                    FailBuild("AndroidBuild: FATAL - keyaliasName is empty immediately before build.");
                }
            }

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

            Debug.Log($"AndroidBuild: Invoking BuildPipeline.BuildPlayer for {artifactLabel}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Android build failed: {report.summary.result}");
                throw new Exception("Android build failed");
            }

            if (buildAppBundle && requireKeystore)
            {
                VerifyAabSigning(outputPath, keystoreConfig);
            }

            Debug.Log($"Android {artifactLabel} built at {outputPath}");
        }

        private static bool ShouldRequireKeystore(BuildOptions options)
        {
            if ((options & BuildOptions.Development) == BuildOptions.Development)
            {
                return false;
            }

            // Release builds must be signed with a custom keystore.
            return true;
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

        private static KeystoreConfig ConfigureAndroidSigningFromEnv(bool requireKeystore)
        {
            // Check if signing was already configured by the CI runner (e.g., game-ci/unity-builder
            // sets PlayerSettings via its androidKeystoreName/Pass/Alias inputs before invoking the
            // custom build method). Env vars from GITHUB_ENV are not forwarded into the Docker
            // container, so the game-ci inputs are the reliable mechanism for CI signing.
            if (PlayerSettings.Android.useCustomKeystore
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName)
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName)
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keystorePass))
            {
                string existingKeystorePath = ResolveKeystorePath(PlayerSettings.Android.keystoreName);
                if (File.Exists(existingKeystorePath))
                {
                    string preConfiguredKeyPass = !string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasPass)
                        ? PlayerSettings.Android.keyaliasPass
                        : PlayerSettings.Android.keystorePass;

                    Debug.Log("========================================");
                    Debug.Log("ANDROID SIGNING PRE-CONFIGURED BY CI");
                    Debug.Log("========================================");
                    Debug.Log($"  Custom keystore enabled: {PlayerSettings.Android.useCustomKeystore}");
                    Debug.Log($"  Keystore path: {existingKeystorePath}");
                    Debug.Log($"  Key alias: {PlayerSettings.Android.keyaliasName}");
                    Debug.Log("========================================");

                    return new KeystoreConfig(
                        existingKeystorePath,
                        PlayerSettings.Android.keystorePass,
                        PlayerSettings.Android.keyaliasName,
                        preConfiguredKeyPass
                    );
                }
            }

            // Check both our custom env var names and game-ci's ANDROID_KEYSTORE_* names
            // (game-ci forwards its androidKeystoreName/Pass/Alias inputs as ANDROID_KEYSTORE_*
            //  env vars inside the Docker container)
            string keystorePathRaw = FirstNonEmptyEnv("KEYSTORE_STORE_FILE", "ANDROID_KEYSTORE_NAME");
            string keystorePass = FirstNonEmptyEnv("KEYSTORE_STORE_PASSWORD", "ANDROID_KEYSTORE_PASS");
            string keyAlias = FirstNonEmptyEnv("KEYSTORE_KEY_ALIAS", "ANDROID_KEYALIAS_NAME");
            string keyPass = FirstNonEmptyEnv("KEYSTORE_KEY_PASSWORD", "ANDROID_KEYALIAS_PASS");

            if (string.IsNullOrWhiteSpace(keystorePathRaw)
                || string.IsNullOrWhiteSpace(keystorePass)
                || string.IsNullOrWhiteSpace(keyAlias))
            {
                if (requireKeystore)
                {
                    FailBuild(
                        "AndroidBuild: Release signing required but keystore env vars are missing. "
                            + "Expected KEYSTORE_STORE_FILE, KEYSTORE_STORE_PASSWORD, KEYSTORE_KEY_ALIAS."
                    );
                }

                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("AndroidBuild: Keystore env vars missing; default signing will be used.");
                return null;
            }

            string keystorePath = ResolveKeystorePath(keystorePathRaw);
            EnsureReadableFile(keystorePath, "keystore");
            VerifyKeystoreContainsAlias(keystorePath, keystorePass, keyAlias);

            string resolvedKeyPass = string.IsNullOrWhiteSpace(keyPass)
                ? keystorePass
                : keyPass;

            // Configure signing - explicit field-by-field assignment
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = resolvedKeyPass;

            // CRITICAL: Persist settings to disk BEFORE building.
            // Unity batchmode may not persist PlayerSettings changes without this.
            AssetDatabase.SaveAssets();

            // Verify persistence succeeded by re-reading settings
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                FailBuild("AndroidBuild: CRITICAL - useCustomKeystore did not persist after SaveAssets(). Signing will fail.");
            }

            // Unity may normalize the keystore path to a relative path.
            // Verify either exact match OR that the stored path resolves to the same file.
            string storedKeystorePath = PlayerSettings.Android.keystoreName;
            string storedAbsolutePath = ResolveKeystorePath(storedKeystorePath);
            if (!string.Equals(storedAbsolutePath, keystorePath, StringComparison.Ordinal))
            {
                FailBuild($"AndroidBuild: CRITICAL - keystoreName did not persist correctly. " +
                    $"Expected '{keystorePath}', got '{storedKeystorePath}' (resolves to '{storedAbsolutePath}').");
            }

            if (!string.Equals(PlayerSettings.Android.keyaliasName, keyAlias, StringComparison.Ordinal))
            {
                FailBuild($"AndroidBuild: CRITICAL - keyaliasName did not persist. Expected '{keyAlias}', got '{PlayerSettings.Android.keyaliasName}'.");
            }

            Debug.Log("========================================");
            Debug.Log("ANDROID RELEASE SIGNING CONFIGURED");
            Debug.Log("========================================");
            Debug.Log($"  Custom keystore enabled: {PlayerSettings.Android.useCustomKeystore}");
            Debug.Log($"  Keystore path (stored): {storedKeystorePath}");
            Debug.Log($"  Keystore path (absolute): {keystorePath}");
            Debug.Log($"  Key alias: {keyAlias}");
            Debug.Log($"  Settings persisted: YES");
            Debug.Log("========================================");

            return new KeystoreConfig(keystorePath, keystorePass, keyAlias, resolvedKeyPass);
        }

        private static void VerifyKeystoreContainsAlias(string keystorePath, string keystorePass, string keyAlias)
        {
            string keytoolOutput;
            try
            {
                keytoolOutput = RunProcess(
                    "keytool",
                    $"-list -keystore \"{keystorePath}\" -alias \"{keyAlias}\" -storepass \"{keystorePass}\"",
                    "keytool (alias verification)"
                );
            }
            catch (Exception ex)
            {
                FailBuild($"AndroidBuild: Failed to verify keystore contains alias '{keyAlias}': {ex.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(keytoolOutput))
            {
                FailBuild($"AndroidBuild: keytool returned empty output when verifying alias '{keyAlias}' in keystore.");
            }

            // keytool -list with -alias exits 0 and prints alias info if found
            // If alias doesn't exist, keytool exits non-zero (caught by RunProcess)
            Debug.Log($"AndroidBuild: Verified keystore contains alias '{keyAlias}'.");
        }

        private static string ResolveKeystorePath(string keystorePathRaw)
        {
            string trimmed = keystorePathRaw.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, trimmed));
        }

        private static void EnsureReadableFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                FailBuild($"AndroidBuild: Required {label} file not found at '{path}'.");
            }

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    stream.ReadByte();
                }
            }
            catch (Exception ex)
            {
                FailBuild($"AndroidBuild: Required {label} file is not readable: {ex.Message}");
            }
        }

        private static void VerifyAabSigning(string aabPath, KeystoreConfig config)
        {
            Debug.Log("========================================");
            Debug.Log("POST-BUILD AAB SIGNING VERIFICATION");
            Debug.Log("========================================");

            if (config == null)
            {
                FailBuild("AndroidBuild: Missing keystore configuration for AAB verification.");
            }

            if (string.IsNullOrWhiteSpace(aabPath))
            {
                FailBuild("AndroidBuild: AAB path is empty; cannot verify signing.");
            }

            string resolvedAabPath = Path.GetFullPath(aabPath);
            Debug.Log($"  AAB path: {resolvedAabPath}");

            if (!File.Exists(resolvedAabPath))
            {
                FailBuild($"AndroidBuild: AAB not found at '{resolvedAabPath}'.");
            }

            Debug.Log("  Running jarsigner -verify...");
            string jarsignerOutput = RunProcess(
                "jarsigner",
                $"-verify -verbose -certs \"{resolvedAabPath}\"",
                "jarsigner"
            );

            if (string.IsNullOrWhiteSpace(jarsignerOutput))
            {
                FailBuild("AndroidBuild: jarsigner returned no output; cannot verify signing.");
            }

            // Check 1: Reject Android Debug signing
            if (jarsignerOutput.IndexOf("Android Debug", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError("========================================");
                Debug.LogError("AAB SIGNING VERIFICATION FAILED");
                Debug.LogError("========================================");
                Debug.LogError("REASON: AAB is signed with Android Debug certificate");
                Debug.LogError("");
                Debug.LogError("This means Unity ignored the custom keystore configuration");
                Debug.LogError("and fell back to debug signing.");
                Debug.LogError("");
                Debug.LogError("--- jarsigner output (truncated) ---");
                string[] lines = jarsignerOutput.Split('\n');
                for (int i = 0; i < Math.Min(lines.Length, 30); i++)
                {
                    if (lines[i].IndexOf("CN=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        lines[i].IndexOf("Android Debug", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.LogError(lines[i]);
                    }
                }
                Debug.LogError("========================================");
                FailBuild("AndroidBuild: FATAL - Release AAB is debug-signed. Google Play will reject this AAB.");
            }

            // Check 2: Verify SHA-256 fingerprint matches release keystore
            Debug.Log("  Extracting expected fingerprint from release keystore...");
            string keytoolOutput = RunProcess(
                "keytool",
                $"-list -v -keystore \"{config.KeystorePath}\" -alias \"{config.KeyAlias}\" -storepass \"{config.KeystorePass}\"",
                "keytool"
            );

            string expectedFingerprint = ExtractFingerprint(keytoolOutput, "SHA256");
            if (string.IsNullOrWhiteSpace(expectedFingerprint))
            {
                Debug.LogError("AndroidBuild: Unable to read SHA-256 fingerprint from keystore.");
                Debug.LogError(keytoolOutput);
                FailBuild("AndroidBuild: Release keystore fingerprint could not be determined.");
            }

            Debug.Log($"  Expected SHA-256: {expectedFingerprint}");

            // Check 3: Extract actual fingerprint from AAB signing certificate
            Debug.Log("  Extracting actual fingerprint from AAB signing certificate...");
            string actualFingerprint = ExtractAabSignerFingerprint(resolvedAabPath);
            if (string.IsNullOrWhiteSpace(actualFingerprint))
            {
                Debug.LogError("AndroidBuild: Unable to extract SHA-256 fingerprint from AAB signing certificate.");
                FailBuild("AndroidBuild: AAB signer fingerprint could not be determined.");
            }

            Debug.Log($"  Actual SHA-256: {actualFingerprint}");

            string normalizedExpected = NormalizeFingerprint(expectedFingerprint);
            string normalizedActual = NormalizeFingerprint(actualFingerprint);
            if (!normalizedExpected.Equals(normalizedActual, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("========================================");
                Debug.LogError("AAB SIGNING VERIFICATION FAILED");
                Debug.LogError("========================================");
                Debug.LogError("REASON: AAB signer fingerprint does not match release keystore");
                Debug.LogError($"Expected SHA-256: {expectedFingerprint}");
                Debug.LogError($"Actual SHA-256:   {actualFingerprint}");
                Debug.LogError("");
                Debug.LogError("This means the AAB was signed with a different keystore than expected.");
                Debug.LogError("========================================");
                FailBuild("AndroidBuild: Release AAB signer does not match the expected keystore.");
            }

            Debug.Log("========================================");
            Debug.Log("AAB SIGNING VERIFICATION PASSED");
            Debug.Log("========================================");
            Debug.Log($"  Signer: NOT Android Debug");
            Debug.Log($"  SHA-256 fingerprint: MATCHES release keystore");
            Debug.Log($"  Fingerprint: {actualFingerprint}");
            Debug.Log("========================================");
        }

        /// <summary>
        /// Extracts the SHA-256 fingerprint from the AAB's signing certificate.
        /// Uses unzip to extract META-INF/*.RSA and keytool to print the certificate.
        /// </summary>
        private static string ExtractAabSignerFingerprint(string aabPath)
        {
            try
            {
                // First, list the META-INF directory to find the .RSA file
                ProcessStartInfo listInfo = new ProcessStartInfo
                {
                    FileName = "unzip",
                    Arguments = $"-l \"{aabPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                string rsaFileName = null;
                using (Process listProc = Process.Start(listInfo))
                {
                    string output = listProc.StandardOutput.ReadToEnd();
                    listProc.WaitForExit();

                    // Look for META-INF/*.RSA file
                    foreach (string line in output.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Contains("META-INF/") && trimmed.EndsWith(".RSA"))
                        {
                            // Extract the filename from the line (last part)
                            string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                rsaFileName = parts[parts.Length - 1];
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(rsaFileName))
                {
                    Debug.LogError("AndroidBuild: Could not find META-INF/*.RSA file in AAB");
                    return null;
                }

                Debug.Log($"  Found signing certificate: {rsaFileName}");

                // Create a temp file for the RSA certificate
                string tempFile = Path.GetTempFileName();
                try
                {
                    // Extract the RSA file to temp
                    ProcessStartInfo extractInfo = new ProcessStartInfo
                    {
                        FileName = "unzip",
                        Arguments = $"-p \"{aabPath}\" \"{rsaFileName}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    byte[] rsaBytes;
                    using (Process extractProc = Process.Start(extractInfo))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            extractProc.StandardOutput.BaseStream.CopyTo(ms);
                            rsaBytes = ms.ToArray();
                        }
                        extractProc.WaitForExit();
                    }

                    File.WriteAllBytes(tempFile, rsaBytes);

                    // Use keytool to print the certificate info
                    ProcessStartInfo keytoolInfo = new ProcessStartInfo
                    {
                        FileName = "keytool",
                        Arguments = $"-printcert -file \"{tempFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (Process keytoolProc = Process.Start(keytoolInfo))
                    {
                        string keytoolOut = keytoolProc.StandardOutput.ReadToEnd();
                        keytoolProc.WaitForExit();

                        // Extract the SHA-256 fingerprint
                        return ExtractFingerprint(keytoolOut, "SHA256");
                    }
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AndroidBuild: Error extracting AAB signer fingerprint: {ex.Message}");
                return null;
            }
        }

        private static string RunProcess(string fileName, string arguments, string toolName)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        FailBuild($"AndroidBuild: Failed to start {toolName}.");
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    if (process.ExitCode != 0)
                    {
                        Debug.LogError($"AndroidBuild: {toolName} failed with exit code {process.ExitCode}.");
                        Debug.LogError(output);
                        Debug.LogError(error);
                        FailBuild($"AndroidBuild: {toolName} failed while verifying signing.");
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        output = string.Concat(output, Environment.NewLine, error);
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                FailBuild($"AndroidBuild: Failed to run {toolName}: {ex.Message}");
                return null;
            }
        }

        private static string ExtractFingerprint(string keytoolOutput, string algorithm)
        {
            if (string.IsNullOrWhiteSpace(keytoolOutput))
            {
                return null;
            }

            string[] lines = keytoolOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(algorithm, StringComparison.OrdinalIgnoreCase))
                {
                    int index = trimmed.IndexOf(':');
                    if (index >= 0 && index + 1 < trimmed.Length)
                    {
                        return trimmed.Substring(index + 1).Trim();
                    }
                }

                if (trimmed.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase))
                {
                    int index = trimmed.IndexOf(':');
                    if (index >= 0 && index + 1 < trimmed.Length)
                    {
                        return trimmed.Substring(index + 1).Trim();
                    }
                }
            }

            return null;
        }

        private static string NormalizeFingerprint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace(":", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("-", string.Empty)
                .ToUpperInvariant();

            return normalized;
        }

        private static void FailBuild(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            throw new InvalidOperationException(message);
        }

        private sealed class KeystoreConfig
        {
            public KeystoreConfig(string keystorePath, string keystorePass, string keyAlias, string keyPass)
            {
                KeystorePath = keystorePath;
                KeystorePass = keystorePass;
                KeyAlias = keyAlias;
                KeyPass = keyPass;
            }

            public string KeystorePath { get; }
            public string KeystorePass { get; }
            public string KeyAlias { get; }
            public string KeyPass { get; }
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
            // Command-line args (passed via customParameters in CI, reliable in Docker containers)
            string cmdVersion = GetCommandLineArg("-versionName");
            if (!string.IsNullOrWhiteSpace(cmdVersion))
            {
                return cmdVersion;
            }

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
            if (IsGitHubActionsEnvironment())
            {
                int? ciVersionCode = ResolveGitHubActionsVersionCode();
                if (!ciVersionCode.HasValue)
                {
                    FailBuild("AndroidBuild: GitHub Actions detected but VERSION_CODE could not be derived from GITHUB_RUN_NUMBER/GITHUB_RUN_ATTEMPT.");
                    return null;
                }

                string ciCmdCode = GetCommandLineArg("-versionCode");
                if (TryParsePositiveInt(ciCmdCode, out int ciCmdCodeInt) && ciCmdCodeInt != ciVersionCode.Value)
                {
                    FailBuild($"AndroidBuild: -versionCode ({ciCmdCodeInt}) does not match CI-derived version code ({ciVersionCode.Value}).");
                    return null;
                }

                string ciEnvVersionCode = FirstNonEmptyEnv("VERSION_CODE", "DECANTRA_VERSION_CODE");
                if (TryParsePositiveInt(ciEnvVersionCode, out int envCodeInt) && envCodeInt != ciVersionCode.Value)
                {
                    FailBuild($"AndroidBuild: VERSION_CODE env ({envCodeInt}) does not match CI-derived version code ({ciVersionCode.Value}).");
                    return null;
                }

                return ciVersionCode.Value;
            }

            // Command-line args (passed via customParameters in CI, reliable in Docker containers)
            string cmdCode = GetCommandLineArg("-versionCode");
            if (TryParsePositiveInt(cmdCode, out int cmdCodeInt))
            {
                return ClampVersionCode(cmdCodeInt);
            }

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

        private static int? ResolveGitHubActionsVersionCode()
        {
            string runNumberRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (!TryParsePositiveLong(runNumberRaw, out long runNumber))
            {
                return null;
            }

            long runAttempt = 1;
            string runAttemptRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT");
            if (TryParsePositiveLong(runAttemptRaw, out long attempt))
            {
                runAttempt = attempt;
            }

            long computed = 1000L + runNumber * 10L + Math.Max(0L, runAttempt - 1L);
            return ClampVersionCode(computed);
        }

        private static bool IsGitHubActionsEnvironment()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true",
                StringComparison.OrdinalIgnoreCase
            );
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

        private static string GetCommandLineArg(string argName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argName, StringComparison.Ordinal))
                {
                    return args[i + 1];
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

        private static bool TryParsePositiveLong(string value, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return long.TryParse(trimmed, out result) && result > 0;
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
