/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using NUnit.Framework;
using Decantra.Domain.Generation;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;

namespace Decantra.Tests.EditMode
{
    /// <summary>
    /// Regression tests to ensure that levels 1-200 remain solvable.
    /// This serves as a CI gate to catch level generation bugs early.
    /// </summary>
    [TestFixture]
    public sealed class LevelSolvabilityRegressionTests
    {
        [Test]
        [Timeout(1800000)]
        public void Levels_1_To_200_AreSolvable()
        {
            // Arrange
            var generator = new LevelGenerator(new BfsSolver());
            var solver = new BfsSolver();

            // Act & Assert
            int seed = 0;
            for (int levelIndex = 1; levelIndex <= 200; levelIndex++)
            {
                seed = NextSeed(levelIndex, seed);
                var profile = LevelDifficultyEngine.GetProfile(levelIndex);
                var level = generator.Generate(seed, profile);

                Assert.IsNotNull(level, $"Level {levelIndex} failed to generate");
                Assert.Greater(level.OptimalMoves, 0, $"Level {levelIndex} has zero or negative optimal moves");

                // Verify the level is actually solvable
                var result = solver.SolveOptimal(level);
                Assert.AreEqual(SolverStatus.Solved, result.Status,
                    $"Level {levelIndex} is not solvable. Status: {result.Status}");
                Assert.AreEqual(level.OptimalMoves, result.OptimalMoves,
                    $"Level {levelIndex} optimal moves mismatch. Expected: {level.OptimalMoves}, Got: {result.OptimalMoves}");
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
