/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;
using System.Collections.Generic;

namespace Decantra.Tests.EditMode
{
    public class LevelMetricsTests
    {
        [Test]
        public void LevelMetrics_Construction_SetsAllProperties()
        {
            var metrics = new LevelMetrics(
                forcedMoveRatio: 0.5f,
                averageBranchingFactor: 2.0f,
                decisionDepth: 2,
                emptyBottleUsageRatio: 0.3f,
                trapScore: 0.2f,
                solutionMultiplicity: 2,
                mixedBottleCount: 3,
                distinctSignatureCount: 4,
                topColorVariety: 3);

            Assert.AreEqual(0.5f, metrics.ForcedMoveRatio, 0.001f);
            Assert.AreEqual(2.0f, metrics.AverageBranchingFactor, 0.001f);
            Assert.AreEqual(2, metrics.DecisionDepth);
            Assert.AreEqual(0.3f, metrics.EmptyBottleUsageRatio, 0.001f);
            Assert.AreEqual(0.2f, metrics.TrapScore, 0.001f);
            Assert.AreEqual(2, metrics.SolutionMultiplicity);
            Assert.AreEqual(3, metrics.MixedBottleCount);
            Assert.AreEqual(4, metrics.DistinctSignatureCount);
            Assert.AreEqual(3, metrics.TopColorVariety);
        }

        [Test]
        public void LevelMetrics_Empty_ReturnsDefaultValues()
        {
            var empty = LevelMetrics.Empty;

            Assert.AreEqual(1f, empty.ForcedMoveRatio, 0.001f);
            Assert.AreEqual(1f, empty.AverageBranchingFactor, 0.001f);
            Assert.AreEqual(0, empty.DecisionDepth);
            Assert.AreEqual(1f, empty.EmptyBottleUsageRatio, 0.001f);
            Assert.AreEqual(0f, empty.TrapScore, 0.001f);
            Assert.AreEqual(1, empty.SolutionMultiplicity);
        }

        [Test]
        public void PathMetrics_ComputePathMetrics_KnownPuzzle()
        {
            // Create a simple puzzle with known solution path
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var solver = new BfsSolver();
            var result = solver.SolveWithPath(state);

            Assert.IsTrue(result.OptimalMoves > 0);
            Assert.IsTrue(result.Path.Count > 0);

            var pathMetrics = MetricsComputer.ComputePathMetrics(state, result.Path);

            // Verify metrics are within valid ranges
            Assert.GreaterOrEqual(pathMetrics.ForcedMoveRatio, 0f);
            Assert.LessOrEqual(pathMetrics.ForcedMoveRatio, 1f);
            Assert.GreaterOrEqual(pathMetrics.AverageBranchingFactor, 1f);
            Assert.GreaterOrEqual(pathMetrics.DecisionDepth, 0);
            Assert.GreaterOrEqual(pathMetrics.EmptyBottleUsageRatio, 0f);
            Assert.LessOrEqual(pathMetrics.EmptyBottleUsageRatio, 1f);
        }

        [Test]
        public void PathMetrics_ForcedMoveRatio_AllForcedMoves()
        {
            // Create state where there's only one legal move
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null })
            }, 0, 10, 1, 1, 1);

            var solver = new BfsSolver();
            var result = solver.SolveWithPath(state);

            if (result.OptimalMoves > 0 && result.Path.Count > 0)
            {
                var pathMetrics = MetricsComputer.ComputePathMetrics(state, result.Path);
                // With limited moves available, forced move ratio should be high
                Assert.GreaterOrEqual(pathMetrics.ForcedMoveRatio, 0f);
            }
        }

        [Test]
        public void StructuralMetrics_ComputeStructuralMetrics_MixedBottles()
        {
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Blue, ColorId.Red, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Red, ColorId.Blue, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var metrics = MetricsComputer.ComputeStructuralMetrics(state);

            Assert.AreEqual(2, metrics.MixedBottleCount, "Both non-empty bottles should be mixed");
            Assert.AreEqual(2, metrics.DistinctSignatureCount, "Two distinct signatures expected");
            Assert.AreEqual(2, metrics.TopColorVariety, "Two top colors (Red and Blue)");
        }

        [Test]
        public void StructuralMetrics_ComputeStructuralMetrics_SingleColorBottles()
        {
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var metrics = MetricsComputer.ComputeStructuralMetrics(state);

            Assert.AreEqual(0, metrics.MixedBottleCount, "No mixed bottles");
            Assert.AreEqual(2, metrics.DistinctSignatureCount);
            Assert.AreEqual(2, metrics.TopColorVariety);
        }

        [Test]
        public void SolutionMultiplicity_EstimateSolutionMultiplicity_SimplePuzzle()
        {
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var solver = new BfsSolver();
            var result = solver.SolveOptimal(state);

            int multiplicity = MetricsComputer.EstimateSolutionMultiplicity(state, result.OptimalMoves, maxSolutions: 3, nearOptimalMargin: 1);

            Assert.GreaterOrEqual(multiplicity, 1, "Should find at least one solution");
        }

        [Test]
        public void TrapScore_ComputeTrapScore_WithNonOptimalMoves()
        {
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var solver = new BfsSolver();
            var result = solver.SolveWithPath(state);

            if (result.Path.Count > 0)
            {
                float trapScore = MetricsComputer.ComputeTrapScore(state, result.Path, solver, sampleCount: 5, nodeBudget: 1000);

                Assert.GreaterOrEqual(trapScore, 0f);
                Assert.LessOrEqual(trapScore, 1f);
            }
        }
    }
}
