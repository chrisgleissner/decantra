using Decantra.Domain.Generation;
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
                int reverseMoves = ComputeReverseMoves(level);
                int padding = ComputeMovesPadding(level);

                var state = generator.Generate(seed, level, reverseMoves, padding);
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

        private static int ComputeReverseMoves(int level)
        {
            if (level <= 1) return 6;
            if (level <= 3) return 8 + level;
            if (level <= 8) return 10 + level * 2;
            return 20 + level * 2;
        }

        private static int ComputeMovesPadding(int level)
        {
            if (level <= 2) return 6;
            if (level <= 6) return 5;
            return 4;
        }
    }
}
