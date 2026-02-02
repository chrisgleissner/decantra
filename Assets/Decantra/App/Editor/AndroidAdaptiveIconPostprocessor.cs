/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Decantra.App.Editor
{
    public sealed class AndroidAdaptiveIconPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string rootPath = path;
            if (string.Equals(Path.GetFileName(path), "unityLibrary", System.StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo parent = Directory.GetParent(path);
                if (parent != null)
                {
                    rootPath = parent.FullName;
                }
            }

            string launcherResPath = Path.Combine(rootPath, "launcher", "src", "main", "res");
            if (!Directory.Exists(launcherResPath))
            {
                Debug.LogWarning($"AndroidAdaptiveIconPostprocessor: launcher res path not found: {launcherResPath}");
                return;
            }

            string foregroundSource = Path.Combine(Application.dataPath, "Icons", "logo.png");
            string backgroundSource = Path.Combine(Application.dataPath, "Icons", "AdaptiveIconBackground.png");

            if (!File.Exists(foregroundSource) || !File.Exists(backgroundSource))
            {
                Debug.LogWarning("AndroidAdaptiveIconPostprocessor: Adaptive icon source assets are missing.");
                return;
            }

            ReplaceAdaptiveIconLayer(launcherResPath, "ic_launcher_foreground.png", foregroundSource);
            ReplaceAdaptiveIconLayer(launcherResPath, "ic_launcher_background.png", backgroundSource);
        }

        private static void ReplaceAdaptiveIconLayer(string launcherResPath, string fileName, string sourcePath)
        {
            string[] targetFiles = Directory.GetFiles(launcherResPath, fileName, SearchOption.AllDirectories);
            if (targetFiles.Length == 0)
            {
                Debug.LogWarning($"AndroidAdaptiveIconPostprocessor: No targets found for {fileName}");
                return;
            }

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!sourceTexture.LoadImage(sourceBytes, false))
            {
                Debug.LogWarning($"AndroidAdaptiveIconPostprocessor: Failed to load {sourcePath}");
                Object.DestroyImmediate(sourceTexture);
                return;
            }

            sourceTexture.filterMode = FilterMode.Bilinear;

            foreach (string targetFile in targetFiles)
            {
                Texture2D targetTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!targetTexture.LoadImage(File.ReadAllBytes(targetFile), false))
                {
                    Debug.LogWarning($"AndroidAdaptiveIconPostprocessor: Failed to read {targetFile}");
                    Object.DestroyImmediate(targetTexture);
                    continue;
                }

                int width = targetTexture.width;
                int height = targetTexture.height;
                Object.DestroyImmediate(targetTexture);

                Texture2D resized = ResizeTexture(sourceTexture, width, height);
                byte[] output = resized.EncodeToPNG();
                File.WriteAllBytes(targetFile, output);
                Object.DestroyImmediate(resized);
            }

            Object.DestroyImmediate(sourceTexture);
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture active = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }
    }
}
