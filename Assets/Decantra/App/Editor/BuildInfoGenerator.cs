/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class BuildInfoGenerator
    {
        private const string BuildInfoPath = "Assets/Decantra/App/Runtime/BuildInfo.cs";

        public static void GenerateAndImport()
        {
            string version = ResolveVersionName();
            string buildUtc = ResolveBuildUtc();
            string revision = ResolveRevision();

            string content =
$"/*\n" +
"Decantra - A Unity-based bottle-sorting puzzle game\n" +
"Copyright (C) 2026 Christian Gleissner\n\n" +
"Licensed under the GNU General Public License v2.0 or later.\n" +
"See <https://www.gnu.org/licenses/> for details.\n" +
"*/\n\n" +
"namespace Decantra.App\n" +
"{\n" +
"    public static class BuildInfo\n" +
"    {\n" +
$"        public const string Version = \"{Escape(version)}\";\n" +
$"        public const string BuildUtc = \"{Escape(buildUtc)}\";\n" +
$"        public const string Revision = \"{Escape(revision)}\";\n" +
"    }\n" +
"}\n";

            Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoPath) ?? "Assets/Decantra/App/Runtime");
            File.WriteAllText(BuildInfoPath, content);
            AssetDatabase.ImportAsset(BuildInfoPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            Debug.Log($"BuildInfoGenerator: Version={version} BuildUtc={buildUtc} Revision={revision}");
        }

        public static string ResolveVersionName()
        {
            string commandLineVersion = GetCommandLineArg("-versionName");
            if (!string.IsNullOrWhiteSpace(commandLineVersion))
            {
                return commandLineVersion.Trim();
            }

            string envVersion = FirstNonEmptyEnv("VERSION_NAME", "DECANTRA_VERSION_NAME", "GITHUB_REF_NAME");
            if (!string.IsNullOrWhiteSpace(envVersion))
            {
                return envVersion.Trim();
            }

            return string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "0.0.0-local" : PlayerSettings.bundleVersion.Trim();
        }

        private static string ResolveBuildUtc()
        {
            string envUtc = FirstNonEmptyEnv("BUILD_UTC", "DECANTRA_BUILD_UTC");
            if (!string.IsNullOrWhiteSpace(envUtc)
                && DateTime.TryParse(envUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedEnv))
            {
                return parsedEnv.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            }

            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        }

        private static string ResolveRevision()
        {
            string commandLineRevision = GetCommandLineArg("-buildRevision");
            if (!string.IsNullOrWhiteSpace(commandLineRevision))
            {
                return commandLineRevision.Trim();
            }

            string envRevision = FirstNonEmptyEnv("BUILD_REVISION", "DECANTRA_BUILD_REVISION", "VERSION_CODE", "DECANTRA_VERSION_CODE", "GITHUB_RUN_NUMBER");
            if (!string.IsNullOrWhiteSpace(envRevision))
            {
                return envRevision.Trim();
            }

            string githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA");
            if (!string.IsNullOrWhiteSpace(githubSha))
            {
                string trimmed = githubSha.Trim();
                return trimmed.Length <= 8 ? trimmed : trimmed.Substring(0, 8);
            }

            return "local";
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string FirstNonEmptyEnv(params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string value = Environment.GetEnvironmentVariable(names[i]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
