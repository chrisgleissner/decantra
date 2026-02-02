using Decantra.Domain.Model;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class LevelCompletionTests
    {
        private LevelState CreateLevel(params Bottle[] bottles)
        {
            // LevelIndex=0, MovesAllowed=10, MoveScramble=0, Palette=0, Seed=0
            return new LevelState(bottles, 0, 10, 0, 0, 0);
        }

        [Test]
        public void IsWin_Level18Scenario_PartialMonochrome_ReturnsTrue()
        {
            // One bottle, 1 unit of Green. Capacity 4.
            // Old logic: Failed because !IsFull.
            // New logic: Should pass (Monochrome + no other bottles).
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_MixedBottle_ReturnsFalse()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Red, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_ConsolidationPossible_ReturnsFalse()
        {
            // Bottle 1: Green (1 unit)
            // Bottle 2: Green (1 unit)
            // Bottle 2 can accept 1 unit. Pour 1->2 allowed.
            // Reduces bottle count (Bottle 1 becomes empty).
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_ConsolidationNotPossible_ReturnsTrue()
        {
            // Bottle 1: Green (3 units)
            // Bottle 2: Green (3 units)
            // Cap 4.
            // Pour 1->2 possible (1 unit).
            // Result: Bottle 1 (2 units), Bottle 2 (4 units).
            // Bottle 1 NOT empty. Count not reduced.
            // So IsWin = true (Irreducible).
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, null }),
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_SinkRestrictions_ReturnsTrue()
        {
            // Bottle 1 (Sink): Green (1 unit)
            // Bottle 2 (Normal): Empty (Capacity 4)
            // Technically could pour Sink -> Normal.
            // But Sink cannot be source.
            // So no legal move exists.
            // IsWin = true.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null }, isSink: true),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_PourIntoSinkPossible_ReturnsFalse()
        {
            // Bottle 1 (Normal): Green (1 unit)
            // Bottle 2 (Sink): Green (1 unit)
            // Can pour Normal -> Sink.
            // Normal becomes empty.
            // Count reduced.
            // IsWin = false.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null }, isSink: true)
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }
    }
}
