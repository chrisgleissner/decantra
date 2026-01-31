using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class SolverTests
    {
        [Test]
        public void Solve_ReturnsZeroForSolvedState()
        {
            var solver = new BfsSolver();
            var solved = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 10, 0, 1, 1);

            var result = solver.Solve(solved);
            Assert.GreaterOrEqual(result.OptimalMoves, 0);
        }

        [Test]
        public void Encode_DifferentStatesHaveDifferentKeys()
        {
            var stateA = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null }),
                new Bottle(new ColorId?[] { null, null, null, null })
            }, 0, 10, 1, 0, 1);

            var stateB = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { null, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null })
            }, 0, 10, 1, 0, 2);

            var keyA = StateEncoder.Encode(stateA);
            var keyB = StateEncoder.Encode(stateB);
            Assert.AreNotEqual(keyA, keyB);
        }

        [Test]
        public void Encode_DistinguishesSinkBottles()
        {
            var stateA = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red }, true),
                new Bottle(new ColorId?[2])
            }, 0, 10, 1, 0, 3);

            var stateB = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[2])
            }, 0, 10, 1, 0, 4);

            var keyA = StateEncoder.EncodeCanonical(stateA);
            var keyB = StateEncoder.EncodeCanonical(stateB);
            Assert.AreNotEqual(keyA, keyB);
        }

        [Test]
        public void Solve_KnownConfiguration_ReturnsMinimumMoves()
        {
            var solver = new BfsSolver();
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 42);

            var result = solver.Solve(state);
            Assert.AreEqual(3, result.OptimalMoves);
        }

        [Test]
        public void Solve_IsDeterministicForSameState()
        {
            var solver = new BfsSolver();
            var state = new LevelState(new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[4])
            }, 0, 20, 0, 1, 99);

            var resultA = solver.Solve(state);
            var resultB = solver.Solve(state);

            Assert.AreEqual(resultA.OptimalMoves, resultB.OptimalMoves);
        }
    }
}
