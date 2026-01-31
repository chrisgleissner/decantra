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
    public sealed class InteractionRulesTests
    {
        [Test]
        public void SinkBottle_CannotBeDraggedOrUsedAsSource()
        {
            var sink = new Bottle(new ColorId?[] { ColorId.Red, null, null }, true);
            Assert.IsFalse(InteractionRules.CanDrag(sink));
            Assert.IsFalse(InteractionRules.CanUseAsSource(sink));
        }

        [Test]
        public void NormalBottle_CanBeDraggedAndUsedAsSource()
        {
            var bottle = new Bottle(new ColorId?[] { ColorId.Red, null, null }, false);
            Assert.IsTrue(InteractionRules.CanDrag(bottle));
            Assert.IsTrue(InteractionRules.CanUseAsSource(bottle));
        }
    }
}
