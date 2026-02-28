/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;

namespace Decantra.Domain.Persistence
{
    [Serializable]
    public sealed class ProgressData
    {
        public int HighestUnlockedLevel;
        public int CurrentLevel;
        public int CurrentSeed;
        public int CurrentScore;
        public int HighScore;
        public int StarBalance;
        public List<int> CompletedLevels;
        public List<string> UnlockedThemes;
        public int SessionCurrentPerfectStreak;
        public int SessionBestPerfectStreak;
        public int LifetimeBestPerfectStreak;
        public int LifetimeOptimalCount;
        public List<LevelPerformanceRecord> BestPerformances;
    }
}
