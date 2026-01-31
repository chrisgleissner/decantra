using System;
using System.Collections.Generic;
using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class GeneratorTests
    {
        [Test]
        public void Generate_IsDeterministicBySeed()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            var profile = LevelDifficultyEngine.GetProfile(1);
            var levelA = generator.Generate(123, profile);
            var levelB = generator.Generate(123, profile);

            var keyA = StateEncoder.Encode(levelA);
            var keyB = StateEncoder.Encode(levelB);

            Assert.AreEqual(keyA, keyB);
            Assert.AreEqual(levelA.OptimalMoves, levelB.OptimalMoves);
            Assert.AreEqual(levelA.MovesAllowed, levelB.MovesAllowed);
        }

        [Test]
        public void Generate_DoesNotStartWithCappedBottles()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            for (int level = 1; level <= 6; level++)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                for (int seed = 10; seed < 40; seed += 3)
                {
                    var state = generator.Generate(seed, profile);
                    foreach (var bottle in state.Bottles)
                    {
                        Assert.IsFalse(bottle.IsSolvedBottle(), "Capped bottle found at start.");
                    }
                }
            }
        }

        [Test]
        public void Generate_ProvidesOptimalMovesAndAllowedMoves()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            int seed = 0;
            var levels = new[] { 1, 5, 10, 25, 50 };
            foreach (int level in levels)
            {
                seed = NextSeed(level, seed);
                var profile = LevelDifficultyEngine.GetProfile(level);
                var state = generator.Generate(seed, profile);
                Assert.GreaterOrEqual(state.OptimalMoves, 0);
                Assert.GreaterOrEqual(state.MovesAllowed, state.OptimalMoves);
            }
        }

        [Test]
        public void Generate_UsesVariableCapacities_ByLevel12()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            var profile = LevelDifficultyEngine.GetProfile(12);
            var state = generator.Generate(321, profile);

            var capacities = new HashSet<int>();
            foreach (var bottle in state.Bottles)
            {
                capacities.Add(bottle.Capacity);
            }

            Assert.Greater(capacities.Count, 1, "Expected capacity variation by level 12.");
        }

        [Test]
        public void Generate_IntroducesLargeCapacity_ByLevel18()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            var profile = LevelDifficultyEngine.GetProfile(18);
            var state = generator.Generate(555, profile);

            bool hasLarge = false;
            foreach (var bottle in state.Bottles)
            {
                if (bottle.Capacity >= 5)
                {
                    hasLarge = true;
                    break;
                }
            }

            Assert.IsTrue(hasLarge, "Expected capacity 5 bottles by level 18.");
        }

        [Test]
        public void Generate_RespectsEmptyCountAndReducedSlack()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            var profile = LevelDifficultyEngine.GetProfile(22);
            var state = generator.Generate(777, profile);

            int empty = 0;
            int nearFull = 0;
            int nonEmpty = 0;
            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsEmpty)
                {
                    empty++;
                    continue;
                }
                nonEmpty++;
                if (bottle.FreeSpace <= 1)
                {
                    nearFull++;
                }

                Assert.LessOrEqual(bottle.Count, bottle.Capacity, "Bottle overfilled.");
            }

            Assert.LessOrEqual(empty, profile.EmptyBottleCount, "Empty bottle count exceeded profile target.");
            Assert.GreaterOrEqual(nearFull, Math.Max(1, nonEmpty / 2), "Expected many bottles to be nearly full.");
        }

        [Test]
        public void Generate_IncludesSinkBottle_ByLevel24()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            var profile = LevelDifficultyEngine.GetProfile(24);
            var state = generator.Generate(901, profile);

            bool hasSink = false;
            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsSink)
                {
                    hasSink = true;
                    break;
                }
            }

            Assert.IsTrue(hasSink, "Expected at least one sink bottle by level 24.");
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
