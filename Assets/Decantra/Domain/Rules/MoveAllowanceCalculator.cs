/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    public static class MoveAllowanceCalculator
    {
        public static int ComputeMovesAllowed(DifficultyProfile profile, int optimalMoves)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (optimalMoves < 1) return 1;

            float slack = ComputeSlackFactor(profile.LevelIndex);
            int allowed = (int)Math.Ceiling(optimalMoves * slack);
            return Math.Max(1, allowed);
        }

        public static float ComputeSlackFactor(int levelIndex)
        {
            if (levelIndex <= 1) return 2.0f;
            if (levelIndex >= 500) return 1.0f;

            float t = (levelIndex - 1) / 499f;
            float slack = 2.0f - t;
            if (slack < 1.0f) slack = 1.0f;
            if (slack > 2.0f) slack = 2.0f;
            return slack;
        }
    }
}
