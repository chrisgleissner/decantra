using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class MoveRulesInvariantTests
    {
        [Test]
        public void NonSink_PourAllowed_WhenSpaceAndColorMatches()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null })
            };
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);

            Assert.IsTrue(MoveRules.IsValidMove(state, 0, 1));
            int amount = MoveRules.GetPourAmount(state, 0, 1);
            Assert.AreEqual(2, amount);
        }

        [Test]
        public void SinkBottle_CannotBeSource_ForMoveRules()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Blue, null, null }, true),
                new Bottle(new ColorId?[] { null, null, null })
            };
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);

            Assert.IsFalse(MoveRules.IsValidMove(state, 0, 1));
            Assert.AreEqual(0, MoveRules.GetPourAmount(state, 0, 1));
        }

        [Test]
        public void PourAmount_IsDeterministic_ForSameState()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, null, null }),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = new LevelState(bottles, 0, 10, 3, 1, 123);

            int first = MoveRules.GetPourAmount(state, 0, 1);
            int second = MoveRules.GetPourAmount(state, 0, 1);

            Assert.AreEqual(first, second);
            Assert.Greater(first, 0);
        }
    }
}
