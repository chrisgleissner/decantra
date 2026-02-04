/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;

namespace Decantra.Tests.EditMode
{
    /// <summary>
    /// Tests for solver metric scoring integrity.
    /// Validates that raw complexity metrics remain deterministic and meaningful.
    /// </summary>
    public sealed class DifficultyIntegrityTests
    {
        [Test]
        public void ComplexityScorer_RawComplexity_NoLevelNumberDependency()
        {
            // Identical metrics should produce identical raw complexity
            // regardless of what we claim the "level number" is

            var metrics = new LevelMetrics(
                forcedMoveRatio: 0.3f,
                averageBranchingFactor: 2.5f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.4f,
                trapScore: 0.6f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 2);

            int optimalMoves = 8;

            // Compute raw complexity multiple times
            double score1 = ComplexityScorer.ComputeRawComplexity(metrics, optimalMoves);
            double score2 = ComplexityScorer.ComputeRawComplexity(metrics, optimalMoves);

            // Should be identical (deterministic)
            Assert.AreEqual(score1, score2, 0.0001);

            // Should be non-zero for reasonable metrics
            Assert.Greater(score1, 0.0);
        }

        [Test]
        public void ComplexityScorer_RawComplexity_PreservesVariance()
        {
            // Generate metrics with natural variation
            var metricsList = new List<LevelMetrics>
            {
                new LevelMetrics(0.1f, 3.0f, 0, 0.2f, 0.8f, 1, 4, 5, 3),
                new LevelMetrics(0.5f, 2.0f, 2, 0.5f, 0.4f, 2, 3, 4, 2),
                new LevelMetrics(0.7f, 1.5f, 5, 0.7f, 0.1f, 3, 2, 3, 2),
                new LevelMetrics(0.2f, 2.8f, 1, 0.3f, 0.7f, 1, 4, 5, 3),
                new LevelMetrics(0.4f, 2.2f, 3, 0.4f, 0.5f, 2, 3, 4, 2),
            };

            var optimalMoves = new[] { 5, 8, 12, 6, 10 };
            var scores = new double[5];

            for (int i = 0; i < 5; i++)
            {
                scores[i] = ComplexityScorer.ComputeRawComplexity(metricsList[i], optimalMoves[i]);
            }

            // Compute variance
            double mean = 0;
            foreach (var s in scores) mean += s;
            mean /= scores.Length;

            double variance = 0;
            foreach (var s in scores)
            {
                double diff = s - mean;
                variance += diff * diff;
            }
            variance /= scores.Length;

            // Should have meaningful variance (not all the same)
            Assert.Greater(variance, 0.1);
        }

        [Test]
        public void ComplexityScorer_ValidateVariance_DetectsSaturation()
        {
            // All scores the same = saturated
            var saturatedScores = new double[100];
            for (int i = 0; i < 100; i++)
                saturatedScores[i] = 10.0;

            bool result = ComplexityScorer.ValidateVariance(saturatedScores);
            Assert.IsFalse(result, "Should detect saturated metrics");

            // Varied scores = good
            var variedScores = new double[100];
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
                variedScores[i] = 5.0 + rng.NextDouble() * 10.0;

            result = ComplexityScorer.ValidateVariance(variedScores);
            Assert.IsTrue(result, "Should pass with sufficient variance");
        }

        [Test]
        public void ComplexityScorer_ValidateIndependence_DetectsCheating()
        {
            // Perfectly correlated with level number = cheating
            var cheatingScores = new double[100];
            for (int i = 0; i < 100; i++)
                cheatingScores[i] = i + 1; // Linear with level

            bool result = ComplexityScorer.ValidateIndependence(cheatingScores, 0.95);
            Assert.IsFalse(result, "Should detect artificial correlation");

            // Random scores = independent
            var independentScores = new double[100];
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
                independentScores[i] = rng.NextDouble() * 20.0;

            result = ComplexityScorer.ValidateIndependence(independentScores, 0.95);
            Assert.IsTrue(result, "Should pass with independent scores");
        }

