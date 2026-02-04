/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using Decantra.Domain.Rules;

namespace Decantra.Domain.Generation
{
    /// <summary>Computes 1..100 difficulty scores, structurally bound to level index for monotonicity.</summary>
    public static class DifficultyScorer
    {
        public const int MinOptimalMovesForMaxDifficulty = 8;
        public const float MinTrapScoreForMaxDifficulty = 0.5f;
        public const float SoftOptimalPenalty = 0.85f;

        /// <summary>Computes normalized [0..1] complexity based on intrinsic puzzle metrics.</summary>
        public static double ComputeDifficulty01(LevelMetrics metrics, int optimalMoves)
        {
            if (metrics == null) return 0.0;
            if (optimalMoves < 0) optimalMoves = 0;

            double lenNorm = Clamp01(optimalMoves / 15.0);
            double forcedNorm = 1.0 - Clamp01(metrics.ForcedMoveRatio / 0.70);
            double branchNorm = Clamp01((metrics.AverageBranchingFactor - 1.0) / 4.0);
            double decisionNorm = 1.0 - Clamp01(metrics.DecisionDepth / 5.0);
            double trapNorm = Clamp01(metrics.TrapScore);
            double multiNorm = 1.0 - Clamp01((metrics.SolutionMultiplicity - 1) / 3.0);

            // Weighted aggregation of complexity factors
            return Clamp01(
                (0.30 * lenNorm) +
                (0.15 * forcedNorm) +
                (0.15 * branchNorm) +
                (0.10 * decisionNorm) +
                (0.20 * trapNorm) +
                (0.10 * multiNorm));
        }

        /// <summary>Returns deterministic difficulty bound to level index.</summary>
        public static int ComputeDifficulty100(LevelMetrics metrics, int optimalMoves, int levelIndex = 0)
        {
            return LevelDifficultyEngine.GetDifficultyForLevel(levelIndex);
        }

        /// <summary>Metric-based 1..100 score for assessment when level index is unknown.</summary>
        public static int ComputeDifficulty100(LevelMetrics metrics, int optimalMoves)
        {
            double intrinsic01 = ComputeDifficulty01(metrics, optimalMoves);
            double curved01 = Math.Pow(intrinsic01, 0.80);
            int rawScore = 1 + (int)Math.Round(curved01 * 99);
            return Math.Max(1, Math.Min(100, rawScore));
        }

        public static int TargetDifficultyForLevel(int levelIndex) => LevelDifficultyEngine.GetDifficultyForLevel(levelIndex);

        public static int MinDifficultyForLevel(int levelIndex) => TargetDifficultyForLevel(levelIndex);

        /// <summary>Scales minimum optimal moves from 3 (Level 1) to 12 (Level 100).</summary>
        public static int SoftMinOptimalForLevel(int levelIndex)
        {
            int eff = LevelDifficultyEngine.GetEffectiveLevel(Math.Max(1, levelIndex));
            return 3 + (int)Math.Round((eff - 1) * 9.0 / 99.0);
        }

        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
    }
}