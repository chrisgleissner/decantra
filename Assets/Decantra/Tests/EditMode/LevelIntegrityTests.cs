/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class LevelIntegrityTests
    {
        [Test]
        public void CapacityCompatibility_DetectsMissingTargetBottle()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red, null }),
                new Bottle(new ColorId?[3])
            };
            var state = new LevelState(bottles, 0, 5, 0, 1, 1);

            Assert.IsFalse(LevelIntegrity.TryValidate(state, out _));
        }

        [Test]
        public void SealedSinkWithMixedColors_IsInvalid()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Blue, ColorId.Red, ColorId.Blue }, true),
                new Bottle(new ColorId?[4])
            };
            var state = new LevelState(bottles, 0, 5, 0, 1, 1);

            Assert.IsFalse(LevelIntegrity.TryValidate(state, out _));
        }

        [Test]
        public void NonFullSinkWithMixedColors_IsInvalid()
        {
            var bottles = new[]
            {
                new Bottle(new ColorId?[] { ColorId.Blue, null, ColorId.Red, null }, true),
                new Bottle(new ColorId?[4])
            };
            var state = new LevelState(bottles, 0, 5, 0, 1, 1);

            Assert.IsFalse(LevelIntegrity.TryValidate(state, out _));
        }
    }
}
