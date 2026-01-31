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
    public sealed class SolvabilityFuzzTests
    {
        [Test]
        public void GeneratedLevels_AreSolvable_AndMeetInvariants()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            const int sampleCount = 60;
            var levels = new List<int> { 1, 5, 10, 23, 30, 50, 100 };
            while (levels.Count < sampleCount)
            {
                levels.Add(1 + (levels.Count % 25));
            }

            int seed = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                int level = levels[i];
                seed = NextSeed(level, seed);
                var profile = LevelDifficultyEngine.GetProfile(level);
                var state = generator.Generate(seed, profile);

                Assert.IsTrue(LevelIntegrity.TryValidate(state, out string error), $"Integrity failed level {level} seed {seed}: {error}");

                foreach (var bottle in state.Bottles)
                {
                    if (!bottle.IsSink) continue;
                    Assert.IsTrue(bottle.IsSingleColorOrEmpty(), $"Sink bottle mixed at level {level} seed {seed}");
                }

                var result = solver.SolveWithPath(state);
                Assert.GreaterOrEqual(result.OptimalMoves, 0, $"Solver failed level {level} seed {seed}");

                var replay = new LevelState(state.Bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves, state.BackgroundPaletteIndex);
                foreach (var move in result.Path)
                {
                    bool applied = replay.TryApplyMove(move.Source, move.Target, out _);
                    Assert.IsTrue(applied, $"Failed to apply move at level {level} seed {seed}");
                }

                Assert.IsTrue(replay.IsWin(), $"Solution did not reach win at level {level} seed {seed}");
            }
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
