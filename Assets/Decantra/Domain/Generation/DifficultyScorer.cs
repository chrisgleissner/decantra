/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Computes objective difficulty scores (1..100) for generated levels.
    /// 
    /// Design principles:
    /// - Difficulty increases LINEARLY from 1 at level 1 to 100 at level 1000
    /// - Maximum difficulty (100) requires: ≥15 optimal moves AND high trap score
    /// - The floor/ceiling system enforces strict monotonic progression
    /// </summary>
    public static class DifficultyScorer
    {
        /// <summary>
        /// Minimum optimal moves required for maximum difficulty (100).
        /// Levels with fewer moves are capped below 100.
        /// Set to 6 to allow most late-level puzzles to qualify.
        /// </summary>
        public const int MinOptimalMovesForMaxDifficulty = 6;

        /// <summary>
        /// Minimum trap score required for maximum difficulty (100).
        /// Levels with lower trap scores are capped below 100.
        /// </summary>
        public const float MinTrapScoreForMaxDifficulty = 0.8f;

        /// <summary>
        /// Computes a normalized difficulty score in [0..1] range based on metrics only.
        /// This measures intrinsic puzzle complexity independent of level index.
        /// </summary>
        public static double ComputeDifficulty01(LevelMetrics metrics, int optimalMoves)
        {
            if (metrics == null) return 0.0;
            if (optimalMoves < 0) optimalMoves = 0;

            // Normalization - weights adjusted to emphasize moves and traps
            double lenNorm = Clamp01(optimalMoves / 20.0);
            double forcedNorm = 1.0 - Clamp01(metrics.ForcedMoveRatio / 0.75);
            double branchNorm = Clamp01((metrics.AverageBranchingFactor - 1.0) / 3.0);
            double decisionNorm = 1.0 - Clamp01(metrics.DecisionDepth / 4.0);
            double trapNorm = Clamp01(metrics.TrapScore);
            double multiNorm = 1.0 - Clamp01((metrics.SolutionMultiplicity - 1) / 2.0);

            // Weighted aggregation - emphasize moves and traps for "real" difficulty
            double rawDifficulty =
                0.25 * lenNorm       // Higher weight: more moves = harder
              + 0.15 * forcedNorm
              + 0.15 * branchNorm
              + 0.10 * decisionNorm
              + 0.25 * trapNorm      // Higher weight: more traps = harder
              + 0.10 * multiNorm;

            return Clamp01(rawDifficulty);
        }

        /// <summary>
        /// Computes the intrinsic complexity score (1..100) based on metrics.
        /// 
        /// This creates an objective measure of puzzle complexity:
        /// - 1 = Trivial (level 1-5 complexity)
        /// - 100 = Maximum complexity (requires deep search, high optimal moves, traps)
        /// 
        /// Note: The 'levelIndex' parameter is kept for API compatibility but is NO LONGER used 
        /// for linear targeting. Complexity is purely intrinsic.
        /// </summary>
        public static int ComputeDifficulty100(LevelMetrics metrics, int optimalMoves, int levelIndex = 0)
        {
            double intrinsic01 = ComputeDifficulty01(metrics, optimalMoves);

            // Apply curve to distribute scores better across the 1-100 range
            // Power < 1 boosts lower scores (makes curve less steep initially)
            double curved01 = Math.Pow(intrinsic01, 0.85);

            // Scale directly to 1-100 without any level-based clamping
            int raw = 1 + (int)Math.Round(curved01 * 99);
            return Math.Max(1, Math.Min(100, raw));
        }

        /// <summary>
        /// Legacy overload without level index.
        /// </summary>
        public static int ComputeDifficulty100(LevelMetrics metrics, int optimalMoves)
        {
            return ComputeDifficulty100(metrics, optimalMoves, 0);
        }

        /// <summary>
        /// Returns the minimum intrinsic complexity expected for a given level.
        /// This defines the 'Complexity Curve' of the game.
        /// </summary>
        public static int MinDifficultyForLevel(int levelIndex)
        {
            // Placeholder: currently returns 1 to allow any complexity
            // Should be replaced by a lookup table or curve function once calibrated
            return 1;
        }

        /// <summary>
        /// Computes the soft minimum optimal moves for a given level.
        /// Higher levels should have longer solutions.
        /// </summary>
        public static int SoftMinOptimalForLevel(int levelIndex)
        {
            if (levelIndex < 1) levelIndex = 1;
            // Scale from 3 at level 1 to 15 at level 1000
            return 3 + (int)Math.Round((levelIndex - 1) * 12.0 / 999.0);
        }

        /// <summary>
        /// Penalty factor for candidates below soft minimum optimal.
        /// </summary>
        public const float SoftOptimalPenalty = 0.85f;

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
