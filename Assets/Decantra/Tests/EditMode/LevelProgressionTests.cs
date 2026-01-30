using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class LevelProgressionTests
    {
        [Test]
        public void Levels_1_To_10_Are_Solvable()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            int seed = 0;
            for (int level = 1; level <= 10; level++)
            {
                seed = NextSeed(level, seed);
                var profile = LevelDifficultyEngine.GetProfile(level);
                var state = generator.Generate(seed, profile);
                Assert.GreaterOrEqual(state.OptimalMoves, 0, $"Unsolvable level {level}");
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
