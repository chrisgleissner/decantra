/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class GenerationSolvabilityTests
    {
        [Test]
        public void GeneratedLevels_AreSolvableAndOptimal()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            int[] levels = { 1, 5, 10, 20, 30, 50 };
            int seed = 12345;
            const int attemptsPerLevel = 4;
            int total = 0;

            foreach (int level in levels)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                for (int i = 0; i < attemptsPerLevel; i++)
                {
                    seed = NextSeed(level, seed + 31);
                    var state = generator.Generate(seed, profile);
                    AssertLevelIntegrity(state);
                    Assert.Greater(state.ScrambleMoves, 0, "Expected stored scramble move count.");

                    var result = solver.SolveWithPath(state);
                    Assert.GreaterOrEqual(result.OptimalMoves, 0, $"Solver failed at level {level} seed {seed}");
                    Assert.AreEqual(state.OptimalMoves, result.OptimalMoves, $"Stored optimal mismatch at level {level} seed {seed}");
                    Assert.AreEqual(result.Path.Count, result.OptimalMoves, "Path length should equal optimal moves");

                    var replay = new LevelState(state.Bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed);
                    for (int m = 0; m < result.Path.Count; m++)
                    {
                        var move = result.Path[m];
                        int poured;
                        bool applied = replay.TryApplyMove(move.Source, move.Target, out poured);
                        Assert.IsTrue(applied, $"Failed to apply solver move {m} at level {level} seed {seed}");
                    }

                    Assert.IsTrue(replay.IsWin(), $"Solver path did not finish in win at level {level} seed {seed}");
                    Assert.GreaterOrEqual(state.MovesAllowed, state.OptimalMoves);
                    total++;
                }
            }

            Assert.GreaterOrEqual(total, attemptsPerLevel * levels.Length);
        }

        private static void AssertLevelIntegrity(LevelState state)
        {
            Assert.IsNotNull(state);
            Assert.IsTrue(LevelIntegrity.TryValidate(state, out string error), error);
        }

        private static int NextSeed(int level, int previous)
        {
            unchecked
            {
                int baseSeed = previous != 0 ? previous : 12345;
                int mix = baseSeed * 1103515245 + 12345 + level * 97;
                return System.Math.Abs(mix == 0 ? level * 7919 : mix);
            }
        }
    }
}
