/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Rules;

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Quality thresholds for level acceptance based on difficulty band.
    /// Levels failing any threshold are rejected during generation.
    /// </summary>
    public sealed class QualityThresholds
    {
        /// <summary>
        /// Maximum allowed forced-move ratio (fraction of single-option states).
        /// </summary>
        public float MaxForcedMoveRatio { get; }

        /// <summary>
        /// Maximum allowed decision depth (steps to first branching point).
        /// </summary>
        public int MaxDecisionDepth { get; }

        /// <summary>
        /// Minimum required average branching factor.
        /// </summary>
        public float MinBranchingFactor { get; }

        /// <summary>
        /// Minimum required trap score (penalty for wrong moves).
        /// </summary>
        public float MinTrapScore { get; }

        /// <summary>
        /// Minimum required solution multiplicity.
        /// </summary>
        public int MinSolutionMultiplicity { get; }

        /// <summary>
        /// Maximum allowed empty-bottle usage ratio.
        /// </summary>
        public float MaxEmptyBottleUsageRatio { get; }

        /// <summary>
        /// Minimum required mixed bottle count.
        /// </summary>
        public int MinMixedBottles { get; }

        /// <summary>
        /// Minimum required distinct bottle signatures.
        /// </summary>
        public int MinDistinctSignatures { get; }

        public QualityThresholds(
            float maxForcedMoveRatio,
            int maxDecisionDepth,
            float minBranchingFactor,
            float minTrapScore,
            int minSolutionMultiplicity,
            float maxEmptyBottleUsageRatio,
            int minMixedBottles,
            int minDistinctSignatures)
        {
            MaxForcedMoveRatio = maxForcedMoveRatio;
            MaxDecisionDepth = maxDecisionDepth;
            MinBranchingFactor = minBranchingFactor;
            MinTrapScore = minTrapScore;
            MinSolutionMultiplicity = minSolutionMultiplicity;
            MaxEmptyBottleUsageRatio = maxEmptyBottleUsageRatio;
            MinMixedBottles = minMixedBottles;
            MinDistinctSignatures = minDistinctSignatures;
        }

        /// <summary>
        /// Gets quality thresholds appropriate for the given difficulty band.
        /// Thresholds are progressively stricter for higher bands.
        /// </summary>
        public static QualityThresholds ForBand(LevelBand band)
        {
            switch (band)
            {
                case LevelBand.A:
                    // Early levels: lenient thresholds to ensure playability
                    return new QualityThresholds(
                        maxForcedMoveRatio: 0.70f,
                        maxDecisionDepth: 4,
                        minBranchingFactor: 1.2f,
                        minTrapScore: 0.05f,
                        minSolutionMultiplicity: 1,
                        maxEmptyBottleUsageRatio: 0.80f,
                        minMixedBottles: 1,
                        minDistinctSignatures: 2);

                case LevelBand.B:
                    // Intermediate: moderate thresholds
                    return new QualityThresholds(
                        maxForcedMoveRatio: 0.60f,
                        maxDecisionDepth: 3,
                        minBranchingFactor: 1.3f,
                        minTrapScore: 0.10f,
                        minSolutionMultiplicity: 1,
                        maxEmptyBottleUsageRatio: 0.70f,
                        minMixedBottles: 2,
                        minDistinctSignatures: 3);

                case LevelBand.C:
                    // Mid-game: stricter thresholds
                    return new QualityThresholds(
                        maxForcedMoveRatio: 0.55f,
                        maxDecisionDepth: 2,
                        minBranchingFactor: 1.4f,
                        minTrapScore: 0.15f,
                        minSolutionMultiplicity: 1,
                        maxEmptyBottleUsageRatio: 0.60f,
                        minMixedBottles: 2,
                        minDistinctSignatures: 3);

                case LevelBand.D:
                    // Advanced: demanding thresholds
                    return new QualityThresholds(
                        maxForcedMoveRatio: 0.50f,
                        maxDecisionDepth: 2,
                        minBranchingFactor: 1.5f,
                        minTrapScore: 0.18f,
                        minSolutionMultiplicity: 1,
                        maxEmptyBottleUsageRatio: 0.55f,
                        minMixedBottles: 3,
                        minDistinctSignatures: 4);

                case LevelBand.E:
                default:
                    // Expert: strictest thresholds
                    return new QualityThresholds(
                        maxForcedMoveRatio: 0.45f,
                        maxDecisionDepth: 2,
                        minBranchingFactor: 1.5f,
                        minTrapScore: 0.20f,
                        minSolutionMultiplicity: 1,
                        maxEmptyBottleUsageRatio: 0.50f,
                        minMixedBottles: 3,
                        minDistinctSignatures: 4);
            }
        }

        /// <summary>
        /// Checks if the given metrics pass all quality thresholds.
        /// </summary>
        public bool Passes(LevelMetrics metrics, out string failureReason)
        {
            if (metrics.ForcedMoveRatio > MaxForcedMoveRatio)
            {
                failureReason = $"ForcedMoveRatio {metrics.ForcedMoveRatio:F2} > {MaxForcedMoveRatio:F2}";
                return false;
            }

            if (metrics.DecisionDepth > MaxDecisionDepth)
            {
                failureReason = $"DecisionDepth {metrics.DecisionDepth} > {MaxDecisionDepth}";
                return false;
            }

            if (metrics.AverageBranchingFactor < MinBranchingFactor)
            {
                failureReason = $"BranchingFactor {metrics.AverageBranchingFactor:F2} < {MinBranchingFactor:F2}";
                return false;
            }

            if (metrics.TrapScore < MinTrapScore)
            {
                failureReason = $"TrapScore {metrics.TrapScore:F2} < {MinTrapScore:F2}";
                return false;
            }

            if (metrics.SolutionMultiplicity < MinSolutionMultiplicity)
            {
                failureReason = $"SolutionMultiplicity {metrics.SolutionMultiplicity} < {MinSolutionMultiplicity}";
                return false;
            }

            if (metrics.EmptyBottleUsageRatio > MaxEmptyBottleUsageRatio)
            {
                failureReason = $"EmptyBottleUsageRatio {metrics.EmptyBottleUsageRatio:F2} > {MaxEmptyBottleUsageRatio:F2}";
                return false;
            }

            if (metrics.MixedBottleCount < MinMixedBottles)
            {
                failureReason = $"MixedBottles {metrics.MixedBottleCount} < {MinMixedBottles}";
                return false;
            }

            if (metrics.DistinctSignatureCount < MinDistinctSignatures)
            {
                failureReason = $"DistinctSignatures {metrics.DistinctSignatureCount} < {MinDistinctSignatures}";
                return false;
            }

            failureReason = null;
            return true;
        }

        public override string ToString()
        {
            return $"QualityThresholds[FMR<={MaxForcedMoveRatio:F2}, DD<={MaxDecisionDepth}, ABF>={MinBranchingFactor:F2}, TS>={MinTrapScore:F2}, SM>={MinSolutionMultiplicity}, EBUR<={MaxEmptyBottleUsageRatio:F2}]";
        }
    }
}
