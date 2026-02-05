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

        public static int CalculateLevelScore(int optimalMoves, int movesUsed, int movesAllowed, int difficulty100, bool cleanSolve)
        {
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));

            int slack = movesAllowed - optimalMoves;
            int delta = movesUsed - optimalMoves;

            double x;
            if (slack <= 0)
            {
                x = delta == 0 ? 1.0 : 0.0;
            }
            else
            {
                x = Clamp01(1.0 - delta / (double)slack);
            }

            // Scoring spec constants:
            // - Difficulty normalization focuses on 70..100 using (D - 70) / 30.
            // - Base = 60 + 60 * d^0.7 (curved to favor high difficulty band).
            // - PerfMult = 0.10 + 1.90 * x^4 (strongly nonlinear performance).
            // - Clean bonus = 25 for no undo/restart/hints.
            double d = Clamp01((difficulty100 - 70) / 30.0);
            double baseScore = 60.0 + 60.0 * Math.Pow(d, 0.7);
            double perfMult = 0.10 + 1.90 * Math.Pow(x, 4.0);
            int cleanBonus = cleanSolve ? 25 : 0;

            int levelScore = (int)Math.Round(baseScore * perfMult) + cleanBonus;
            return Math.Max(0, levelScore);
        }

        public static int CalculateStars(int optimalMoves, int movesUsed, int movesAllowed)
        {
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));

            int slack = movesAllowed - optimalMoves;
            int delta = movesUsed - optimalMoves;

            if (delta == 0) return 5;
            if (slack <= 0) return 0;

            // Star thresholds per spec: 20% bands of slack from 4 -> 0 stars.
            if (delta <= 0.2 * slack) return 4;
            if (delta <= 0.4 * slack) return 3;
            if (delta <= 0.6 * slack) return 2;
            if (delta <= 0.8 * slack) return 1;
            return 0;
        }

        public static int CalculateTotalScore(int currentTotal, int levelScore)
        {
            if (currentTotal < 0) throw new ArgumentOutOfRangeException(nameof(currentTotal));
            if (levelScore < 0) throw new ArgumentOutOfRangeException(nameof(levelScore));

            const int MaxTotal = 999999; // Hard cap per scoring spec.
            double decay = 1.0 - (currentTotal / (double)MaxTotal);
            int increment = (int)Math.Round(levelScore * decay);
            return Math.Min(MaxTotal, currentTotal + Math.Max(0, increment));
        }

        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
    }
}
