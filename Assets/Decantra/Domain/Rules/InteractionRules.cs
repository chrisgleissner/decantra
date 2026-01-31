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
