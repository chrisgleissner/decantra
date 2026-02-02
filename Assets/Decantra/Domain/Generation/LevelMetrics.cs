/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Metrics describing the decision density and strategic complexity of a generated level.
    /// Used for quality gating during level generation.
    /// </summary>
    public sealed class LevelMetrics
    {
        /// <summary>
        /// Fraction of states along the optimal path with exactly one legal move (0.0 to 1.0).
        /// Lower is better - indicates more choice points.
        /// </summary>
        public float ForcedMoveRatio { get; }

        /// <summary>
        /// Average number of legal moves available at each state along the optimal path.
        /// Higher is better - indicates more branching.
        /// </summary>
        public float AverageBranchingFactor { get; }

        /// <summary>
        /// Maximum length of any forced-move streak along the optimal path.
        /// Lower is better - indicates decisions occur more frequently.
        /// </summary>
        public int DecisionDepth { get; }

        /// <summary>
        /// Fraction of optimal moves that pour into empty bottles (0.0 to 1.0).
        /// Lower values indicate less reliance on empty bottles as staging buffers.
        /// </summary>
        public float EmptyBottleUsageRatio { get; }

        /// <summary>
        /// Fraction of sampled non-optimal moves that lead to harder or unsolvable states (0.0 to 1.0).
        /// Higher is better - indicates meaningful consequences for wrong choices.
        /// </summary>
        public float TrapScore { get; }

        /// <summary>
        /// Estimated number of optimal or near-optimal solution paths (capped at a small value).
        /// Higher indicates more solution diversity. 0 means could not determine.
        /// </summary>
        public int SolutionMultiplicity { get; }

        /// <summary>
        /// Number of bottles with mixed colors (more than one color).
        /// </summary>
        public int MixedBottleCount { get; }

        /// <summary>
        /// Number of distinct bottle content signatures.
        /// </summary>
        public int DistinctSignatureCount { get; }

        /// <summary>
        /// Number of distinct colors appearing at top positions across all bottles.
        /// </summary>
        public int TopColorVariety { get; }

        public LevelMetrics(
            float forcedMoveRatio,
            float averageBranchingFactor,
            int decisionDepth,
            float emptyBottleUsageRatio,
            float trapScore,
            int solutionMultiplicity,
            int mixedBottleCount,
            int distinctSignatureCount,
            int topColorVariety)
        {
            ForcedMoveRatio = forcedMoveRatio;
            AverageBranchingFactor = averageBranchingFactor;
            DecisionDepth = decisionDepth;
            EmptyBottleUsageRatio = emptyBottleUsageRatio;
            TrapScore = trapScore;
            SolutionMultiplicity = solutionMultiplicity;
            MixedBottleCount = mixedBottleCount;
            DistinctSignatureCount = distinctSignatureCount;
            TopColorVariety = topColorVariety;
        }

        /// <summary>
        /// Creates empty metrics with default values (used for fallback/error cases).
        /// </summary>
        public static LevelMetrics Empty => new LevelMetrics(1f, 1f, 0, 1f, 0f, 1, 0, 0, 0);

        public override string ToString()
        {
            return $"LevelMetrics[FMR={ForcedMoveRatio:F2}, ABF={AverageBranchingFactor:F2}, DD={DecisionDepth}, EBUR={EmptyBottleUsageRatio:F2}, TS={TrapScore:F2}, SM={SolutionMultiplicity}, Mixed={MixedBottleCount}, Sigs={DistinctSignatureCount}, TopVar={TopColorVariety}]";
        }
    }
}
