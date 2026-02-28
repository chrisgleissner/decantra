/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Persistence;

namespace Decantra.Domain.Scoring
{
    public static class PerfectStreakTracker
    {
        public static bool IsPerfectCompletion(int stars, bool autoSolveUsed, bool blackBottleConverted)
        {
            return stars == 5 && !autoSolveUsed && !blackBottleConverted;
        }

        public static void RecordCompletion(ProgressData progress, bool isPerfect, out bool newLifetimeRecord, out int milestone)
        {
            newLifetimeRecord = false;
            milestone = 0;
            if (progress == null) return;

            if (!isPerfect)
            {
                progress.SessionCurrentPerfectStreak = 0;
                return;
            }

            progress.SessionCurrentPerfectStreak = progress.SessionCurrentPerfectStreak + 1;
            progress.LifetimeOptimalCount = progress.LifetimeOptimalCount + 1;

            if (progress.SessionCurrentPerfectStreak > progress.SessionBestPerfectStreak)
            {
                progress.SessionBestPerfectStreak = progress.SessionCurrentPerfectStreak;
            }

            if (progress.SessionCurrentPerfectStreak > progress.LifetimeBestPerfectStreak)
            {
                progress.LifetimeBestPerfectStreak = progress.SessionCurrentPerfectStreak;
                newLifetimeRecord = true;
            }

            switch (progress.SessionCurrentPerfectStreak)
            {
                case 3:
                case 5:
                case 7:
                case 10:
                case 15:
                    milestone = progress.SessionCurrentPerfectStreak;
                    break;
            }
        }
    }
}
