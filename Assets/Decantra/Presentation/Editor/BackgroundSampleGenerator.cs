/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.IO;
using Decantra.Domain.Background;
using Decantra.Domain.Rules;
using UnityEditor;
using UnityEngine;

namespace Decantra.Presentation.Editor
{
    /// <summary>
    /// Editor tool for generating background samples for quality inspection.
    /// Can be run from the menu or via batchmode for CI/build integration.
    /// </summary>
    public static class BackgroundSampleGenerator
    {
        private const int SampleWidth = 512;
        private const int SampleHeight = 256;
        private const string OutputPath = "doc/img/background-samples";

        /// <summary>
        /// Generate samples for all implemented archetypes via menu.
        /// </summary>
        [MenuItem("Decantra/Generate Background Samples")]
        public static void GenerateAllSamples()
        {
            GenerateSamplesInternal(interactive: true);
        }

        /// <summary>
        /// Batchmode entry point for CI/build scripts.
        /// </summary>
        public static void GenerateSamplesBatchmode()
        {
            Debug.Log("BackgroundSampleGenerator: Starting batchmode sample generation...");
            GenerateSamplesInternal(interactive: false);
            Debug.Log("BackgroundSampleGenerator: Batchmode sample generation complete.");
        }

        private static void GenerateSamplesInternal(bool interactive)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputDir = Path.Combine(projectRoot, OutputPath);

            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Debug.Log($"Created output directory: {outputDir}");
            }

            int totalSamples = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();

            // Generate samples for each implemented archetype
            foreach (var archetype in BackgroundGeneratorRegistry.GetImplementedArchetypes())
            {
                if (interactive)
                {
                    EditorUtility.DisplayProgressBar(
                        "Generating Background Samples",
                        $"Generating {archetype}...",
                        (float)totalSamples / 9f);
                }

                // Generate 3 samples per archetype (different zones)
                for (int zone = 0; zone < 3; zone++)
                {
                    ulong seed = (ulong)(archetype.GetHashCode() ^ (zone * 0x9E3779B9));
                    GenerateSample(archetype, zone, seed, outputDir);
                    totalSamples++;
                }
            }

            // Generate one "theme transition" sample showing all three archetypes
            GenerateThemeTransitionSample(outputDir);
            totalSamples++;

            timer.Stop();