        [Test]
        public void MonotonicMapper_MapToDifficulty_EnforcesMonotonicity()
        {
            // Create levels with random raw scores
            var rng = new Random(42);
            var levels = new List<LevelComplexityData>();

            for (int i = 1; i <= 100; i++)
            {
                levels.Add(new LevelComplexityData
                {
                    LevelIndex = i,
                    RawComplexity = rng.NextDouble() * 30.0 // Random 0-30
                });
            }

            var difficulties = MonotonicDifficultyMapper.MapToDifficulty(levels);

            // Check monotonicity
            for (int i = 2; i <= 100; i++)
            {
                int current = difficulties[i];
                int previous = difficulties[i - 1];
                Assert.GreaterOrEqual(current, previous,
                    $"Difficulty must be monotonic at level {i}");
            }
        }

        [Test]
        public void MonotonicMapper_MapToDifficulty_SpansFullRange()
        {
            // Create levels with varied raw scores
            var levels = new List<LevelComplexityData>();

            for (int i = 1; i <= 1000; i++)
            {
                // Gradually increasing raw scores with some noise
                double baseScore = i / 50.0; // 0.02 to 20.0
                double noise = (i % 7) * 0.5;
                levels.Add(new LevelComplexityData
                {
                    LevelIndex = i,
                    RawComplexity = baseScore + noise
                });
            }

            var difficulties = MonotonicDifficultyMapper.MapToDifficulty(levels);

            // Should span close to 1-100
            int minDiff = int.MaxValue;
            int maxDiff = int.MinValue;

            foreach (var diff in difficulties.Values)
            {
                minDiff = Math.Min(minDiff, diff);
                maxDiff = Math.Max(maxDiff, diff);
            }

            Assert.LessOrEqual(minDiff, 5, "Min difficulty should be near 1");
            Assert.GreaterOrEqual(maxDiff, 95, "Max difficulty should be near 100");
        }

        [Test]
        public void MonotonicMapper_ValidateMonotonicity_DetectsViolations()
        {
            // Create difficulties with a violation
            var difficulties = new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 5 },
                { 4, 3 }, // Violation: goes down
                { 5, 6 }
            };

            var result = MonotonicDifficultyMapper.ValidateMonotonicity(difficulties);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(4, result.FirstViolationLevel);
            Assert.Greater(result.Violations.Count, 0);
        }

        [Test]
        public void MonotonicMapper_ValidateLinearity_DetectsPlateaus()
        {
            // Create difficulties with a long plateau
            var difficulties = new Dictionary<int, int>();

            for (int i = 1; i <= 100; i++)
            {
                // Long plateau for first 20 levels, then linear increase
                difficulties[i] = i <= 20 ? 10 : 10 + (i - 20);
            }

            var result = MonotonicDifficultyMapper.ValidateLinearity(difficulties);

            // Should detect the plateau
            Assert.Greater(result.Warnings.Count, 0);
        }

        [Test]
        public void ComplexityScorer_DifferentMetrics_ProduceDifferentScores()
        {
            // Easy puzzle: high forced moves, low branching, no traps
            var easyMetrics = new LevelMetrics(
                forcedMoveRatio: 0.8f,
                averageBranchingFactor: 1.2f,
                decisionDepth: 5,
                emptyBottleUsageRatio: 0.9f,
                trapScore: 0.0f,
                solutionMultiplicity: 3,
                mixedBottleCount: 2,
                distinctSignatureCount: 3,
                topColorVariety: 2);

            // Hard puzzle: low forced moves, high branching, high traps
            var hardMetrics = new LevelMetrics(
                forcedMoveRatio: 0.1f,
                averageBranchingFactor: 4.0f,
                decisionDepth: 0,
                emptyBottleUsageRatio: 0.2f,
                trapScore: 0.9f,
                solutionMultiplicity: 1,
                mixedBottleCount: 5,
                distinctSignatureCount: 8,
                topColorVariety: 4);

            double easyScore = ComplexityScorer.ComputeRawComplexity(easyMetrics, 5);
            double hardScore = ComplexityScorer.ComputeRawComplexity(hardMetrics, 12);

            Assert.Greater(hardScore, easyScore,
                "Harder puzzle should have higher raw complexity");
        }
    }
}
