/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;

namespace Decantra.Domain.Persistence
{
    public static class ProgressResetPolicy
    {
        public static ProgressData ResetForNewGame(ProgressData progress)
        {
            int highestUnlockedLevel = progress?.HighestUnlockedLevel ?? 1;
            return new ProgressData
            {
                HighestUnlockedLevel = highestUnlockedLevel > 0 ? highestUnlockedLevel : 1,
                CurrentLevel = 1,
                CurrentSeed = 0,
                CurrentScore = 0,
                StarBalance = 0,
                HighScore = progress?.HighScore ?? 0,
                CompletedLevels = new List<int>(),
                UnlockedThemes = new List<string>(),
                SessionCurrentPerfectStreak = 0,
                SessionBestPerfectStreak = 0,
                LifetimeBestPerfectStreak = progress?.LifetimeBestPerfectStreak ?? 0,
                LifetimeOptimalCount = progress?.LifetimeOptimalCount ?? 0,
                BestPerformances = progress?.BestPerformances ?? new List<LevelPerformanceRecord>()
            };
        }
    }
}
