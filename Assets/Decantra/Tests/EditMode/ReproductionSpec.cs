using System.Collections.Generic;
using NUnit.Framework;
using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using Decantra.Domain.Model;
using System.Linq;

namespace Decantra.Tests.EditMode
{
    public class ReproductionSpec
    {
        [Test]
        public void VerifyBottleCountsAreWithinGridLimits()
        {
            // Grid is 3x3 = 9.
            for (int i = 1; i <= 100; i++)
            {
                var profile = LevelDifficultyEngine.GetProfile(i);
                Assert.LessOrEqual(profile.BottleCount, 9, $"Level {i} has too many bottles: {profile.BottleCount}");
            }
        }

        [Test]
        public void VerifyNoSinkModeDisallowsSinkTargets()
        {
            // With allowSinkMoves=false the solver must avoid sink targets.

            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);
            int seed = 12345;

            // Find a level with sinks
            int levelWithSinks = -1;
            for (int i = 1; i <= 50; i++)
            {
                var profile = LevelDifficultyEngine.GetProfile(i);
                int sinkCount = LevelDifficultyEngine.ResolveSinkCount(i);
                if (sinkCount > 0 && profile.EmptyBottleCount > 0)
                {
                    levelWithSinks = i;
                    break;
                }
            }

            Assert.AreNotEqual(-1, levelWithSinks, "Could not find a level configuration with sinks");
            TestContext.Out.WriteLine($"Testing Level {levelWithSinks} for Sink leakage...");

            var profileWithSinks = LevelDifficultyEngine.GetProfile(levelWithSinks);
            var state = generator.Generate(seed, profileWithSinks);

            // Re-solve the generated state to inspect moves
            var solveResult = solver.SolveWithPath(state, 100000, 2000, allowSinkMoves: false);

            Assert.AreNotEqual(SolverStatus.Unsolvable, solveResult.Status);
            Assert.IsNotEmpty(solveResult.Path);

            var bottles = state.Bottles;
            var sinkIndices = new List<int>();
            for (int i = 0; i < bottles.Count; i++)
            {
                if (bottles[i].IsSink) sinkIndices.Add(i);
            }

            Assert.IsNotEmpty(sinkIndices, "Generated level should have sinks for this test");

            foreach (var move in solveResult.Path)
            {
                if (sinkIndices.Contains(move.Source))
                {
                    Assert.Fail($"Solution moves FROM a sink! Move: {move.Source}->{move.Target}. Sink Indices: {string.Join(",", sinkIndices)}");
                }
                Assert.IsFalse(sinkIndices.Contains(move.Target),
                    $"No-sink solve should not move TO a sink. Move: {move.Source}->{move.Target}. Sink Indices: {string.Join(",", sinkIndices)}");
            }
        }
    }
}
