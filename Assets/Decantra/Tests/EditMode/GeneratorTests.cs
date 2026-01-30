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
