using System;
using Decantra.Domain.Persistence;

namespace Decantra.Domain.Rules
{
    public static class ProgressionResumePolicy
    {
        public static int ResolveResumeLevel(ProgressData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int current = data.CurrentLevel;
            if (current > 0)
            {
                return Math.Max(1, current);
            }

            int highest = data.HighestUnlockedLevel;
            return Math.Max(1, highest);
        }
    }
}