            if (interactive)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Background Sample Generation Complete",
                    $"Generated {totalSamples} samples in {timer.Elapsed.TotalSeconds:F1}s\n\nOutput: {outputDir}",
                    "OK");
            }

            Debug.Log($"Generated {totalSamples} background samples in {timer.Elapsed.TotalMilliseconds:F0}ms");
            Debug.Log($"Output directory: {outputDir}");
        }

        private static void GenerateSample(GeneratorArchetype archetype, int zone, ulong seed, string outputDir)
        {
            var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);

            // Get appropriate parameters for macro layer (most visible)
            var parameters = BackgroundGeneratorRegistry.GetDefaultParameters(archetype, ScaleBand.Macro);

            var sampleTimer = System.Diagnostics.Stopwatch.StartNew();
            float[] field = generator.Generate(SampleWidth, SampleHeight, parameters, seed);
            sampleTimer.Stop();

            // Convert to texture with a pleasant color palette
            var texture = CreateColoredTexture(field, SampleWidth, SampleHeight, archetype, zone);

            // Save as PNG
            string filename = $"{archetype}_zone{zone}_seed{seed:X8}.png";
            string filepath = Path.Combine(outputDir, filename);
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filepath, pngData);

            // Clean up
            UnityEngine.Object.DestroyImmediate(texture);

            Debug.Log($"  Generated {filename} ({sampleTimer.Elapsed.TotalMilliseconds:F0}ms)");
        }

        private static void GenerateThemeTransitionSample(string outputDir)
        {
            // Create a wide image showing all three archetypes side by side
            int totalWidth = SampleWidth * 3;
            var combinedTexture = new Texture2D(totalWidth, SampleHeight, TextureFormat.RGBA32, false);

            var archetypes = new[]
            {
                GeneratorArchetype.AtmosphericWash,
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection
            };

            for (int i = 0; i < archetypes.Length; i++)
            {
                var generator = BackgroundGeneratorRegistry.GetGenerator(archetypes[i]);
                var parameters = BackgroundGeneratorRegistry.GetDefaultParameters(archetypes[i], ScaleBand.Macro);
                ulong seed = (ulong)(0xDEADBEEF + i * 0x12345678);

                float[] field = generator.Generate(SampleWidth, SampleHeight, parameters, seed);
                var sectionTexture = CreateColoredTexture(field, SampleWidth, SampleHeight, archetypes[i], i);

                // Copy to combined texture
                var pixels = sectionTexture.GetPixels(0, 0, SampleWidth, SampleHeight);
                combinedTexture.SetPixels(i * SampleWidth, 0, SampleWidth, SampleHeight, pixels);

                UnityEngine.Object.DestroyImmediate(sectionTexture);
            }

            combinedTexture.Apply();

            // Save
            string filepath = Path.Combine(outputDir, "theme_transitions.png");
            byte[] pngData = combinedTexture.EncodeToPNG();
            File.WriteAllBytes(filepath, pngData);

            UnityEngine.Object.DestroyImmediate(combinedTexture);
            Debug.Log($"  Generated theme_transitions.png");
        }

        private static Texture2D CreateColoredTexture(float[] field, int width, int height, GeneratorArchetype archetype, int zoneVariant)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var colors = new Color32[field.Length];

            // Select color palette based on archetype and zone variant
            Color baseColor, accentColor;
            GetPalette(archetype, zoneVariant, out baseColor, out accentColor);

            for (int i = 0; i < field.Length; i++)
            {
                float alpha = field[i];
                // Blend between base and accent based on alpha
                Color c = Color.Lerp(baseColor, accentColor, alpha * alpha); // Squared for more contrast
                c.a = 0.4f + alpha * 0.6f; // Semi-transparent base, more opaque highlights

                colors[i] = c;
            }

            texture.SetPixels32(colors);
            texture.Apply();
            return texture;
        }

        private static void GetPalette(GeneratorArchetype archetype, int variant, out Color baseColor, out Color accentColor)
        {
            // Each archetype gets a distinct color family
            switch (archetype)
            {
                case GeneratorArchetype.AtmosphericWash:
                    // Warm sunset/sunrise tones
                    if (variant == 0)
                    {
                        baseColor = new Color(0.15f, 0.08f, 0.18f);   // Deep purple
                        accentColor = new Color(0.85f, 0.45f, 0.35f); // Warm coral
                    }
                    else if (variant == 1)
                    {
                        baseColor = new Color(0.12f, 0.15f, 0.22f);   // Deep blue
                        accentColor = new Color(0.95f, 0.65f, 0.40f); // Golden amber
                    }
                    else
                    {
                        baseColor = new Color(0.18f, 0.12f, 0.10f);   // Deep brown
                        accentColor = new Color(0.75f, 0.55f, 0.45f); // Dusty rose
                    }
                    break;

                case GeneratorArchetype.DomainWarpedClouds:
                    // Cool ethereal tones
                    if (variant == 0)
                    {
                        baseColor = new Color(0.08f, 0.12f, 0.20f);   // Deep navy
                        accentColor = new Color(0.50f, 0.70f, 0.85f); // Sky blue
                    }
                    else if (variant == 1)
                    {
                        baseColor = new Color(0.10f, 0.18f, 0.15f);   // Deep teal
                        accentColor = new Color(0.45f, 0.80f, 0.70f); // Aqua
                    }
                    else
                    {
                        baseColor = new Color(0.15f, 0.10f, 0.20f);   // Deep violet
                        accentColor = new Color(0.70f, 0.55f, 0.85f); // Lavender
                    }
                    break;

                case GeneratorArchetype.CurlFlowAdvection:
                default:
                    // Dynamic flowing tones
                    if (variant == 0)
                    {
                        baseColor = new Color(0.12f, 0.08f, 0.15f);   // Deep magenta
                        accentColor = new Color(0.90f, 0.40f, 0.60f); // Hot pink
                    }
                    else if (variant == 1)
                    {
                        baseColor = new Color(0.08f, 0.15f, 0.12f);   // Deep green
                        accentColor = new Color(0.40f, 0.85f, 0.55f); // Bright green
                    }
                    else
                    {
                        baseColor = new Color(0.18f, 0.10f, 0.08f);   // Deep rust
                        accentColor = new Color(0.95f, 0.55f, 0.25f); // Orange
                    }
                    break;
            }
        }
    }
}
