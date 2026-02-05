/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Reflection;
using Decantra.Domain.Background;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.PlayMode.Tests
{
    /// <summary>
    /// Sanity checks to prevent flat or near-black backgrounds.
    /// </summary>
    public sealed class BackgroundSanityTests
    {
        private const int CompositeWidth = 128;
        private const int CompositeHeight = 256;
        private const int TestSeed = unchecked((int)0xC0FFEE12);

        [UnityTest]
        public IEnumerator BackgroundComposite_Level1_HasVisibleStructure()
        {
            yield return RunCompositeCheck(1);
        }

        [UnityTest]
        public IEnumerator BackgroundComposite_Level12_HasVisibleStructure()
        {
            yield return RunCompositeCheck(12);
        }

        [UnityTest]
        public IEnumerator BackgroundComposite_Level24_HasVisibleStructure()
        {
            yield return RunCompositeCheck(24);
        }

        private static IEnumerator RunCompositeCheck(int levelIndex)
        {
            var controller = CreateController();
            try
            {
                ApplyBackgroundVariation(controller, levelIndex, TestSeed);

                var composite = CompositeBackground(controller);
                ComputeLuminanceMetrics(composite, out float variance, out float brightRatio);

                // Deterministic thresholds: reject flat or near-black backgrounds.
                Assert.Greater(variance, 0.0001f, $"Level {levelIndex} luminance variance too low: {variance:F6}");
                Assert.Greater(brightRatio, 0.005f, $"Level {levelIndex} bright pixel ratio too low: {brightRatio:P2}");
            }
            finally
            {
                if (controller != null)
                {
                    Object.Destroy(controller.gameObject);
                }
            }

            yield return null;
        }

        private static GameController CreateController()
        {
            var controllerGo = new GameObject("BackgroundTestController");
            var controller = controllerGo.AddComponent<GameController>();

            SetPrivateField(controller, "backgroundImage", CreateImage("Base"));
            SetPrivateField(controller, "backgroundDetail", CreateImage("Detail"));
            SetPrivateField(controller, "backgroundFlow", CreateImage("Flow"));
            SetPrivateField(controller, "backgroundShapes", CreateImage("Shapes"));
            SetPrivateField(controller, "backgroundBubbles", CreateImage("Bubbles"));
            SetPrivateField(controller, "backgroundLargeStructure", CreateImage("Large"));
            SetPrivateField(controller, "backgroundVignette", CreateImage("Vignette"));

            return controller;
        }

        private static Image CreateImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var image = go.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static void ApplyBackgroundVariation(GameController controller, int levelIndex, int seed)
        {
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForLevel(levelIndex, seed);
            SetPrivateField(controller, "_currentBackgroundFamily", (int)archetype);

            var method = typeof(GameController).GetMethod("ApplyBackgroundVariation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ApplyBackgroundVariation method not found");
            method.Invoke(controller, new object[] { levelIndex, seed, -1 });
        }

        private static Color[] CompositeBackground(GameController controller)
        {
            var baseImage = GetPrivateField<Image>(controller, "backgroundImage");
            var largeImage = GetPrivateField<Image>(controller, "backgroundLargeStructure");
            var flowImage = GetPrivateField<Image>(controller, "backgroundFlow");
            var shapesImage = GetPrivateField<Image>(controller, "backgroundShapes");
            var bubblesImage = GetPrivateField<Image>(controller, "backgroundBubbles");
            var detailImage = GetPrivateField<Image>(controller, "backgroundDetail");

            var pixels = new Color[CompositeWidth * CompositeHeight];

            for (int y = 0; y < CompositeHeight; y++)
            {
                float v = (y + 0.5f) / CompositeHeight;
                for (int x = 0; x < CompositeWidth; x++)
                {
                    float u = (x + 0.5f) / CompositeWidth;
                    int idx = y * CompositeWidth + x;

                    var baseColor = SampleLayer(baseImage, u, v);
                    var color = baseColor;
                    color = BlendOver(color, SampleLayer(largeImage, u, v));
                    color = BlendOver(color, SampleLayer(flowImage, u, v));
                    color = BlendOver(color, SampleLayer(shapesImage, u, v));
                    color = BlendOver(color, SampleLayer(bubblesImage, u, v));
                    color = BlendOver(color, SampleLayer(detailImage, u, v));

                    pixels[idx] = color;
                }
            }

            return pixels;
        }

        private static Color SampleLayer(Image image, float u, float v)
        {
            if (image == null || image.sprite == null)
            {
                return Color.clear;
            }

            float alpha = SampleSpriteAlpha(image.sprite, u, v);
            float finalAlpha = alpha * image.color.a;
            return new Color(image.color.r, image.color.g, image.color.b, finalAlpha);
        }

        private static float SampleSpriteAlpha(Sprite sprite, float u, float v)
        {
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            float x = rect.x + rect.width * u;
            float y = rect.y + rect.height * v;
            float texU = x / texture.width;
            float texV = y / texture.height;
            return texture.GetPixelBilinear(texU, texV).a;
        }

        private static Color BlendOver(Color under, Color over)
        {
            float a = over.a;
            float inv = 1f - a;
            return new Color(
                over.r * a + under.r * inv,
                over.g * a + under.g * inv,
                over.b * a + under.b * inv,
                a + under.a * inv);
        }

        private static void ComputeLuminanceMetrics(Color[] pixels, out float variance, out float brightRatio)
        {
            float sum = 0f;
            float sumSq = 0f;
            int brightCount = 0;
            float threshold = 0.05f;

            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                sum += lum;
                sumSq += lum * lum;
                if (lum > threshold)
                {
                    brightCount++;
                }
            }

            float mean = sum / pixels.Length;
            variance = sumSq / pixels.Length - mean * mean;
            brightRatio = brightCount / (float)pixels.Length;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found");
            return field.GetValue(target) as T;
        }
    }
}
