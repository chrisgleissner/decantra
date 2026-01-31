/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class Level10RegressionTests
    {
        [Test]
        public void Level10_HasLegalOpeningMove_And_IsSolvable()
        {
            var solver = new BfsSolver();
            var generator = new LevelGenerator(solver);

            int seed = 0;
            for (int level = 1; level <= 10; level++)
            {
                seed = NextSeed(level, seed);
            }

            var profile = LevelDifficultyEngine.GetProfile(10);
            var state = generator.Generate(seed, profile);

            Assert.IsTrue(LevelStartValidator.HasAnyLegalMove(state), "Expected at least one legal move at start.");

            var result = solver.SolveOptimal(state);
            Assert.GreaterOrEqual(result.OptimalMoves, 0, "Level 10 should be solvable.");
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
