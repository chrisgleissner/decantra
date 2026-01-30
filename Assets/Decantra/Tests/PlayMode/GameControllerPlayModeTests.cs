using System.Collections;
using System.IO;
using System.Reflection;
using Decantra.App.Services;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Solver;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public class GameControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameController_StartsAndRenders()
        {
            var go = new GameObject("GameController");
            var controller = go.AddComponent<GameController>();
            yield return null;
            Assert.IsNotNull(controller);
        }

        [UnityTest]
        public IEnumerator CaptureGameplayScreenshot()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Screenshot capture is not supported in batch mode.");
            }

            SceneBootstrap.EnsureScene();
            yield return new WaitForSeconds(5f);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDir = Path.Combine(projectRoot, "doc", "img");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string filePath = Path.Combine(outputDir, "playmode-gameplay.png");
            ScreenCapture.CaptureScreenshot(filePath);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(File.Exists(filePath), $"Screenshot not found at {filePath}");
        }

        [UnityTest]
        public IEnumerator BackgroundVariation_ChangesWithLevel()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            var field = typeof(GameController).GetField("backgroundImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);

            var image = field.GetValue(controller) as UnityEngine.UI.Image;
            Assert.IsNotNull(image);

            var first = image.color;
            controller.LoadLevel(3, 12345);
            yield return null;
            var second = image.color;

            Assert.AreNotEqual(first, second);
        }

        [UnityTest]
        public IEnumerator BottleViews_ArePresent()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottles = Object.FindObjectsOfType<Decantra.Presentation.View.BottleView>();
            Assert.GreaterOrEqual(bottles.Length, 6);
        }

        [UnityTest]
        public IEnumerator OutOfMoves_ShowsFailureAndBlocksInput()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(1, 123);
            yield return null;

            var generated = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(generated);
            var failState = new LevelState(generated.Bottles, 0, 0, generated.OptimalMoves, generated.LevelIndex, generated.Seed);
            SetPrivateField(controller, "_state", failState);
            SetPrivateField(controller, "_inputLocked", false);
            InvokePrivate(controller, "Render");

            Assert.IsTrue(TryFindValidMove(failState, out int source, out int target));
            bool started = controller.TryStartMove(source, target, out float duration);
            Assert.IsTrue(started);

            yield return new WaitForSeconds(duration + 0.2f);

            Assert.IsTrue(controller.IsInputLocked, "Input should be blocked after running out of moves.");

            var banner = GetPrivateField(controller, "outOfMovesBanner") as Decantra.Presentation.OutOfMovesBanner;
            Assert.IsNotNull(banner, "Out-of-moves banner should exist.");
            Assert.IsTrue(banner.IsVisible, "Out-of-moves banner should be visible after failure.");
        }

        [UnityTest]
        public IEnumerator OutOfMoves_RestartsLevelWithSameState()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(1, 777);
            yield return null;

            var generated = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(generated);
            string initialKey = StateEncoder.Encode(generated);

            var failState = new LevelState(generated.Bottles, 0, 0, generated.OptimalMoves, generated.LevelIndex, generated.Seed);
            SetPrivateField(controller, "_state", failState);
            SetPrivateField(controller, "_inputLocked", false);
            InvokePrivate(controller, "Render");

            Assert.IsTrue(TryFindValidMove(failState, out int source, out int target));
            bool started = controller.TryStartMove(source, target, out float duration);
            Assert.IsTrue(started);

            yield return new WaitForSeconds(duration + 1.0f);

            var currentState = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(currentState);
            string currentKey = StateEncoder.Encode(currentState);

            Assert.AreEqual(initialKey, currentKey, "Level state should reset to original after failure.");
            Assert.AreEqual(0, currentState.MovesUsed);
        }

        [UnityTest]
        public IEnumerator Failure_DoesNotUnlockNextLevel()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            string root = Path.Combine(Path.GetTempPath(), "decantra-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "progress.json");

            var store = new ProgressStore(new[] { path });
            var progress = new ProgressData
            {
                HighestUnlockedLevel = 3,
                CurrentLevel = 3,
                CurrentSeed = 555,
                CurrentScore = 0,
                HighScore = 0
            };

            SetPrivateField(controller, "_progressStore", store);
            SetPrivateField(controller, "_progress", progress);
            SetPrivateField(controller, "_currentLevel", 3);
            SetPrivateField(controller, "_currentSeed", 555);

            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null }),
                new Bottle(new ColorId?[4])
            }, 0, 0, 1, 3, 555);

            SetPrivateField(controller, "_state", state);
            SetPrivateField(controller, "_inputLocked", false);
            InvokePrivate(controller, "Render");

            Assert.IsTrue(TryFindValidMove(state, out int source, out int target));
            bool started = controller.TryStartMove(source, target, out float duration);
            Assert.IsTrue(started);

            yield return new WaitForSeconds(duration + 0.8f);

            Assert.AreEqual(3, progress.HighestUnlockedLevel, "Failure should not unlock the next level.");
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {name} not found");
            field.SetValue(instance, value);
        }

        private static object GetPrivateField(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {name} not found");
            return field.GetValue(instance);
        }

        private static void InvokePrivate(object instance, string name)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method {name} not found");
            method.Invoke(instance, null);
        }

        private static bool TryFindValidMove(LevelState state, out int source, out int target)
        {
            source = -1;
            target = -1;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    if (state.Bottles[i].MaxPourAmountInto(state.Bottles[j]) > 0)
                    {
                        source = i;
                        target = j;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
