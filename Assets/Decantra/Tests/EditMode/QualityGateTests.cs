/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class QualityGateTests
    {
        [Test]
        public void QualityThresholds_ForBand_ReturnsValidThresholds()
        {
            foreach (LevelBand band in System.Enum.GetValues(typeof(LevelBand)))
            {
                var thresholds = QualityThresholds.ForBand(band);

                Assert.IsNotNull(thresholds);
                Assert.Greater(thresholds.MaxForcedMoveRatio, 0f);
                Assert.LessOrEqual(thresholds.MaxForcedMoveRatio, 1f);
                Assert.Greater(thresholds.MaxDecisionDepth, 0);
                Assert.Greater(thresholds.MinBranchingFactor, 0f);
                Assert.GreaterOrEqual(thresholds.MinTrapScore, 0f);
                Assert.LessOrEqual(thresholds.MinTrapScore, 1f);
                Assert.GreaterOrEqual(thresholds.MinSolutionMultiplicity, 1);
            }
        }

        [Test]
        public void QualityThresholds_BandProgression_GetsStricter()
        {
            var bandA = QualityThresholds.ForBand(LevelBand.A);
            var bandC = QualityThresholds.ForBand(LevelBand.C);
            var bandE = QualityThresholds.ForBand(LevelBand.E);

            // Forced move ratio should decrease (stricter)
            Assert.GreaterOrEqual(bandA.MaxForcedMoveRatio, bandC.MaxForcedMoveRatio);
            Assert.GreaterOrEqual(bandC.MaxForcedMoveRatio, bandE.MaxForcedMoveRatio);

            // Branching factor minimum should increase (stricter)
            Assert.LessOrEqual(bandA.MinBranchingFactor, bandC.MinBranchingFactor);
            Assert.LessOrEqual(bandC.MinBranchingFactor, bandE.MinBranchingFactor);

            // Trap score minimum should increase (stricter)
            Assert.LessOrEqual(bandA.MinTrapScore, bandC.MinTrapScore);
            Assert.LessOrEqual(bandC.MinTrapScore, bandE.MinTrapScore);
        }

        [Test]
        public void QualityThresholds_Passes_GoodMetrics()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.A);

            var goodMetrics = new LevelMetrics(
                forcedMoveRatio: 0.4f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            bool passes = thresholds.Passes(goodMetrics, out string reason);

            Assert.IsTrue(passes, $"Good metrics should pass. Failure: {reason}");
            Assert.IsNull(reason);
        }

        [Test]
        public void QualityThresholds_Fails_HighForcedMoveRatio()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.C);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.9f, // Too high
                averageBranchingFactor: 2.0f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("ForcedMoveRatio"));
        }

        [Test]
        public void QualityThresholds_Fails_LowBranchingFactor()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.C);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.4f,
                averageBranchingFactor: 1.0f, // Too low
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("BranchingFactor"));
        }

        [Test]
        public void QualityThresholds_Fails_LowTrapScore()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.E);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.3f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.4f,
                trapScore: 0.01f, // Too low for Band E
                solutionMultiplicity: 2,
                mixedBottleCount: 4,
                distinctSignatureCount: 5,
                topColorVariety: 4);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("TrapScore"));
        }

        [Test]
        public void QualityThresholds_Fails_HighDecisionDepth()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.C);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.4f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 10, // Too high
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("DecisionDepth"));
        }

        [Test]
        public void QualityThresholds_Fails_HighEmptyBottleUsage()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.E);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.3f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.9f, // Too high
                trapScore: 0.25f,
                solutionMultiplicity: 2,
                mixedBottleCount: 4,
                distinctSignatureCount: 5,
                topColorVariety: 4);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("EmptyBottleUsageRatio"));
        }

        [Test]
        public void QualityThresholds_Fails_LowMixedBottles()
        {
            var thresholds = QualityThresholds.ForBand(LevelBand.C);

            var badMetrics = new LevelMetrics(
                forcedMoveRatio: 0.4f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 1, // Too low for Band C
                distinctSignatureCount: 4,
                topColorVariety: 3);

            bool passes = thresholds.Passes(badMetrics, out string reason);

            Assert.IsFalse(passes);
            Assert.IsNotNull(reason);
            Assert.IsTrue(reason.Contains("MixedBottles"));
        }

        [Test]
        public void DifficultyObjective_Score_ReturnsValidRange()
        {
            var objective = DifficultyObjective.Default;

            var metrics = new LevelMetrics(
                forcedMoveRatio: 0.5f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 2,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.3f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            float score = objective.Score(metrics);

            Assert.GreaterOrEqual(score, 0f);
            Assert.LessOrEqual(score, 1.5f); // Max possible score with bonus
        }

        [Test]
        public void DifficultyObjective_Score_HigherForBetterMetrics()
        {
            var objective = DifficultyObjective.Default;

            var poorMetrics = new LevelMetrics(
                forcedMoveRatio: 0.9f,
                averageBranchingFactor: 1.1f,
                decisionDepth: 8,
                emptyBottleUsageRatio: 0.9f,
                trapScore: 0.05f,
                solutionMultiplicity: 1,
                mixedBottleCount: 1,
                distinctSignatureCount: 2,
                topColorVariety: 2);

            var goodMetrics = new LevelMetrics(
                forcedMoveRatio: 0.3f,
                averageBranchingFactor: 2.5f,
                decisionDepth: 1,
                emptyBottleUsageRatio: 0.3f,
                trapScore: 0.4f,
                solutionMultiplicity: 3,
                mixedBottleCount: 4,
                distinctSignatureCount: 5,
                topColorVariety: 4);

            float poorScore = objective.Score(poorMetrics);
            float goodScore = objective.Score(goodMetrics);

            Assert.Greater(goodScore, poorScore, "Better metrics should have higher score");
        }

        [Test]
        public void DifficultyObjective_TrapFocused_WeightsTrapScoreHigher()
        {
            var trapFocused = DifficultyObjective.TrapFocused;
            var defaultObj = DifficultyObjective.Default;

            var highTrapMetrics = new LevelMetrics(
                forcedMoveRatio: 0.5f,
                averageBranchingFactor: 1.5f,
                decisionDepth: 2,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.8f, // High trap score
                solutionMultiplicity: 1,
                mixedBottleCount: 2,
                distinctSignatureCount: 3,
                topColorVariety: 2);

            var lowTrapMetrics = new LevelMetrics(
                forcedMoveRatio: 0.5f,
                averageBranchingFactor: 1.5f,
                decisionDepth: 2,
                emptyBottleUsageRatio: 0.5f,
                trapScore: 0.1f, // Low trap score
                solutionMultiplicity: 1,
                mixedBottleCount: 2,
                distinctSignatureCount: 3,
                topColorVariety: 2);

            float trapFocusedDiff = trapFocused.Score(highTrapMetrics) - trapFocused.Score(lowTrapMetrics);
            float defaultDiff = defaultObj.Score(highTrapMetrics) - defaultObj.Score(lowTrapMetrics);

            Assert.Greater(trapFocusedDiff, defaultDiff, "Trap-focused objective should show larger difference for trap score changes");
        }

        [Test]
        public void LevelGenerationReport_Construction_SetsAllProperties()
        {
            var metrics = LevelMetrics.Empty;
            var report = new LevelGenerationReport(
                levelIndex: 5,
                seed: 12345,
                attemptsUsed: 3,
                metrics: metrics,
                optimalMoves: 8,
                movesAllowed: 12,
                scrambleMoves: 15,
                generationTimeMs: 100,
                solverTimeMs: 50,
                metricsTimeMs: 20,
                difficultyScore: 0.75f,
                qualityGatesApplied: true,
                lastRejectionReason: null);

            Assert.AreEqual(5, report.LevelIndex);
            Assert.AreEqual(12345, report.Seed);
            Assert.AreEqual(3, report.AttemptsUsed);
            Assert.AreEqual(8, report.OptimalMoves);
            Assert.AreEqual(12, report.MovesAllowed);
            Assert.AreEqual(15, report.ScrambleMoves);
            Assert.AreEqual(100, report.GenerationTimeMs);
            Assert.AreEqual(50, report.SolverTimeMs);
            Assert.AreEqual(20, report.MetricsTimeMs);
            Assert.AreEqual(0.75f, report.DifficultyScore, 0.001f);
            Assert.IsTrue(report.QualityGatesApplied);
            Assert.IsNull(report.LastRejectionReason);
        }
    }
}
