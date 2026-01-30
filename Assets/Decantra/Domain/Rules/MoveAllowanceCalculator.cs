using System;

namespace Decantra.Domain.Rules
{
    public static class MoveAllowanceCalculator
    {
        public static int ComputeMovesAllowed(DifficultyProfile profile, int optimalMoves)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (optimalMoves < 1) return 1;

            int surplus = ComputeSurplus(profile);
            return Math.Max(1, optimalMoves + surplus);
        }

        private static int ComputeSurplus(DifficultyProfile profile)
        {
            int complexity = profile.ColorCount * 3 + profile.BottleCount * 2 + profile.EmptyBottleCount;
            int basePadding = (int)Math.Ceiling(complexity / 6f);
            int bandTightening = ((int)profile.Band) * 2;
            int padding = basePadding - bandTightening;
            if (padding < 1) padding = 1;
            if (padding > 8) padding = 8;
            return padding;
        }
    }
}
