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
        public void IsWin_Level18Scenario_PartialMonochrome_ReturnsFalse()
        {
            // One bottle, 1 unit of Green. Capacity 4.
            // Not full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
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
        public void IsWin_ConsolidationNotPossible_ReturnsFalse()
        {
            // Bottle 1: Green (3 units)
            // Bottle 2: Green (3 units)
            // Cap 4.
            // Neither bottle is full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, null }),
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_SinkRestrictions_ReturnsFalse()
        {
            // Bottle 1 (Sink): Green (1 unit)
            // Bottle 2 (Normal): Empty (Capacity 4)
            // Sink has 1/4 fill → not full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Green, null, null, null }, isSink: true),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
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

        // ============================================================
        // Additional tests for binding completion rules validation
        // ============================================================

        [Test]
        public void IsWin_MultipleColorsInBottle_ReturnsFalse()
        {
            // A bottle with multiple colors is never a valid win state
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Green, ColorId.Blue, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_TwoBottlesSameColorCanMerge_ReturnsFalse()
        {
            // Bottle 1: Red (2 units) in capacity 4
            // Bottle 2: Red (1 unit) in capacity 4
            // Can pour 1 -> 2 completely, reducing bottle count
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_TwoBottlesSameColorCannotMergeDueToCapacity_ReturnsFalse()
        {
            // Bottle 1: Red (3 units) cap 4 - not full
            // Bottle 2: Red (3 units) cap 4 - not full
            // Neither bottle is full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_CannotMergeBecauseSinkCannotBeSource_ReturnsFalse()
        {
            // Bottle 1 (Sink): Red (3 units) in capacity 4 - not full
            // Bottle 2 (Normal): Red (2 units) in capacity 4 - not full
            // Neither bottle is full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null }, isSink: true),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_ThreeBottlesSameColorPartiallyMergeable_ReturnsFalse()
        {
            // Bottle 1: Red (1 unit) cap 4
            // Bottle 2: Red (1 unit) cap 4
            // Bottle 3: Red (1 unit) cap 4
            // Bottle 1 can pour into Bottle 2, becoming empty. Count reduced.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_DifferentCapacitiesPreventsMerge_ReturnsTrue()
        {
            // Bottle 1: Red (3 units) cap 3 (FULL)
            // Bottle 2: Red (2 units) cap 2 (FULL)
            // Both full, no space to receive. No merge possible.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_AsymmetricCapacitiesAllowOnlyPartialPour_ReturnsFalse()
        {
            // Bottle 1: Red (5 units) cap 6 - not full
            // Bottle 2: Red (4 units) cap 5 - not full
            // Neither bottle is full → not win.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red, null }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }

        [Test]
        public void IsWin_EmptyBottlesIgnored_ReturnsTrue()
        {
            // Bottle 1: Red (4 units) cap 4 (FULL, monochrome)
            // Bottle 2: Empty cap 4
            // Pouring into empty doesn't reduce count of Red bottles.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { null, null, null, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_MultipleColorsSeparated_ReturnsTrue()
        {
            // Each color in its own bottle, all monochrome, no merge possible
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red }),
                new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, ColorId.Green }),
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Blue, ColorId.Blue })
            };
            var state = CreateLevel(bottles);
            Assert.IsTrue(state.IsWin());
        }

        [Test]
        public void IsWin_SameColorDifferentBottlesDifferentCapacities_CanMerge_ReturnsFalse()
        {
            // Bottle 1: Red (2 units) cap 10 - 8 free
            // Bottle 2: Red (2 units) cap 3 - 1 free
            // Bottle 2 can fully pour into Bottle 1. Count reduced.
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null, null, null, null, null, null, null }),
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null })
            };
            var state = CreateLevel(bottles);
            Assert.IsFalse(state.IsWin());
        }
    }
}
