using System.Collections.Generic;
using Decantra.Domain.Model;
using Decantra.Domain.Solver;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class SolverSinkModeTests
    {
        [Test]
        public void SolveWithPath_AllowSinkMovesFlag_ControlsSinkTargeting()
        {
            var state = new LevelState(
                new List<Bottle>
                {
                    new Bottle(new ColorId?[] { ColorId.Red, ColorId.Blue }, isSink: false),
                    new Bottle(new ColorId?[] { ColorId.Blue, null }, isSink: true),
                    new Bottle(new ColorId?[] { ColorId.Red, null }, isSink: false)
                },
                0,
                10,
                2,
                50,
                12345);

            var solver = new BfsSolver();

            var noSinkResult = solver.SolveWithPath(state, 10000, 1000, allowSinkMoves: false);
            Assert.Less(noSinkResult.OptimalMoves, 0, "No-sink mode should not solve sink-required state.");

            var sinkEnabledResult = solver.SolveWithPath(state, 10000, 1000, allowSinkMoves: true);
            Assert.AreEqual(SolverStatus.Solved, sinkEnabledResult.Status);
            Assert.GreaterOrEqual(sinkEnabledResult.Path.Count, 1);
            Assert.AreEqual(1, sinkEnabledResult.Path[0].Target, "Expected black bottle to be used as target when enabled.");
        }
    }
}
