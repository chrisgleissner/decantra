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
    public static class WebGlBuild
    {
        private const string DefaultWebGlBuildPath = "Builds/WebGL";

        [MenuItem("Decantra/Build/WebGL Release")]
        public static void BuildRelease()
        {
            string outputPath = ResolveBuildPath();

            Directory.CreateDirectory(outputPath);

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            PlayerSettings.productName = "Decantra";
            PlayerSettings.applicationIdentifier = "uk.gleissner.decantra";
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

            Debug.Log($"WebGlBuild: build completed at {outputPath}");
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