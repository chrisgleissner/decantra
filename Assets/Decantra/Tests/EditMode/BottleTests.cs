/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Model;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class BottleTests
    {
        [Test]
        public void EmptyBottle_HasNoTopColor()
        {
            var bottle = new Bottle(4);
            Assert.IsNull(bottle.TopColor);
            Assert.IsTrue(bottle.IsEmpty);
            Assert.IsFalse(bottle.IsFull);
        }

        [Test]
        public void MaxPourAmount_RespectsTargetFreeSpace()
        {
            var source = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, null });
            var target = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null });

            int amount = source.MaxPourAmountInto(target);
            Assert.AreEqual(1, amount);
        }

        [Test]
        public void FullSinkBottle_CannotPourOut()
        {
            var source = new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, ColorId.Blue }, true);
            var target = new Bottle(new ColorId?[] { null, null, null });

            Assert.IsTrue(source.IsFull);
            Assert.AreEqual(0, source.MaxPourAmountInto(target));
        }

        [Test]
        public void SinkBottle_AllowsPourWhenNotFull()
        {
            var source = new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Blue, null }, true);
            var target = new Bottle(new ColorId?[] { null, null, null });

            Assert.IsFalse(source.IsFull);
            Assert.Greater(source.MaxPourAmountInto(target), 0);
        }

        [Test]
        public void PourInto_AllowsMatchingTopOrEmpty()
        {
            var source = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, null, null });
            var target = new Bottle(new ColorId?[] { null, null, null, null });
            Assert.IsTrue(source.CanPourInto(target));

            target = new Bottle(new ColorId?[] { ColorId.Red, null, null, null });
            Assert.IsTrue(source.CanPourInto(target));

            target = new Bottle(new ColorId?[] { ColorId.Blue, null, null, null });
            Assert.IsFalse(source.CanPourInto(target));
        }

        [Test]
        public void PourInto_PoursMaxContiguous()
        {
            var source = new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Blue, ColorId.Blue });
            var target = new Bottle(new ColorId?[] { null, null, null, null });

            int amount = source.MaxPourAmountInto(target);
            Assert.AreEqual(2, amount);

            source.PourInto(target, amount);

            Assert.AreEqual(ColorId.Blue, target.TopColor);
            Assert.AreEqual(2, target.Count);
            Assert.AreEqual(2, source.Count);
            Assert.AreEqual(ColorId.Red, source.TopColor);
        }

        [Test]
        public void IsSolvedBottle_TrueOnlyWhenFullSameColor()
        {
            var bottle = new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, ColorId.Green, ColorId.Green });
            Assert.IsTrue(bottle.IsSolvedBottle());

            var notFull = new Bottle(new ColorId?[] { ColorId.Green, ColorId.Green, null, null });
            Assert.IsFalse(notFull.IsSolvedBottle());

            var mixed = new Bottle(new ColorId?[] { ColorId.Green, ColorId.Blue, ColorId.Green, ColorId.Green });
            Assert.IsFalse(mixed.IsSolvedBottle());
        }

        [Test]
        public void PourInto_ExactFitAcrossCapacities()
        {
            var source = new Bottle(new ColorId?[]
            {
                ColorId.Red, ColorId.Red, ColorId.Red, null, null, null, null, null
            });
            var target = new Bottle(new ColorId?[]
            {
                ColorId.Red, ColorId.Red, ColorId.Red, null, null, null
            });

            int amount = source.MaxPourAmountInto(target);
            Assert.AreEqual(3, amount);

            source.PourInto(target, amount);

            Assert.AreEqual(0, source.Count);
            Assert.AreEqual(6, target.Count);
            Assert.IsTrue(target.IsFull);
        }

        [Test]
        public void PourInto_RespectsFreeSpaceAcrossCapacities()
        {
            var source = new Bottle(new ColorId?[]
            {
                ColorId.Blue, ColorId.Blue, ColorId.Blue, ColorId.Blue, ColorId.Blue,
                null, null, null, null, null
            });
            var target = new Bottle(new ColorId?[]
            {
                ColorId.Blue, ColorId.Blue, null, null
            });

            int amount = source.MaxPourAmountInto(target);
            Assert.AreEqual(2, amount);

            source.PourInto(target, amount);

            Assert.AreEqual(3, source.Count);
            Assert.AreEqual(4, target.Count);
            Assert.IsTrue(target.IsFull);
        }
    }
}
