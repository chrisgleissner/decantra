/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using Decantra.Domain.Model;

namespace Decantra.Domain.Rules
{
    public static class InteractionRules
    {
        public static bool CanUseAsSource(Bottle bottle)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            return !bottle.IsSink;
        }

        public static bool CanDrag(Bottle bottle)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            return !bottle.IsSink;
        }
    }
}
