/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
using Decantra.Presentation.View;
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

            // Get the detail overlay image (which has distinct colors per theme bucket)
            var detailField = typeof(GameController).GetField("backgroundDetail", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(detailField, "backgroundDetail field not found");

            var detail = detailField.GetValue(controller) as UnityEngine.UI.Image;
            Assert.IsNotNull(detail, "backgroundDetail is null");

            // Compare levels from different theme buckets (1-9, 10-19, 20-24)
            controller.LoadLevel(1, 12345);  // Theme bucket 0 (blue)
            yield return null;
            var firstColor = detail.color;

            controller.LoadLevel(15, 12345);  // Theme bucket 1 (purple)
            yield return null;
            var secondColor = detail.color;

            Assert.AreNotEqual(firstColor, secondColor, "Background overlays should differ between theme buckets");
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
        public IEnumerator FirstMove_DoesNotShiftBottleGridVertically()
        {
            SceneBootstrap.EnsureScene();

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found.");

            float readyTimeout = 8f;
            float readyElapsed = 0f;
            while (readyElapsed < readyTimeout && (!controller.HasActiveLevel || controller.IsInputLocked))
            {
                readyElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(controller.HasActiveLevel, "Controller did not load an active level in time.");
            Assert.IsFalse(controller.IsInputLocked, "Controller remained input-locked after startup.");

            Canvas.ForceUpdateCanvases();
            yield return null;

            var gridRect = GameObject.Find("BottleGrid")?.GetComponent<RectTransform>();
            Assert.IsNotNull(gridRect, "BottleGrid RectTransform not found.");

            float beforeAnchoredY = gridRect.anchoredPosition.y;
            var beforeRows = new (string Name, float ScreenY)[gridRect.childCount];
            int beforeCount = 0;
            for (int i = 0; i < gridRect.childCount; i++)
            {
                if (!(gridRect.GetChild(i) is RectTransform child) || !child.gameObject.activeSelf)
                {
                    continue;
                }

                var screen = RectTransformUtility.WorldToScreenPoint(null, child.TransformPoint(child.rect.center));
                beforeRows[beforeCount++] = (child.name, screen.y);
            }

            for (int i = 0; i < beforeCount - 1; i++)
            {
                for (int j = i + 1; j < beforeCount; j++)
                {
                    if (beforeRows[j].ScreenY > beforeRows[i].ScreenY)
                    {
                        var temp = beforeRows[i];
                        beforeRows[i] = beforeRows[j];
                        beforeRows[j] = temp;
                    }
                }
            }

            var state = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(state, "Controller state not available.");
            Assert.IsTrue(TryFindValidMove(state, out int source, out int target), "No valid move found in initial state.");

            int poured = controller.GetPourAmount(source, target);
            Assert.Greater(poured, 0, "Resolved move had zero pour amount.");
            float expectedDuration = Mathf.Max(0.2f, 0.12f * poured);

            controller.OnBottleTapped(source);
            yield return null;
            controller.OnBottleTapped(target);

            yield return new WaitForSeconds(expectedDuration + 0.25f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            float afterAnchoredY = gridRect.anchoredPosition.y;
            float delta = afterAnchoredY - beforeAnchoredY;

            Assert.AreEqual(0f, delta, 0.0001f,
                $"BottleGrid anchored Y changed after first move. Before={beforeAnchoredY:F6}, After={afterAnchoredY:F6}, Delta={delta:F6}");

            int topRowCount = Mathf.Min(6, beforeCount);
            float worstDelta = 0f;
            string worstBottle = string.Empty;
            for (int i = 0; i < topRowCount; i++)
            {
                string name = beforeRows[i].Name;
                float beforeScreenY = beforeRows[i].ScreenY;
                bool found = false;
                float afterScreenY = 0f;

                for (int c = 0; c < gridRect.childCount; c++)
                {
                    if (!(gridRect.GetChild(c) is RectTransform child) || !child.gameObject.activeSelf) continue;
                    if (!string.Equals(child.name, name, System.StringComparison.Ordinal)) continue;
                    var screen = RectTransformUtility.WorldToScreenPoint(null, child.TransformPoint(child.rect.center));
                    afterScreenY = screen.y;
                    found = true;
                    break;
                }

                Assert.IsTrue(found, $"Bottle '{name}' from top rows before move not found after move.");
                float d = Mathf.Abs(Mathf.Round(afterScreenY) - Mathf.Round(beforeScreenY));
                if (d > worstDelta)
                {
                    worstDelta = d;
                    worstBottle = name;
                }
            }

            Assert.LessOrEqual(worstDelta, 1f,
                $"Top rows moved on first move. Worst bottle={worstBottle}, rounded screen delta={worstDelta:F3}px");
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

            // Mixed state ensures multiple moves are required; movesAllowed=0 triggers failure on any move.
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Blue, ColorId.Red, null }),
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

            var confirmReset = GameObject.Find("YesButton");
            Assert.IsNotNull(confirmReset, "Reset confirmation should be visible after tapping reset.");
            var confirmResetButton = confirmReset.GetComponent<Button>();
            Assert.IsNotNull(confirmResetButton, "Reset confirmation button should have Button component.");
            confirmResetButton.onClick.Invoke();

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

            var confirmReset = GameObject.Find("YesButton");
            Assert.IsNotNull(confirmReset, "Reset confirmation should be visible after tapping reset.");
            var confirmResetButton = confirmReset.GetComponent<Button>();
            Assert.IsNotNull(confirmResetButton, "Reset confirmation button should have Button component.");
            confirmResetButton.onClick.Invoke();

            yield return null;

            var afterColor = image.color;
            float delta = Mathf.Abs(initialColor.r - afterColor.r)
                + Mathf.Abs(initialColor.g - afterColor.g)
                + Mathf.Abs(initialColor.b - afterColor.b)
                + Mathf.Abs(initialColor.a - afterColor.a);
            Assert.Less(delta, 0.002f, "Reset should keep the same background.");
        }

        [UnityTest]
        public IEnumerator SinkBottle_CannotStartDrag_ButNormalBottleCan()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            // Scan levels 20+ to find one that deterministically has a sink bottle.
            // DetermineSinkCount is hash-based; level 25 has sinkCount=1 with
            // the current hash, but we scan to be resilient if the hash changes.
            int sinkLevel = -1;
            for (int candidate = 20; candidate <= 99; candidate++)
            {
                if (LevelDifficultyEngine.DetermineSinkCount(candidate) > 0)
                {
                    sinkLevel = candidate;
                    break;
                }
            }
            Assert.GreaterOrEqual(sinkLevel, 0, "Expected at least one level 20-99 with sinks.");

            controller.LoadLevel(sinkLevel, 901);
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

            Assert.GreaterOrEqual(sinkIndex, 0, $"Expected a sink bottle at level {sinkLevel}.");
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
        public IEnumerator StarTradeIn_HiddenOnStartup_AndTutorialReplayKeepsItHidden()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            var dialog = GetPrivateField(controller, "starTradeInDialog") as StarTradeInDialog;
            Assert.IsNotNull(dialog, "Star trade-in dialog should be wired.");
            Assert.IsFalse(dialog.IsVisible, "Star trade-in must be hidden on startup.");
            Assert.IsFalse(dialog.gameObject.activeSelf, "Star trade-in root should be inactive on startup.");

            var canvasGroup = GetPrivateField(dialog, "canvasGroup") as CanvasGroup;
            Assert.IsNotNull(canvasGroup);
            Assert.IsFalse(canvasGroup.blocksRaycasts, "Hidden star trade-in must not block input.");

            controller.ReplayTutorial();
            yield return null;

            var tutorialOverlay = GameObject.Find("TutorialOverlay");
            Assert.IsNotNull(tutorialOverlay);
            Assert.IsTrue(tutorialOverlay.activeSelf, "Tutorial should be visible after replay request.");
            Assert.IsFalse(dialog.IsVisible, "Star trade-in must remain hidden while tutorial is visible.");
        }

        [UnityTest]
        public IEnumerator StarTradeIn_CloseButton_HidesOverlayAndRestoresInput()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(1, 101);
            yield return null;

            ConfigureControllerProgress(controller, stars: 30, currentLevel: 1, seed: 101);
            bool lockedBeforeOpen = controller.IsInputLocked;

            controller.ShowStarTradeInDialog();
            yield return null;

            var dialog = GetPrivateField(controller, "starTradeInDialog") as StarTradeInDialog;
            Assert.IsNotNull(dialog);
            Assert.IsTrue(dialog.IsVisible, "Dialog should be visible after opening trade-in.");

            var closeButton = GetPrivateField(dialog, "closeButton") as Button;
            Assert.IsNotNull(closeButton);
            closeButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(dialog.IsVisible, "Dialog should close when Close is pressed.");
            Assert.AreEqual(lockedBeforeOpen, controller.IsInputLocked, "Input lock should be restored after closing trade-in.");
        }

        [UnityTest]
        public IEnumerator StarTradeIn_LowStars_DisablesActionsAndShowsInlineMessage()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            int sinkLevel = FindFirstLevelWithSink();
            Assert.GreaterOrEqual(sinkLevel, 20);

            ConfigureControllerProgress(controller, stars: 0, currentLevel: sinkLevel, seed: 901);
            controller.LoadLevel(sinkLevel, 901);
            yield return null;

            controller.ShowStarTradeInDialog();
            yield return null;

            var dialog = GetPrivateField(controller, "starTradeInDialog") as StarTradeInDialog;
            Assert.IsNotNull(dialog);
            Assert.IsTrue(dialog.IsVisible);

            var convertButton = GetPrivateField(dialog, "convertButton") as Button;
            var autoSolveButton = GetPrivateField(dialog, "autoSolveButton") as Button;
            var convertStatusText = GetPrivateField(dialog, "convertStatusText") as Text;
            var autoSolveStatusText = GetPrivateField(dialog, "autoSolveStatusText") as Text;
            var convertCostValueText = GetPrivateField(dialog, "convertCostValueText") as Text;
            var autoSolveCostValueText = GetPrivateField(dialog, "autoSolveCostValueText") as Text;

            Assert.IsNotNull(convertButton);
            Assert.IsNotNull(autoSolveButton);
            Assert.IsNotNull(convertStatusText);
            Assert.IsNotNull(autoSolveStatusText);
            Assert.IsNotNull(convertCostValueText);
            Assert.IsNotNull(autoSolveCostValueText);

            Assert.IsFalse(convertButton.interactable, "Convert card should be disabled when stars are insufficient.");
            Assert.IsFalse(autoSolveButton.interactable, "Auto-solve card should be disabled when stars are insufficient.");
            Assert.AreEqual("Not enough stars", convertStatusText.text);
            Assert.AreEqual("Not enough stars", autoSolveStatusText.text);
            StringAssert.Contains("stars", convertCostValueText.text);
            StringAssert.Contains("stars", autoSolveCostValueText.text);
        }

        [UnityTest]
        public IEnumerator StarTradeIn_ConvertConfirm_DeductsStarsAndRemovesSinks()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            int sinkLevel = FindFirstLevelWithSink();
            Assert.GreaterOrEqual(sinkLevel, 20);

            ConfigureControllerProgress(controller, stars: 20, currentLevel: sinkLevel, seed: 902);
            controller.LoadLevel(sinkLevel, 902);
            yield return null;

            controller.ShowStarTradeInDialog();
            yield return null;

            var dialog = GetPrivateField(controller, "starTradeInDialog") as StarTradeInDialog;
            Assert.IsNotNull(dialog);
            Assert.IsTrue(dialog.IsVisible);

            var convertButton = GetPrivateField(dialog, "convertButton") as Button;
            var confirmButton = GetPrivateField(dialog, "confirmButton") as Button;
            var confirmationRoot = GetPrivateField(dialog, "confirmationRoot") as GameObject;
            Assert.IsNotNull(convertButton);
            Assert.IsNotNull(confirmButton);
            Assert.IsNotNull(confirmationRoot);
            Assert.IsTrue(convertButton.interactable, "Convert card should be enabled when stars are sufficient.");

            convertButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(confirmationRoot.activeSelf, "Confirmation view should open after selecting an action.");

            confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(dialog.IsVisible, "Dialog should close after confirming the trade-in.");

            var progress = GetPrivateField(controller, "_progress") as ProgressData;
            Assert.IsNotNull(progress);
            Assert.AreEqual(10, progress.StarBalance, "Convert trade-in should deduct exactly 10 stars.");

            var state = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(state);
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                Assert.IsFalse(state.Bottles[i].IsSink, "All sink bottles should be converted to normal bottles.");
            }

            Assert.IsFalse(controller.IsInputLocked, "Input should be restored after confirm-flow conversion.");
        }

        [UnityTest]
        public IEnumerator StarTradeIn_ConvertExecution_GatedWhenStarsAreInsufficient()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            int sinkLevel = FindFirstLevelWithSink();
            Assert.GreaterOrEqual(sinkLevel, 20);

            ConfigureControllerProgress(controller, stars: 0, currentLevel: sinkLevel, seed: 903);
            controller.LoadLevel(sinkLevel, 903);
            yield return null;

            var stateBefore = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(stateBefore);
            bool hadSinkBefore = false;
            for (int i = 0; i < stateBefore.Bottles.Count; i++)
            {
                if (!stateBefore.Bottles[i].IsSink) continue;
                hadSinkBefore = true;
                break;
            }
            Assert.IsTrue(hadSinkBefore, "Expected at least one sink before conversion attempt.");

            InvokePrivate(controller, "ExecuteConvertSinksTradeIn");
            yield return null;

            var progress = GetPrivateField(controller, "_progress") as ProgressData;
            Assert.IsNotNull(progress);
            Assert.AreEqual(0, progress.StarBalance, "Insufficient-star conversion attempt must not spend stars.");

            var stateAfter = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(stateAfter);
            bool hasSinkAfter = false;
            for (int i = 0; i < stateAfter.Bottles.Count; i++)
            {
                if (!stateAfter.Bottles[i].IsSink) continue;
                hasSinkAfter = true;
                break;
            }

            Assert.IsTrue(hasSinkAfter, "Insufficient-star conversion attempt must not execute sink conversion.");
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

            // Now test long-press behavior on Reset button
            var resetGo = GameObject.Find("ResetButton");
            Assert.IsNotNull(resetGo, "Reset button should exist in scene.");

            // Get the LongPressButton component and trigger the long-press action directly
            var longPress = resetGo.GetComponent<LongPressButton>();
            Assert.IsNotNull(longPress, "Reset button should have LongPressButton component.");

            // Simulate long-press by directly invoking the callback (RequestRestartGame)
            controller.RequestRestartGame();

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
        public IEnumerator LevelPanelShortTap_DoesNotTriggerShareUi()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.LoadLevel(1, 123);
            yield return null;

            var levelPanel = GameObject.Find("LevelPanel");
            Assert.IsNotNull(levelPanel);
            var levelButton = levelPanel.GetComponent<Button>();
            Assert.IsNotNull(levelButton);

            Assert.IsNull(GameObject.Find("ShareButtonRoot"), "Share UI must be absent.");
            bool inputLockedBefore = controller.IsInputLocked;
            bool hasLevelBefore = controller.HasActiveLevel;

            levelButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(GameObject.Find("ShareButtonRoot"), "Share UI should remain absent after short tap.");
            Assert.AreEqual(inputLockedBefore, controller.IsInputLocked);
            Assert.AreEqual(hasLevelBefore, controller.HasActiveLevel);
        }

        [UnityTest]
        public IEnumerator LevelCompleteBanner_CentersStarsAndScoreGroup()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var banner = Object.FindFirstObjectByType<LevelCompleteBanner>();
            Assert.IsNotNull(banner);

            bool completed = false;
            // Use a mid-star count and non-zero score to exercise layout sizing.
            const float LayoutSettleSeconds = 0.05f; // Short wait to allow UI layout to settle in batch mode.

            banner.Show(1, 4, 180, false, () => { }, () => completed = true);
            yield return null;
            yield return new WaitForSeconds(LayoutSettleSeconds);

            var panel = GetPrivateField(banner, "panel") as RectTransform;
            var starsText = GetPrivateField(banner, "starsText") as Text;
            var scoreText = GetPrivateField(banner, "scoreText") as Text;
            Assert.IsNotNull(panel);
            Assert.IsNotNull(starsText);
            Assert.IsNotNull(scoreText);

            const float MinTextHeight = 1f; // Avoid zero bounds before Text layout finalizes.
            const float Half = 0.5f; // Used for center/extent calculations.
            const float CenterBaseline = 0f; // Panel center in anchored coordinates.

            float starsHeight = Mathf.Max(MinTextHeight, starsText.preferredHeight);
            float scoreHeight = Mathf.Max(MinTextHeight, scoreText.preferredHeight);
            float starsCenter = starsText.rectTransform.anchoredPosition.y;
            float scoreCenter = scoreText.rectTransform.anchoredPosition.y;

            float groupTop = starsCenter + starsHeight * Half;
            float groupBottom = scoreCenter - scoreHeight * Half;
            float groupCenter = (groupTop + groupBottom) * Half;

            // Shift is derived from panel height: 20px relative to a 280px panel.
            float expectedCenter = panel.rect.height * (20f / 280f);
            float tolerance = 1.5f; // Allow small layout variance while asserting centering.

            Assert.LessOrEqual(Mathf.Abs(groupCenter - expectedCenter), tolerance);
            Assert.Greater(groupCenter, CenterBaseline);

            if (completed)
            {
                banner.HideImmediate();
            }
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
            SetPrivateField(controller, "_precomputeTask", null);

            SetPrivateField(controller, "levelBanner", null);
            SetPrivateField(controller, "introBanner", null);

            InvokePrivate(controller, "Render");

            yield return new WaitForSeconds(0.8f);

            Assert.Greater(progress.CurrentScore, 100, "Winning should commit score.");
            Assert.GreaterOrEqual(progress.HighScore, progress.CurrentScore);
            Assert.GreaterOrEqual(progress.CurrentLevel, 2, "Winning should advance level.");
        }

        [UnityTest]
        public IEnumerator AccessibleColorsToggle_UpdatesRenderedBottleLiquid()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);
            float timeout = 8f;
            float elapsed = 0f;
            while (elapsed < timeout && !controller.HasActiveLevel)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(controller.HasActiveLevel);

            var state = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(state);

            int bottleIndex = -1;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var top = state.Bottles[i].TopColor;
                if (!top.HasValue) continue;
                bottleIndex = i;
                break;
            }

            Assert.GreaterOrEqual(bottleIndex, 0, "Expected at least one non-empty bottle.");

            var view = GetBottleViewForIndex(controller, bottleIndex);
            Assert.IsNotNull(view);

            controller.SetAccessibleColorsEnabled(false);
            yield return null;
            Color defaultColor = GetVisibleLiquidColor(view);
            Assert.IsTrue(MatchesAnyBoostedDefaultPalette((Color32)defaultColor, 2),
                $"Expected default bottle color to match boosted default palette. actual={(Color32)defaultColor}");

            controller.SetAccessibleColorsEnabled(true);
            Color accessibleColor = defaultColor;
            float elapsedWait = 0f;
            const float maxWait = 0.75f;
            while (elapsedWait < maxWait)
            {
                yield return null;
                elapsedWait += Time.unscaledDeltaTime;
                accessibleColor = GetVisibleLiquidColor(view);
                if (MatchesAnyBoostedAccessiblePalette((Color32)accessibleColor, 2))
                {
                    break;
                }
            }

            Assert.IsTrue(MatchesAnyBoostedAccessiblePalette((Color32)accessibleColor, 2),
                $"Expected accessible bottle color to match boosted accessible palette. actual={(Color32)accessibleColor}");
            Assert.IsFalse(MatchesAnyBoostedDefaultPalette((Color32)accessibleColor, 2),
                $"Accessible color still matches boosted default palette. actual={(Color32)accessibleColor}");
            Assert.IsFalse(IsColorWithinTolerance((Color32)defaultColor, (Color32)accessibleColor, 1));
        }

        [UnityTest]
        public IEnumerator AccessibleColorsToggle_MidGame_NoNullReferenceLogs()
        {
            bool nullReferenceLogged = false;
            string nullReferenceLogDetails = null;
            Application.LogCallback callback = (condition, stackTrace, type) =>
            {
                if (type == LogType.Exception && condition != null && condition.Contains("NullReferenceException"))
                {
                    nullReferenceLogged = true;
                    nullReferenceLogDetails = condition + "\n" + stackTrace;
                }
            };

            Application.logMessageReceived += callback;
            try
            {
                SceneBootstrap.EnsureScene();
                yield return null;

                var controller = Object.FindFirstObjectByType<GameController>();
                Assert.IsNotNull(controller);
                float timeout = 8f;
                float elapsed = 0f;
                while (elapsed < timeout && !controller.HasActiveLevel)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.IsTrue(controller.HasActiveLevel);

                for (int i = 0; i < 3; i++)
                {
                    controller.SetAccessibleColorsEnabled(true);
                    yield return null;
                    controller.SetAccessibleColorsEnabled(false);
                    yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }

            Assert.IsFalse(nullReferenceLogged,
                $"A NullReferenceException was logged during AccessibleColorsToggle_MidGame_NoNullReferenceLogs.\n{nullReferenceLogDetails}");
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

        private static int FindFirstLevelWithSink()
        {
            for (int level = 20; level <= 200; level++)
            {
                if (LevelDifficultyEngine.DetermineSinkCount(level) > 0)
                {
                    return level;
                }
            }

            return -1;
        }

        private static void ConfigureControllerProgress(GameController controller, int stars, int currentLevel, int seed)
        {
            string root = Path.Combine(Path.GetTempPath(), "decantra-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "progress.json");

            var store = new ProgressStore(new[] { path });
            var progress = new ProgressData
            {
                HighestUnlockedLevel = Mathf.Max(1, currentLevel),
                CurrentLevel = Mathf.Max(1, currentLevel),
                CurrentSeed = seed,
                CurrentScore = 0,
                HighScore = 0,
                StarBalance = Mathf.Max(0, stars)
            };

            SetPrivateField(controller, "_progressStore", store);
            SetPrivateField(controller, "_progress", progress);
            SetPrivateField(controller, "_currentLevel", progress.CurrentLevel);
            SetPrivateField(controller, "_currentSeed", progress.CurrentSeed);
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

        private static Decantra.Presentation.View.BottleView GetBottleViewForIndex(GameController controller, int index)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "bottleViews field not found");
            var views = field.GetValue(controller) as System.Collections.Generic.List<Decantra.Presentation.View.BottleView>;
            Assert.IsNotNull(views);
            Assert.Greater(index, -1);
            return views[index];
        }

        private static Color GetVisibleLiquidColor(Decantra.Presentation.View.BottleView view)
        {
            var field = typeof(Decantra.Presentation.View.BottleView).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "slots field not found");
            var slots = field.GetValue(view) as System.Collections.Generic.List<Image>;
            Assert.IsNotNull(slots);

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] == null || !slots[i].gameObject.activeSelf) continue;
                return slots[i].color;
            }

            Assert.Fail("No visible liquid segment found.");
            return Color.clear;
        }

        private static Color ExpectedDefaultColor(ColorId colorId)
        {
            switch (colorId)
            {
                case ColorId.Red: return new Color32(235, 64, 56, 255);
                case ColorId.Blue: return new Color32(64, 128, 242, 255);
                case ColorId.Green: return new Color32(51, 199, 99, 255);
                case ColorId.Yellow: return new Color32(250, 224, 51, 255);
                case ColorId.Purple: return new Color32(166, 97, 217, 255);
                case ColorId.Orange: return new Color32(247, 140, 51, 255);
                case ColorId.Cyan: return new Color32(51, 217, 230, 255);
                case ColorId.Magenta: return new Color32(235, 92, 199, 255);
                default: return Color.white;
            }
        }

        private static Color ExpectedAccessibleColor(ColorId colorId)
        {
            switch (colorId)
            {
                case ColorId.Red: return new Color32(0, 114, 178, 255);
                case ColorId.Blue: return new Color32(230, 159, 0, 255);
                case ColorId.Green: return new Color32(86, 180, 233, 255);
                case ColorId.Yellow: return new Color32(0, 158, 115, 255);
                case ColorId.Purple: return new Color32(240, 228, 66, 255);
                case ColorId.Orange: return new Color32(213, 94, 0, 255);
                case ColorId.Cyan: return new Color32(204, 121, 167, 255);
                case ColorId.Magenta: return new Color32(27, 42, 65, 255);
                default: return Color.white;
            }
        }

        private static Color BoostForBottleLiquid(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            v = Mathf.Clamp01(v * 1.35f + 0.08f);
            s = Mathf.Clamp01(Mathf.Lerp(s, 1f, 0.12f));
            return Color.HSVToRGB(h, s, v);
        }

        private static void AssertColorApproximately(Color32 expected, Color32 actual, byte channelTolerance)
        {
            Assert.LessOrEqual(Mathf.Abs(expected.r - actual.r), channelTolerance, $"R channel differs. expected={expected} actual={actual}");
            Assert.LessOrEqual(Mathf.Abs(expected.g - actual.g), channelTolerance, $"G channel differs. expected={expected} actual={actual}");
            Assert.LessOrEqual(Mathf.Abs(expected.b - actual.b), channelTolerance, $"B channel differs. expected={expected} actual={actual}");
            Assert.LessOrEqual(Mathf.Abs(expected.a - actual.a), channelTolerance, $"A channel differs. expected={expected} actual={actual}");
        }

        private static bool IsColorWithinTolerance(Color32 expected, Color32 actual, byte channelTolerance)
        {
            return Mathf.Abs(expected.r - actual.r) <= channelTolerance
                && Mathf.Abs(expected.g - actual.g) <= channelTolerance
                && Mathf.Abs(expected.b - actual.b) <= channelTolerance
                && Mathf.Abs(expected.a - actual.a) <= channelTolerance;
        }

        private static bool MatchesAnyBoostedDefaultPalette(Color32 actual, byte channelTolerance)
        {
            return MatchesAnyBoostedPalette(actual, channelTolerance, ExpectedDefaultColor);
        }

        private static bool MatchesAnyBoostedAccessiblePalette(Color32 actual, byte channelTolerance)
        {
            return MatchesAnyBoostedPalette(actual, channelTolerance, ExpectedAccessibleColor);
        }

        private static bool MatchesAnyBoostedPalette(Color32 actual, byte channelTolerance, System.Func<ColorId, Color> expectedColorProvider)
        {
            var ids = new[]
            {
                ColorId.Red,
                ColorId.Blue,
                ColorId.Green,
                ColorId.Yellow,
                ColorId.Purple,
                ColorId.Orange,
                ColorId.Cyan,
                ColorId.Magenta
            };

            for (int i = 0; i < ids.Length; i++)
            {
                var expected = (Color32)BoostForBottleLiquid(expectedColorProvider(ids[i]));
                if (IsColorWithinTolerance(expected, actual, channelTolerance))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
