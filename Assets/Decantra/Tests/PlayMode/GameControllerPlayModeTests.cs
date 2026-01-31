using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Decantra.App.Services;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
using Decantra.Domain.Scoring;
using Decantra.Domain.Solver;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
            Object.DestroyImmediate(go);
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

            var bottles = Object.FindObjectsByType<Decantra.Presentation.View.BottleView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        [UnityTest]
        public IEnumerator ResetButton_RestoresInitialStateAndScore()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(1, 123);
            yield return null;

            var initial = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(initial);
            string initialKey = StateEncoder.Encode(initial);

            var session = GetPrivateField(controller, "_scoreSession") as ScoreSession;
            Assert.IsNotNull(session);
            int startTotal = session.TotalScore;

            Assert.IsTrue(TryFindValidMove(initial, out int source, out int target));
            bool started = controller.TryStartMove(source, target, out float duration);
            Assert.IsTrue(started);
            yield return new WaitForSeconds(duration + 0.2f);

            var midSession = GetPrivateField(controller, "_scoreSession") as ScoreSession;
            Assert.IsNotNull(midSession);
            Assert.Greater(midSession.ProvisionalScore, 0);
            Assert.AreEqual(startTotal, midSession.TotalScore);

            var resetGo = GameObject.Find("ResetButton");
            Assert.IsNotNull(resetGo, "Reset button should exist in scene.");
            var button = resetGo.GetComponent<Button>();
            Assert.IsNotNull(button, "Reset button should have Button component.");
            button.onClick.Invoke();

            yield return null;

            var resetState = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(resetState);
            string resetKey = StateEncoder.Encode(resetState);
            Assert.AreEqual(initialKey, resetKey, "Reset should restore the original level layout.");
            Assert.AreEqual(0, resetState.MovesUsed);

            var resetSession = GetPrivateField(controller, "_scoreSession") as ScoreSession;
            Assert.IsNotNull(resetSession);
            Assert.AreEqual(startTotal, resetSession.TotalScore);
            Assert.AreEqual(0, resetSession.ProvisionalScore);
        }

        [UnityTest]
        public IEnumerator ResetButton_PreservesBackground()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(3, 12345);
            yield return null;

            var imageField = typeof(GameController).GetField("backgroundImage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(imageField);
            var image = imageField.GetValue(controller) as Image;
            Assert.IsNotNull(image);

            var initialColor = image.color;

            var resetGo = GameObject.Find("ResetButton");
            Assert.IsNotNull(resetGo, "Reset button should exist in scene.");
            var button = resetGo.GetComponent<Button>();
            Assert.IsNotNull(button, "Reset button should have Button component.");
            button.onClick.Invoke();

            yield return null;

            var afterColor = image.color;
            Assert.AreEqual(initialColor, afterColor, "Reset should keep the same background.");
        }

        [UnityTest]
        public IEnumerator SinkBottle_CannotStartDrag_ButNormalBottleCan()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(24, 901);
            yield return null;

            var state = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(state);

            int sinkIndex = -1;
            int normalIndex = -1;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                if (state.Bottles[i].IsSink)
                {
                    sinkIndex = i;
                }
                else if (normalIndex < 0)
                {
                    normalIndex = i;
                }
            }

            Assert.GreaterOrEqual(sinkIndex, 0, "Expected a sink bottle by level 24.");
            Assert.GreaterOrEqual(normalIndex, 0, "Expected a normal bottle.");

            var sinkInput = GetBottleInputForIndex(controller, sinkIndex);
            var normalInput = GetBottleInputForIndex(controller, normalIndex);

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            };

            sinkInput.OnBeginDrag(eventData);
            Assert.IsFalse(sinkInput.IsDragging, "Sink bottle should not start dragging.");

            normalInput.OnBeginDrag(eventData);
            Assert.IsTrue(normalInput.IsDragging, "Normal bottle should start dragging.");
            normalInput.OnEndDrag(eventData);

            yield return null;
        }

        [UnityTest]
        public IEnumerator RestartGame_ConfirmationResetsProgress()
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
                HighestUnlockedLevel = 7,
                CurrentLevel = 7,
                CurrentSeed = 777,
                CurrentScore = 120,
                HighScore = 200
            };

            SetPrivateField(controller, "_progressStore", store);
            SetPrivateField(controller, "_progress", progress);

            var restartGo = GameObject.Find("RestartButton");
            Assert.IsNotNull(restartGo, "Restart button should exist in scene.");
            var restartButton = restartGo.GetComponent<Button>();
            Assert.IsNotNull(restartButton, "Restart button should have Button component.");
            restartButton.onClick.Invoke();

            yield return null;

            var dialog = GetPrivateField(controller, "restartDialog");
            Assert.IsNotNull(dialog, "Restart dialog should be wired.");

            var confirmGo = GameObject.Find("ConfirmRestartButton");
            Assert.IsNotNull(confirmGo, "Confirm restart button should exist.");
            var confirmButton = confirmGo.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Confirm restart button should have Button component.");
            confirmButton.onClick.Invoke();

            yield return null;

            var updated = GetPrivateField(controller, "_progress") as ProgressData;
            Assert.IsNotNull(updated);
            Assert.AreEqual(1, updated.HighestUnlockedLevel);
            Assert.AreEqual(1, updated.CurrentLevel);
            Assert.AreEqual(0, updated.CurrentScore);
            Assert.AreEqual(0, updated.HighScore);
        }

        [UnityTest]
        public IEnumerator Win_CommitsScoreAndAdvancesLevel()
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
                HighestUnlockedLevel = 1,
                CurrentLevel = 1,
                CurrentSeed = 111,
                CurrentScore = 100,
                HighScore = 100
            };

            SetPrivateField(controller, "_progressStore", store);
            SetPrivateField(controller, "_progress", progress);
            SetPrivateField(controller, "_currentLevel", 1);
            SetPrivateField(controller, "_currentSeed", 111);

            var session = new ScoreSession(progress.CurrentScore);
            session.BeginAttempt(progress.CurrentScore);
            SetPrivateField(controller, "_scoreSession", session);

            var solved = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 10, 0, 1, 111);

            SetPrivateField(controller, "_state", solved);
            SetPrivateField(controller, "_initialState", solved);
            SetPrivateField(controller, "_isCompleting", false);
            SetPrivateField(controller, "_isFailing", false);

            var nextState = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[4])
            }, 0, 10, 0, 2, 222);

            SetPrivateField(controller, "_nextLevel", 2);
            SetPrivateField(controller, "_nextSeed", 222);
            SetPrivateField(controller, "_nextState", nextState);
            SetPrivateField(controller, "_precomputeTask", Task.FromResult(nextState));

            SetPrivateField(controller, "levelBanner", null);
            SetPrivateField(controller, "introBanner", null);

            InvokePrivate(controller, "Render");

            yield return new WaitForSeconds(0.8f);

            Assert.Greater(progress.CurrentScore, 100, "Winning should commit score.");
            Assert.GreaterOrEqual(progress.HighScore, progress.CurrentScore);
            Assert.GreaterOrEqual(progress.CurrentLevel, 2, "Winning should advance level.");
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
                    if (MoveRules.GetPourAmount(state, i, j) > 0)
                    {
                        source = i;
                        target = j;
                        return true;
                    }
                }
            }
            return false;
        }

        private static Decantra.Presentation.View.BottleInput GetBottleInputForIndex(GameController controller, int index)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "bottleViews field not found");
            var views = field.GetValue(controller) as System.Collections.Generic.List<Decantra.Presentation.View.BottleView>;
            Assert.IsNotNull(views);
            Assert.Greater(index, -1);
            var view = views[index];
            Assert.IsNotNull(view);
            var input = view.GetComponent<Decantra.Presentation.View.BottleInput>();
            Assert.IsNotNull(input);
            return input;
        }
    }
}
