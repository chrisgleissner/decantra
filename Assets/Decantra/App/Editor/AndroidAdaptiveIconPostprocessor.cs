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
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color[] srcPixels = source.GetPixels();
            Color[] dstPixels = new Color[width * height];

            float xRatio = width > 1
                ? (source.width - 1f) / (width - 1f)
                : 0f;
            float yRatio = height > 1
                ? (source.height - 1f) / (height - 1f)
                : 0f;

            for (int y = 0; y < height; y++)
            {
                float gy = y * yRatio;
                int y0 = Mathf.Clamp((int)gy, 0, source.height - 1);
                int y1 = Mathf.Min(y0 + 1, source.height - 1);
                float fy = gy - y0;

                for (int x = 0; x < width; x++)
                {
                    float gx = x * xRatio;
                    int x0 = Mathf.Clamp((int)gx, 0, source.width - 1);
                    int x1 = Mathf.Min(x0 + 1, source.width - 1);
                    float fx = gx - x0;

                    Color c00 = srcPixels[y0 * source.width + x0];
                    Color c10 = srcPixels[y0 * source.width + x1];
                    Color c01 = srcPixels[y1 * source.width + x0];
                    Color c11 = srcPixels[y1 * source.width + x1];

                    Color c0 = Color.Lerp(c00, c10, fx);
                    Color c1 = Color.Lerp(c01, c11, fx);
                    dstPixels[y * width + x] = Color.Lerp(c0, c1, fy);
                }
            }

            result.SetPixels(dstPixels);
            result.Apply();
            return result;
        }
    }
}
