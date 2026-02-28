/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Decantra.App.Editor
{
    public static class WebGlBuild
    {
        private const string DefaultWebGlBuildPath = "Builds/WebGL";
        private const string WebGlTemplateName = "PROJECT:DecantraResponsive";

        [MenuItem("Decantra/Build/WebGL Release")]
        public static void BuildRelease()
        {
            string outputPath = ResolveBuildPath();

            Directory.CreateDirectory(outputPath);

            ConfigureVersioningFromEnv();
            BuildInfoGenerator.GenerateAndImport();

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = WebGlTemplateName;
            AssetDatabase.SaveAssets();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Decantra/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"WebGlBuild: exporting WebGL build to {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"WebGL build failed: {report.summary.result}");
            }

            InjectVersionMarker(outputPath);

            Debug.Log($"WebGlBuild: build completed at {outputPath}");
        }

        private static void ConfigureVersioningFromEnv()
        {
            string versionName = BuildInfoGenerator.ResolveVersionName();
            if (!string.IsNullOrWhiteSpace(versionName))
            {
                PlayerSettings.bundleVersion = versionName.Trim();
            }
        }

        private static void InjectVersionMarker(string outputPath)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexPath))
            {
                return;
            }

            string revision = ResolveBuildRevision();
            string revisionLabel = string.IsNullOrWhiteSpace(revision) ? string.Empty : $" ({revision})";

            string html = File.ReadAllText(indexPath);
            html = html.Replace("__DECANTRA_BUILD_REVISION__", revision ?? string.Empty);
            html = html.Replace("__DECANTRA_BUILD_REVISION_LABEL__", revisionLabel);

            html = Regex.Replace(
                html,
                "<title>.*?</title>",
                $"<title>Decantra WebGL {PlayerSettings.bundleVersion}{revisionLabel}</title>",
                RegexOptions.Singleline);

            File.WriteAllText(indexPath, html);
        }

        private static string ResolveBuildRevision()
        {
            string commandLineRevision = GetCommandLineArg("-buildRevision");
            if (!string.IsNullOrWhiteSpace(commandLineRevision))
            {
                return commandLineRevision.Trim();
            }

            string githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA");
            if (!string.IsNullOrWhiteSpace(githubSha))
            {
                return githubSha.Trim().Length <= 8 ? githubSha.Trim() : githubSha.Trim().Substring(0, 8);
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

        private static string ResolveBuildPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outputPath = DefaultWebGlBuildPath;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildPath", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath = args[i + 1];
                }
            }

            return string.IsNullOrWhiteSpace(outputPath) ? DefaultWebGlBuildPath : outputPath;
        }
    }
}
