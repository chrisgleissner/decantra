using System;
using Decantra.Domain.Persistence;

namespace Decantra.Domain.Rules
{
    public static class ProgressionResumePolicy
    {
        public static int ResolveResumeLevel(ProgressData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int highest = data.HighestUnlockedLevel;
            int current = data.CurrentLevel;
            int resume = Math.Max(highest, current);
            return Math.Max(1, resume);
        }
    }
}
