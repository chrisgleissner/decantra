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
    /// Computes raw complexity scores from solver-observed metrics.
    /// Does NOT use level number or designer intent.
    /// Stage 1 of the two-stage difficulty system.
    /// </summary>
    public static class ComplexityScorer
    {
        /// <summary>
        /// Computes unbounded raw complexity score from solver metrics.
        /// Returns a continuous score with natural variance.
        /// Higher = more complex puzzle.
        /// </summary>
        /// <param name="metrics">Solver-observed metrics</param>
        /// <param name="optimalMoves">Optimal solution length</param>
        /// <returns>Raw complexity score (unbounded, typically 0-50)</returns>
        public static double ComputeRawComplexity(LevelMetrics metrics, int optimalMoves)
        {
            if (metrics == null || optimalMoves < 0)
                return 0.0;

            // Component 1: Solution length (log scale to reduce variance compression)
            // log(1) = 0, log(20) ≈ 3.0
            double moveComplexity = Math.Log(Math.Max(1, optimalMoves));

            // Component 2: Branching entropy (Shannon entropy of decision tree)
            // Higher branching = more exploration required
            double branchingEntropy = ComputeBranchingEntropy(metrics.AverageBranchingFactor);

            // Component 3: Trap depth (actual cost, not binary)
            // Measures how much harder wrong moves make the puzzle
            double trapDepth = ComputeTrapDepth(metrics.TrapScore);

            // Component 4: Decision density (inverse of forced-move streaks)
            // More decisions = higher complexity
            double decisionDensity = 1.0 - metrics.ForcedMoveRatio;

            // Component 5: Early decision pressure
            // Lower decision depth = harder (immediate choices)
            double earlyDecisionPressure = ComputeEarlyDecisionPressure(metrics.DecisionDepth);

            // Component 6: Solution uniqueness (inverse of multiplicity)
            // Fewer solutions = harder to find
            double uniqueness = ComputeUniqueness(metrics.SolutionMultiplicity);

            // Weighted combination (no normalization, preserve variance)
            double rawScore =
                  4.0 * moveComplexity            // Emphasize solution length
                + 3.0 * branchingEntropy          // Branching exploration
                + 5.0 * trapDepth                 // Trap consequences
                + 2.5 * decisionDensity           // Choice frequency
                + 2.0 * earlyDecisionPressure     // Immediate difficulty
                + 1.5 * uniqueness;               // Solution scarcity

            return rawScore;
        }

        /// <summary>
        /// Computes branching entropy from average branching factor.
        /// Uses Shannon entropy approximation: H ≈ log2(b) where b is branching factor.
        /// </summary>
        private static double ComputeBranchingEntropy(float avgBranching)
        {
            if (avgBranching <= 1.0f)
                return 0.0;

            // Shannon entropy for uniform branching
            // log2(2) = 1.0, log2(10) ≈ 3.32
            return Math.Log(avgBranching, 2.0);
        }

        /// <summary>
        /// Computes trap depth from trap score.
        /// Trap score is already 0-1, but we want to emphasize high trap scores.
        /// </summary>
        private static double ComputeTrapDepth(float trapScore)
        {
            // Exponential emphasis: traps are very important for difficulty
            // 0.0 -> 0.0, 0.5 -> 0.25, 1.0 -> 1.0
            return trapScore * trapScore;
        }

        /// <summary>
        /// Computes early decision pressure from decision depth.
        /// Lower depth = higher pressure (decisions required immediately).
        /// </summary>
        private static double ComputeEarlyDecisionPressure(int decisionDepth)
        {
            if (decisionDepth <= 0)
                return 1.0; // Maximum pressure: decision at start

            // Inverse relationship with saturation
            // depth=0 -> 1.0, depth=3 -> 0.25, depth=10 -> ~0.09
            return 1.0 / (1.0 + decisionDepth / 3.0);
        }

        /// <summary>
        /// Computes solution uniqueness from multiplicity.
        /// Fewer alternative solutions = higher uniqueness = harder.
        /// </summary>
        private static double ComputeUniqueness(int multiplicity)
        {
            if (multiplicity <= 1)
                return 1.0; // Unique solution

            // Inverse with diminishing returns
            // mult=1 -> 1.0, mult=2 -> 0.5, mult=3 -> 0.33, mult=5 -> 0.2
            return 1.0 / multiplicity;
        }

        /// <summary>
        /// Validates that raw complexity scores have sufficient variance.
        /// Returns false if metrics appear saturated or degenerate.
        /// </summary>
        public static bool ValidateVariance(double[] rawScores)
        {
            if (rawScores == null || rawScores.Length < 100)
                return false;

            // Compute standard deviation
            double mean = 0.0;
            foreach (var score in rawScores)
                mean += score;
            mean /= rawScores.Length;

            double variance = 0.0;
            foreach (var score in rawScores)
            {
                double diff = score - mean;
                variance += diff * diff;
            }
            variance /= rawScores.Length;
            double stdDev = Math.Sqrt(variance);

            // Coefficient of variation should be > 0.15 (15% relative variation)
            double cv = stdDev / Math.Max(0.001, mean);

            return cv > 0.15;
        }

        /// <summary>
        /// Validates that raw complexity is not artificially correlated with level number.
        /// Returns false if correlation is too high (indicates cheating).
        /// </summary>
        public static bool ValidateIndependence(double[] rawScores, double maxCorrelation = 0.95)
        {
            if (rawScores == null || rawScores.Length < 10)
                return false;

            // Compute Pearson correlation between raw scores and level indices
            int n = rawScores.Length;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;

            for (int i = 0; i < n; i++)
            {
                double x = i + 1; // Level number (1-based)
                double y = rawScores[i];

                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            double numerator = n * sumXY - sumX * sumY;
            double denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));

            if (Math.Abs(denominator) < 0.0001)
                return false; // Degenerate

            double correlation = numerator / denominator;

            return Math.Abs(correlation) < maxCorrelation;
        }
    }
}
