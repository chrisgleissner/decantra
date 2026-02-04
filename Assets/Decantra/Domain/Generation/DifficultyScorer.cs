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
    /// <summary>Computes 1..100 difficulty scores based on intrinsic puzzle metrics.</summary>
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

        /// <summary>
        /// Returns intrinsic difficulty score (1-100) based on puzzle metrics.
        /// This is the TRUE difficulty experienced by the player.
        /// </summary>
        public static int ComputeDifficulty100(LevelMetrics metrics, int optimalMoves, int levelIndex = 0)
        {
            // Compute intrinsic difficulty from metrics (ignoring level index)
            return ComputeIntrinsicDifficulty100(metrics, optimalMoves);
        }

        /// <summary>
        /// Computes intrinsic difficulty (1-100) purely from puzzle metrics.
        /// This measures actual cognitive load on the player:
        /// - More moves = harder to plan ahead
        /// - Higher branching = more options to evaluate
        /// - Higher trap score = more dead-ends to avoid
        /// - Lower forced ratio = more decisions required
        /// - Lower multiplicity = fewer valid paths to discover
        /// </summary>
        public static int ComputeIntrinsicDifficulty100(LevelMetrics metrics, int optimalMoves)
        {
            if (metrics == null || optimalMoves <= 0) return 1;

            // Component weights designed for gradual increase then plateau
            // Max values: optimal=25, branch=20, trap=1.0, forcedRatio=0, multi=1

            // 1. Solution length contribution (0-40 points)
            // Short solutions (3-5) are easy; long solutions (15+) are hard
            double moveScore;
            if (optimalMoves <= 5)
                moveScore = optimalMoves * 4.0;  // 0-20 points for trivial puzzles
            else if (optimalMoves <= 12)
                moveScore = 20.0 + (optimalMoves - 5) * 2.5;  // 20-37.5 for medium
            else
                moveScore = 37.5 + Math.Min(2.5, (optimalMoves - 12) * 0.3);  // 37.5-40 plateau

            // 2. Branching factor contribution (0-25 points)
            // Branch=1 means forced moves (trivial)
            // Branch=3+ means many choices at each step
            double branchScore = Clamp01((metrics.AverageBranchingFactor - 1.0) / 3.5) * 25.0;

            // 3. Trap score contribution (0-20 points)
            // Traps create dead-ends that frustrate players
            // Quadratic emphasis - high trap scores are much harder
            double trapScore = metrics.TrapScore * metrics.TrapScore * 20.0;

            // 4. Decision frequency contribution (0-10 points)
            // More forced moves = easier (auto-play segments)
            double decisionScore = (1.0 - Clamp01(metrics.ForcedMoveRatio)) * 10.0;

            // 5. Solution uniqueness contribution (0-5 points)
            // Multiple solutions make puzzles easier to stumble into
            double uniqueScore;
            if (metrics.SolutionMultiplicity <= 1)
                uniqueScore = 5.0;
            else if (metrics.SolutionMultiplicity <= 3)
                uniqueScore = 3.0;
            else
                uniqueScore = 1.0;

            // Aggregate score (0-100)
            double rawScore = moveScore + branchScore + trapScore + decisionScore + uniqueScore;

            // Clamp to 1-100 range
            int finalScore = (int)Math.Round(rawScore);
            return Math.Max(1, Math.Min(100, finalScore));
        }

        /// <summary>Metric-based 1..100 score for assessment when level index is unknown.</summary>
        [Obsolete("Use ComputeIntrinsicDifficulty100 instead")]
        public static int ComputeDifficulty100_Legacy(LevelMetrics metrics, int optimalMoves)
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