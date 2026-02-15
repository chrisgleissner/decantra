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
    /// <summary>
    /// Tests the terminal-state invariant: in any solved level, every non-empty bottle
    /// must be completely full AND monochrome. Partially-filled bottles are never accepted.
    /// </summary>
    public sealed class TerminalStateInvariantTests
    {
        private LevelState CreateLevel(params Bottle[] bottles)
        {
            return new LevelState(bottles, 0, 10, 0, 0, 0);
        }

        // ---- Unit tests for tightened IsWin() ----

        [Test]
        public void IsWin_PartialMonochrome_ReturnsFalse()
        {
            // 2 of 4 Red — monochrome but not full
            var state = CreateLevel(new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null }));
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_FullMonochrome_ReturnsTrue()
        {
            // 4 of 4 Red — full and monochrome
            var state = CreateLevel(
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(4));
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_AllEmpty_ReturnsTrue()
        {
            var state = CreateLevel(new Bottle(4), new Bottle(4));
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_FullMixed_ReturnsFalse()
        {
            var state = CreateLevel(
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }));
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_MultipleFullColors_ReturnsTrue()
        {
            var state = CreateLevel(
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Blue }),
                new Bottle(3));
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_FullAndPartialSameColor_ReturnsFalse()
        {
            // One full Red, one partial Red — partial fails
            var state = CreateLevel(
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null }));
            // Partial is not full → false. Also the partial could merge (pour 1 into full? No, full is full).
            // But IsFull check catches it first.
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void Regression_OldCodeAcceptedPartialMonochrome()
        {
            // Old IsWin accepted this as solved (monochrome + irreducible).
            // New IsWin rejects it (not full).
            var state = CreateLevel(
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null }));
            Assert.IsFalse(state.IsWin(), "Partial monochrome must not be accepted as solved");
        }

        // ---- Generator invariant: total units per color = one bottle capacity ----

        [Test]
        public void GeneratedLevel_TotalUnitsPerColor_EqualsOneBottleCapacity()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            int[] levels = { 1, 5, 10, 20, 30, 50 };
            int seed = 12345;

            foreach (int level in levels)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                seed = NextSeed(level, seed);
                var state = generator.Generate(seed, profile);

                var colorVolumes = new Dictionary<ColorId, int>();
                for (int i = 0; i < state.Bottles.Count; i++)
                {
                    for (int s = 0; s < state.Bottles[i].Slots.Count; s++)
                    {
                        var c = state.Bottles[i].Slots[s];
                        if (!c.HasValue) continue;
                        if (!colorVolumes.ContainsKey(c.Value)) colorVolumes[c.Value] = 0;
                        colorVolumes[c.Value]++;
                    }
                }

                foreach (var kvp in colorVolumes)
                {
                    bool matchesAnyCapacity = false;
                    for (int i = 0; i < state.Bottles.Count; i++)
                    {
                        if (state.Bottles[i].Capacity == kvp.Value)
                        {
                            matchesAnyCapacity = true;
                            break;
                        }
                    }
                    Assert.IsTrue(matchesAnyCapacity,
                        $"Level {level} seed {seed}: color {kvp.Key} has {kvp.Value} units but no bottle has matching capacity");
                }
            }
        }

        // ---- Integration: solve levels, verify terminal state ----

        [Test]
        public void Integration_25Levels_TerminalState_AllFullOrEmpty()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            for (int i = 0; i < 25; i++)
            {
                int level = 1 + (i % 25);
                int seed = NextSeed(level, 42 + i * 7919);
                var profile = LevelDifficultyEngine.GetProfile(level);
                var state = generator.Generate(seed, profile);

                var result = solver.SolveWithPath(state);
                Assert.GreaterOrEqual(result.OptimalMoves, 0,
                    $"Unsolvable: level={level} seed={seed}");

                var replay = new LevelState(state.Bottles, 0, state.MovesAllowed,
                    state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves);
                foreach (var move in result.Path)
                {
                    bool applied = replay.TryApplyMove(move.Source, move.Target, out _);
                    Assert.IsTrue(applied, $"Move failed: level={level} seed={seed}");
                }

                Assert.IsTrue(replay.IsWin(), $"Not win: level={level} seed={seed}");
                AssertTerminalStateValid(replay, level, seed);
            }
        }

        // ---- Helpers ----

        private static void AssertTerminalStateValid(LevelState state, int level, int seed)
        {
            for (int b = 0; b < state.Bottles.Count; b++)
            {
                var bottle = state.Bottles[b];
                if (bottle.IsEmpty) continue;
                Assert.IsTrue(bottle.IsFull,
                    $"Headspace: level={level} seed={seed} bottle={b} count={bottle.Count} cap={bottle.Capacity}");
                Assert.IsTrue(bottle.IsMonochrome,
                    $"Mixed: level={level} seed={seed} bottle={b}");
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
