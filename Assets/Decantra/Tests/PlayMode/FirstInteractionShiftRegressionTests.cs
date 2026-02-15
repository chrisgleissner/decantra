/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public sealed class FirstInteractionShiftRegressionTests
    {
        private const float RowTolerancePx = 1f;
        private const float RowMergeTolerance = 8f;

        [UnityTest]
        public IEnumerator Level1_FirstInteraction_DoesNotShiftTopTwoRows_AndEmitsArtifacts()
        {
            SceneBootstrap.EnsureScene();

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found.");

            controller.LoadLevel(1, 10991);
            yield return WaitForControllerReady(controller, 8f);

            var gridRect = GameObject.Find("BottleGrid")?.GetComponent<RectTransform>();
            Assert.IsNotNull(gridRect, "BottleGrid RectTransform not found.");

            Canvas.ForceUpdateCanvases();
            yield return null;

            string outputDir = GetOutputDirectory();
            Directory.CreateDirectory(outputDir);

            Texture2D beforeFrame = null;
            yield return CaptureFrame(texture => beforeFrame = texture);
            Assert.IsNotNull(beforeFrame, "Failed to capture pre-interaction frame.");
            string s0Path = Path.Combine(outputDir, "S0.png");
            File.WriteAllBytes(s0Path, beforeFrame.EncodeToPNG());

            var beforeTracked = CaptureTopRows(gridRect, 2);
            Assert.Greater(beforeTracked.Count, 0, "No bottles found in top two rows before interaction.");

            var state = GetPrivateField(controller, "_state") as LevelState;
            Assert.IsNotNull(state, "Controller state not available.");
            Assert.IsTrue(TryFindValidMove(state, out int source, out int target, out int poured), "No valid move found in initial state.");

            controller.OnBottleTapped(source);
            yield return null;
            controller.OnBottleTapped(target);

            float duration = Mathf.Max(0.2f, 0.12f * poured);
            yield return new WaitForSeconds(duration + 0.35f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Texture2D afterFrame = null;
            yield return CaptureFrame(texture => afterFrame = texture);
            Assert.IsNotNull(afterFrame, "Failed to capture post-interaction frame.");
            string s1Path = Path.Combine(outputDir, "S1.png");
            File.WriteAllBytes(s1Path, afterFrame.EncodeToPNG());

            string diffPath = Path.Combine(outputDir, "diff.png");
            WriteDiffImage(beforeFrame, afterFrame, diffPath);

            var afterByName = CaptureByName(gridRect);
            float maxAbsDy = 0f;
            string worstBottle = string.Empty;

            var report = new StringBuilder();
            report.AppendLine("{");
            report.AppendLine("  \"levelIndex\": 1,");
            report.AppendLine("  \"seed\": 10991,");
            report.AppendLine("  \"trackedRows\": [1,2],");
            report.AppendLine($"  \"resolution\": \"{Screen.width}x{Screen.height}\",");
            report.AppendLine("  \"bottles\": [");

            for (int i = 0; i < beforeTracked.Count; i++)
            {
                var before = beforeTracked[i];
                Assert.IsTrue(afterByName.TryGetValue(before.Name, out float afterY), $"Bottle '{before.Name}' missing after interaction.");

                float dy = afterY - before.ScreenY;
                float absDy = Mathf.Abs(dy);
                if (absDy > maxAbsDy)
                {
                    maxAbsDy = absDy;
                    worstBottle = before.Name;
                }

                string comma = i < beforeTracked.Count - 1 ? "," : string.Empty;
                report.AppendLine($"    {{ \"name\": \"{before.Name}\", \"row\": {before.RowIndex + 1}, \"beforeY\": {before.ScreenY:F4}, \"afterY\": {afterY:F4}, \"dy\": {dy:F4}, \"absDy\": {absDy:F4} }}{comma}");
            }

            report.AppendLine("  ],");
            report.AppendLine($"  \"maxAbsDy\": {maxAbsDy:F6},");
            report.AppendLine($"  \"worstBottle\": \"{worstBottle}\",");
            report.AppendLine($"  \"pass\": {(maxAbsDy <= RowTolerancePx ? "true" : "false")}");
            report.AppendLine("}");

            string reportPath = Path.Combine(outputDir, "report.json");
            File.WriteAllText(reportPath, report.ToString());

            Object.Destroy(beforeFrame);
            Object.Destroy(afterFrame);

            Assert.LessOrEqual(maxAbsDy, RowTolerancePx,
                $"Top two rows shifted after first interaction. maxAbsDy={maxAbsDy:F4}, worstBottle={worstBottle}");
        }

        private static IEnumerator WaitForControllerReady(GameController controller, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (controller.HasActiveLevel && !controller.IsInputLocked)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Controller did not become ready in time.");
        }

        private static IEnumerator CaptureFrame(System.Action<Texture2D> onCapture)
        {
            yield return new WaitForEndOfFrame();
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false, false);
            onCapture?.Invoke(texture);
        }

        private static Dictionary<string, float> CaptureByName(RectTransform gridRect)
        {
            var result = new Dictionary<string, float>();
            for (int i = 0; i < gridRect.childCount; i++)
            {
                if (!(gridRect.GetChild(i) is RectTransform child) || !child.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, child.TransformPoint(child.rect.center));
                result[child.name] = screen.y;
            }

            return result;
        }

        private static List<BottleSnapshot> CaptureTopRows(RectTransform gridRect, int rowCount)
        {
            var all = new List<BottleSnapshot>();
            for (int i = 0; i < gridRect.childCount; i++)
            {
                if (!(gridRect.GetChild(i) is RectTransform child) || !child.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, child.TransformPoint(child.rect.center));
                all.Add(new BottleSnapshot(child.name, child.anchoredPosition.y, screen.y, -1));
            }

            all.Sort((a, b) => b.AnchoredY.CompareTo(a.AnchoredY));

            int currentRow = -1;
            float rowY = float.NaN;
            var tracked = new List<BottleSnapshot>();
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (currentRow < 0 || Mathf.Abs(item.AnchoredY - rowY) > RowMergeTolerance)
                {
                    currentRow++;
                    rowY = item.AnchoredY;
                }

                item = new BottleSnapshot(item.Name, item.AnchoredY, item.ScreenY, currentRow);
                if (currentRow < rowCount)
                {
                    tracked.Add(item);
                }
            }

            return tracked;
        }

        private static bool TryFindValidMove(LevelState state, out int source, out int target, out int poured)
        {
            source = -1;
            target = -1;
            poured = 0;

            for (int i = 0; i < state.Bottles.Count; i++)
            {
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    int amount = MoveRules.GetPourAmount(state, i, j);
                    if (amount <= 0)
                    {
                        continue;
                    }

                    source = i;
                    target = j;
                    poured = amount;
                    return true;
                }
            }

            return false;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        private static void WriteDiffImage(Texture2D before, Texture2D after, string outputPath)
        {
            int width = Mathf.Min(before.width, after.width);
            int height = Mathf.Min(before.height, after.height);
            var diff = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color a = before.GetPixel(x, y);
                    Color b = after.GetPixel(x, y);
                    float d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    float v = Mathf.Clamp01(d * 2.5f);
                    diff.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }

            diff.Apply(false, false);
            File.WriteAllBytes(outputPath, diff.EncodeToPNG());
            Object.Destroy(diff);
        }

        private static string GetOutputDirectory()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Artifacts", "first-interaction-shift-test");
        }

        private readonly struct BottleSnapshot
        {
            public BottleSnapshot(string name, float anchoredY, float screenY, int rowIndex)
            {
                Name = name;
                AnchoredY = anchoredY;
                ScreenY = screenY;
                RowIndex = rowIndex;
            }

            public string Name { get; }
            public float AnchoredY { get; }
            public float ScreenY { get; }
            public int RowIndex { get; }
        }
    }
}
