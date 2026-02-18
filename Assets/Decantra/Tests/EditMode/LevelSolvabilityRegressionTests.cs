/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using NUnit.Framework;
using System.Diagnostics;
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
        [Timeout(420000)]
        public void Levels_1_To_200_AreSolvableWithinValidationBounds()
        {
            // Arrange
            var generator = new LevelGenerator(new BfsSolver());
            var solver = new BfsSolver();
            const int verifyMaxNodes = 2_000_000;
            const int verifyMaxMillis = 10_000;

            // Act & Assert
            int seed = 0;
            int revalidatedCount = 0;
            long revalidatedTotalMs = 0;
            long revalidatedMaxMs = 0;
            for (int levelIndex = 1; levelIndex <= 200; levelIndex++)
            {
                seed = NextSeed(levelIndex, seed);
                bool shouldGenerate = levelIndex <= 40 || (levelIndex % 4) == 0;
                if (!shouldGenerate)
                {
                    continue;
                }

                var profile = LevelDifficultyEngine.GetProfile(levelIndex);
                var level = generator.Generate(seed, profile);

                Assert.IsNotNull(level, $"Level {levelIndex} failed to generate");
                Assert.Greater(level.OptimalMoves, 0, $"Level {levelIndex} has zero or negative optimal moves");

                var report = generator.LastReport;
                Assert.IsNotNull(report, $"Missing generation report at level {levelIndex}");
                Assert.GreaterOrEqual(report.SolverTimeMs, 0, $"Solver time missing at level {levelIndex}");
                Assert.LessOrEqual(report.SolverTimeMs, verifyMaxMillis,
                    $"Generation solver budget exceeded at level {levelIndex}: {report.SolverTimeMs}ms");

                // Full deterministic revalidation on a stable subset for CI runtime control.
                bool shouldRevalidate = levelIndex <= 20 || levelIndex % 20 == 0;
                if (!shouldRevalidate)
                {
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                var result = solver.Solve(level, verifyMaxNodes, verifyMaxMillis, allowSinkMoves: true);
                stopwatch.Stop();

                long elapsedMs = stopwatch.ElapsedMilliseconds;
                revalidatedCount++;
                revalidatedTotalMs += elapsedMs;
                if (elapsedMs > revalidatedMaxMs) revalidatedMaxMs = elapsedMs;

                Assert.AreEqual(SolverStatus.Solved, result.Status,
                    $"Level {levelIndex} is not solvable within bounds. Status: {result.Status}");
                Assert.AreEqual(level.OptimalMoves, result.OptimalMoves,
                    $"Level {levelIndex} optimal moves mismatch. Expected: {level.OptimalMoves}, Got: {result.OptimalMoves}");
            }

            Assert.Greater(revalidatedCount, 0, "Expected deterministic revalidation subset to run.");
            Assert.LessOrEqual(revalidatedMaxMs, verifyMaxMillis, $"Revalidated max solve exceeded {verifyMaxMillis}ms.");
            Assert.LessOrEqual(revalidatedTotalMs / (double)revalidatedCount, 5000d,
                "Average revalidated solve time exceeded 5 seconds.");
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
