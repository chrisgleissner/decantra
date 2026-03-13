using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode.Layout
{
    public sealed class LayoutMetricsProbePlayModeTests
    {
        private const float AbsoluteTolerancePx = 1f;
        private const float RatioTolerance = 0.001f;

        [UnityTest]
        public IEnumerator CaptureLayoutMetricsJson()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return CaptureMetrics(metric => metrics = metric);

            string outputPath = ResolveOutputPath(
                "DECANTRA_LAYOUT_METRICS_PATH",
                Path.Combine("Artifacts", "layout", "layout-metrics.json"));

            WriteMetrics(metrics, outputPath);
            Assert.That(File.Exists(outputPath), Is.True, $"Layout metrics file was not written: {outputPath}");
            Assert.False(metrics.HasBottleOverlap, "Measured bottle row overlap is present in captured layout metrics.");
        }

        [UnityTest]
        public IEnumerator LayoutInvariants_PreserveHudAndHorizontalBaseline_WhileCompactingThreeRowBoards()
        {
            string baselinePath = ResolveOutputPath(
                "DECANTRA_LAYOUT_BASELINE_PATH",
                Path.Combine("Assets", "Decantra", "Tests", "PlayMode", "Fixtures", "layout-baseline-1.4.1.json"));

            if (!File.Exists(baselinePath))
            {
                Assert.Ignore($"Baseline metrics file not found: {baselinePath}");
            }

            var baseline = JsonUtility.FromJson<LayoutProbe.LayoutMetrics>(File.ReadAllText(baselinePath));
            Assert.NotNull(baseline, "Failed to parse baseline layout metrics JSON.");

            var current = default(LayoutProbe.LayoutMetrics);
            yield return CaptureMetrics(metric => current = metric);

            Assert.False(current.HasBottleOverlap, "Current layout has bottle overlap.");
            Assert.GreaterOrEqual(current.RowGap12, 0f, "Row 1 and row 2 overlap.");
            Assert.GreaterOrEqual(current.RowGap23, 0f, "Row 2 and row 3 overlap.");

            Compare("LogoTopY", baseline.LogoTopY, current.LogoTopY, baseline.LogoTopRatioY, current.LogoTopRatioY);
            Compare("LogoBottomY", baseline.LogoBottomY, current.LogoBottomY, baseline.LogoBottomRatioY, current.LogoBottomRatioY);
            Compare("LogoCenterX", baseline.LogoCenterX, current.LogoCenterX, baseline.LogoCenterRatioX, current.LogoCenterRatioX);

            Compare("LeftBottleCenterX", baseline.LeftBottleCenterX, current.LeftBottleCenterX, baseline.LeftBottleCenterRatioX, current.LeftBottleCenterRatioX);
            Compare("MiddleBottleCenterX", baseline.MiddleBottleCenterX, current.MiddleBottleCenterX, baseline.MiddleBottleCenterRatioX, current.MiddleBottleCenterRatioX);
            Compare("RightBottleCenterX", baseline.RightBottleCenterX, current.RightBottleCenterX, baseline.RightBottleCenterRatioX, current.RightBottleCenterRatioX);

            Compare("BottleSpacingLM", baseline.BottleSpacingLM, current.BottleSpacingLM, baseline.BottleSpacingLMRatioX, current.BottleSpacingLMRatioX);
            Compare("BottleSpacingMR", baseline.BottleSpacingMR, current.BottleSpacingMR, baseline.BottleSpacingMRRatioX, current.BottleSpacingMRRatioX);

        }

        private static IEnumerator CaptureMetrics(Action<LayoutProbe.LayoutMetrics> onCaptured)
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.NotNull(controller, "GameController not found.");

            controller.LoadLevel(21, 192731);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.1f);

            Canvas.ForceUpdateCanvases();

            var probeGo = new GameObject("LayoutProbe");
            try
            {
                var probe = probeGo.AddComponent<LayoutProbe>();
                var metrics = probe.Capture(controller);
                onCaptured(metrics);
            }
            finally
            {
                UnityEngine.Object.Destroy(probeGo);
            }
        }

        private static GameController ResolvePrimaryController()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameController best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < controllers.Length; i++)
            {
                var candidate = controllers[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = 0;
                var views = ResolveBottleViews(candidate);
                score += views.Count * 10;
                if (candidate.gameObject.activeInHierarchy)
                {
                    score += 100;
                }

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static List<BottleView> ResolveBottleViews(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return new List<BottleView>();
            }

            var views = field.GetValue(controller) as List<BottleView>;
            if (views == null)
            {
                return new List<BottleView>();
            }

            return views;
        }

        private static string ResolveOutputPath(string envName, string defaultRelativePath)
        {
            string fromEnv = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return Path.IsPathRooted(fromEnv)
                    ? fromEnv
                    : Path.GetFullPath(fromEnv, Directory.GetCurrentDirectory());
            }

            return Path.GetFullPath(defaultRelativePath, Directory.GetCurrentDirectory());
        }

        private static void WriteMetrics(LayoutProbe.LayoutMetrics metrics, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(metrics, true);
            File.WriteAllText(path, json);
            Debug.Log($"Layout probe metrics written: {path}\n{json}");
        }

        private static void Compare(string name, float baselineAbs, float currentAbs, float baselineRatio, float currentRatio)
        {
            float absDelta = Mathf.Abs(currentAbs - baselineAbs);
            float ratioDelta = Mathf.Abs(currentRatio - baselineRatio);

            Assert.LessOrEqual(
                absDelta,
                AbsoluteTolerancePx,
                $"{name} absolute delta {absDelta:F4}px exceeds tolerance {AbsoluteTolerancePx:F4}px (baseline={baselineAbs:F4}, current={currentAbs:F4}).");

            Assert.LessOrEqual(
                ratioDelta,
                RatioTolerance,
                $"{name} ratio delta {ratioDelta:F6} exceeds tolerance {RatioTolerance:F6} (baseline={baselineRatio:F6}, current={currentRatio:F6}).");
        }
    }
}