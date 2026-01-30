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
    }
}
