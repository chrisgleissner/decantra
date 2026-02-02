/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Difficulty objective function for scoring scramble candidates.
    /// Higher scores indicate more interesting/challenging levels.
    /// </summary>
    public sealed class DifficultyObjective
    {
        private readonly float _weightBranching;
        private readonly float _weightTrap;
        private readonly float _weightDecision;
        private readonly float _weightMultiplicity;
        private readonly float _weightEmptyUsage;

        public DifficultyObjective(
            float weightBranching = 0.25f,
            float weightTrap = 0.30f,
            float weightDecision = 0.20f,
            float weightMultiplicity = 0.15f,
            float weightEmptyUsage = 0.10f)
        {
            _weightBranching = weightBranching;
            _weightTrap = weightTrap;
            _weightDecision = weightDecision;
            _weightMultiplicity = weightMultiplicity;
            _weightEmptyUsage = weightEmptyUsage;
        }

        /// <summary>
        /// Computes a difficulty score for the given metrics.
        /// Higher scores indicate more desirable levels (more decision points, trap potential, etc.).
        /// Score is normalized to approximately 0.0-1.0 range.
        /// </summary>
        public float Score(LevelMetrics metrics)
        {
            if (metrics == null) return 0f;

            // Branching factor component: normalize to ~0-1 range (typical range 1.0-3.0)
            float branchingScore = Clamp01((metrics.AverageBranchingFactor - 1f) / 2f);

            // Trap score: already 0-1
            float trapScore = Clamp01(metrics.TrapScore);

            // Forced-move streak length: lower is better, normalize (typical range 0-10)
            float decisionScore = Clamp01(1f - metrics.DecisionDepth / 10f);

            // Multiplicity: more is better, normalize (typical range 1-5)
            float multiplicityScore = Clamp01((metrics.SolutionMultiplicity - 1f) / 4f);

            // Empty bottle usage: lower is better
            float emptyUsageScore = Clamp01(1f - metrics.EmptyBottleUsageRatio);

            // Forced move ratio penalty: applied separately (not a reward, but absence is good)
            float forcedMoveBonus = Clamp01(1f - metrics.ForcedMoveRatio);

            return _weightBranching * branchingScore
                 + _weightTrap * trapScore
                 + _weightDecision * decisionScore
                 + _weightMultiplicity * multiplicityScore
                 + _weightEmptyUsage * emptyUsageScore
                 + 0.15f * forcedMoveBonus; // bonus for low forced-move ratio
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        /// <summary>
        /// Default objective weights balanced for general difficulty.
        /// </summary>
        public static DifficultyObjective Default => new DifficultyObjective();

        /// <summary>
        /// Objective weights favoring trap potential (for harder levels).
        /// </summary>
        public static DifficultyObjective TrapFocused => new DifficultyObjective(
            weightBranching: 0.20f,
            weightTrap: 0.40f,
            weightDecision: 0.15f,
            weightMultiplicity: 0.15f,
            weightEmptyUsage: 0.10f);

        /// <summary>
        /// Objective weights favoring branching (for more strategic levels).
        /// </summary>
        public static DifficultyObjective BranchingFocused => new DifficultyObjective(
            weightBranching: 0.35f,
            weightTrap: 0.20f,
            weightDecision: 0.25f,
            weightMultiplicity: 0.10f,
            weightEmptyUsage: 0.10f);
    }
}
