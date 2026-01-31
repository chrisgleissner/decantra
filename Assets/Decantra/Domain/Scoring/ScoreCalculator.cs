/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Scoring
{
    public static class ScoreCalculator
    {
        public static float CalculateEfficiency(int optimalMoves, int movesUsed)
        {
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (optimalMoves == 0) return 1f;

            int clampedMoves = Math.Max(movesUsed, optimalMoves);
            return optimalMoves / (float)clampedMoves;
        }

        public static PerformanceGrade CalculateGrade(int optimalMoves, int movesUsed)
        {
            float efficiency = CalculateEfficiency(optimalMoves, movesUsed);
            if (efficiency >= 1.0f) return PerformanceGrade.S;
            if (efficiency >= 0.9f) return PerformanceGrade.A;
            if (efficiency >= 0.8f) return PerformanceGrade.B;
            if (efficiency >= 0.66f) return PerformanceGrade.C;
            if (efficiency >= 0.5f) return PerformanceGrade.D;
            return PerformanceGrade.E;
        }

        public static int CalculateLevelScore(int levelIndex, int optimalMoves, int movesUsed, bool usedUndo, bool usedHints, int streak)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (streak < 0) throw new ArgumentOutOfRangeException(nameof(streak));

            int scaledLevel = Math.Max(1, levelIndex);
            float efficiency = CalculateEfficiency(optimalMoves, movesUsed);
            float baseScore = 180f + scaledLevel * 20f;

            float bonus = 1f;
            if (!usedUndo) bonus += 0.05f;
            if (!usedHints) bonus += 0.05f;
            bonus += Math.Min(5, streak) * 0.02f;

            float score = baseScore * efficiency * bonus;
            return Math.Max(0, (int)Math.Round(score));
        }
    }
}
