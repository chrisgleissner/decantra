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
    }
}
