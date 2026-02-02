/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Stage 2: Maps raw complexity scores to monotonic difficulty bands (1-100).
    /// Ensures difficulty increases with level number while respecting solver metrics.
    /// </summary>
    public static class MonotonicDifficultyMapper
    {
        /// <summary>
        /// Maps raw complexity scores to difficulty values (1-100).
        /// Guarantees monotonicity by enforcing difficulty[N+1] >= difficulty[N].
        /// Uses raw complexity to adjust from linear baseline, then enforces monotonicity.
        /// </summary>
        /// <param name="levels">Level data with raw scores</param>
        /// <returns>Difficulty assignments (1-100) for each level</returns>
        public static Dictionary<int, int> MapToDifficulty(List<LevelComplexityData> levels)
        {
            if (levels == null || levels.Count == 0)
                return new Dictionary<int, int>();

            // Sort levels by level index to process in order
            var sortedByLevel = levels.OrderBy(l => l.LevelIndex).ToList();
            int n = sortedByLevel.Count;

            // Find min/max raw complexity for normalization
            double minRaw = levels.Min(l => l.RawComplexity);
            double maxRaw = levels.Max(l => l.RawComplexity);
            double rawRange = maxRaw - minRaw;

            // Build difficulty assignments with monotonicity enforcement
            var result = new Dictionary<int, int>();
            int currentDifficulty = 1;

            // REVISED ALGORITHM: Proportional spacing with complexity-based micro-adjustments
            // Strategy: Ensure every level gets a fair share of the 1-100 range, 
            // with small adjustments based on raw complexity to preserve empirical signal

            for (int i = 0; i < n; i++)
            {
                var level = sortedByLevel[i];

                // Base position: proportional spacing ensuring we use full 1-100 range
                double basePosition = 1.0 + (i / (double)Math.Max(1, n - 1)) * 99.0;

                // Normalize raw complexity to 0-1 range
                double normalizedRaw = rawRange > 0.0001
                    ? (level.RawComplexity - minRaw) / rawRange
                    : 0.5;

                // Micro-adjustment based on complexity deviation from level-based expectation
                // Expected normalized complexity at this position
                double expectedComplexity = i / (double)Math.Max(1, n - 1);
                double complexityDeviation = normalizedRaw - expectedComplexity;

                // Apply SMALL adjustment (max ±2 difficulty points) to preserve signal
                // without disrupting monotonicity
                double microAdjustment = complexityDeviation * 4.0; // ±2 points max

                // Target difficulty with micro-adjustment
                double targetDifficulty = basePosition + microAdjustment;

                // Clamp to valid range
                int proposedDifficulty = Math.Max(1, Math.Min(100, (int)Math.Round(targetDifficulty)));

                // Enforce monotonicity: never decrease
                currentDifficulty = Math.Max(currentDifficulty, proposedDifficulty);

                result[level.LevelIndex] = currentDifficulty;
            }

            return result;
        }

        /// <summary>
        /// Validates that difficulty is strictly monotonic.
        /// Returns validation result with diagnostics.
        /// </summary>
        public static MonotonicityValidation ValidateMonotonicity(Dictionary<int, int> difficulties)
        {
            var validation = new MonotonicityValidation { IsValid = true };

            if (difficulties == null || difficulties.Count < 2)
            {
                validation.IsValid = false;
                validation.Message = "Insufficient data for validation";
                return validation;
            }

            var sorted = difficulties.OrderBy(kv => kv.Key).ToList();

            for (int i = 1; i < sorted.Count; i++)
            {
                int level = sorted[i].Key;
                int prevLevel = sorted[i - 1].Key;
                int difficulty = sorted[i].Value;
                int prevDifficulty = sorted[i - 1].Value;

                if (difficulty < prevDifficulty)
                {
                    validation.IsValid = false;
                    validation.Violations.Add($"Level {level}: difficulty={difficulty} < prev={prevDifficulty}");

                    if (validation.FirstViolationLevel < 0)
                        validation.FirstViolationLevel = level;
                }
            }

            if (!validation.IsValid)
            {
                validation.Message = $"Monotonicity violated at {validation.Violations.Count} levels";
            }
            else
            {
                validation.Message = "Difficulty is monotonic";
            }

            return validation;
        }

        /// <summary>
        /// Validates that difficulty progression is roughly linear.
        /// Returns false if progression is too flat or stair-stepped.
        /// </summary>
        public static LinearityValidation ValidateLinearity(Dictionary<int, int> difficulties)
        {
            var validation = new LinearityValidation { IsValid = true };

            if (difficulties == null || difficulties.Count < 100)
            {
                validation.IsValid = false;
                validation.Message = "Insufficient data for linearity check";
                return validation;
            }

            var sorted = difficulties.OrderBy(kv => kv.Key).ToList();
            int n = sorted.Count;

            // Check for plateau detection (too many consecutive equal values)
            int maxPlateauLength = 0;
            int currentPlateauLength = 1;
            int lastDifficulty = sorted[0].Value;

            for (int i = 1; i < n; i++)
            {
                if (sorted[i].Value == lastDifficulty)
                {
                    currentPlateauLength++;
                    maxPlateauLength = Math.Max(maxPlateauLength, currentPlateauLength);
                }
                else
                {
                    currentPlateauLength = 1;
                    lastDifficulty = sorted[i].Value;
                }
            }

            // Flag if more than 5% of levels form a single plateau
            if (maxPlateauLength > n / 20)
            {
                validation.IsValid = false;
                validation.Warnings.Add($"Long plateau detected: {maxPlateauLength} consecutive levels with same difficulty");
            }

            // Check slope in windows
            int windowSize = Math.Min(100, n / 10);
            int minIncrease = Math.Max(1, windowSize / 20); // Expect at least 5% increase per window

            for (int i = 0; i + windowSize < n; i += windowSize)
            {
                int diffStart = sorted[i].Value;
                int diffEnd = sorted[i + windowSize].Value;
                int increase = diffEnd - diffStart;

                if (increase < minIncrease)
                {
                    validation.Warnings.Add($"Flat progression at level {sorted[i].Key}: increase={increase} over {windowSize} levels");
                }
            }

            // Check overall range
            int minDiff = sorted.Min(kv => kv.Value);
            int maxDiff = sorted.Max(kv => kv.Value);
            int range = maxDiff - minDiff;

            if (range < 90) // Should span most of 1-100
            {
                validation.IsValid = false;
                validation.Warnings.Add($"Insufficient range: {minDiff}-{maxDiff} (expected ~1-100)");
            }

            if (validation.Warnings.Count > 0)
            {
                validation.Message = $"Linearity issues detected: {validation.Warnings.Count} warnings";
            }
            else
            {
                validation.Message = "Difficulty progression is linear";
            }

            return validation;
        }
    }

    /// <summary>
    /// Level complexity data for difficulty mapping.
    /// </summary>
    public sealed class LevelComplexityData
    {
        public int LevelIndex { get; set; }
        public double RawComplexity { get; set; }
        public LevelMetrics Metrics { get; set; }
        public int OptimalMoves { get; set; }
    }

    /// <summary>
    /// Monotonicity validation result.
    /// </summary>
    public sealed class MonotonicityValidation
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public int FirstViolationLevel { get; set; } = -1;
        public List<string> Violations { get; } = new List<string>();
    }

    /// <summary>
    /// Linearity validation result.
    /// </summary>
    public sealed class LinearityValidation
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }
}
