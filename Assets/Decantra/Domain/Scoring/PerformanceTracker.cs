/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Persistence;

namespace Decantra.Domain.Scoring
{
    public static class PerformanceTracker
    {
        public static LevelPerformanceRecord GetBest(ProgressData data, int levelIndex)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            if (data.BestPerformances == null) return null;
            for (int i = 0; i < data.BestPerformances.Count; i++)
            {
                var record = data.BestPerformances[i];
                if (record != null && record.LevelIndex == levelIndex)
                {
                    return record;
                }
            }
            return null;
        }

        public static void UpdateBest(ProgressData data, LevelPerformanceRecord incoming)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            if (incoming.LevelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(incoming.LevelIndex));

            if (data.BestPerformances == null)
            {
                data.BestPerformances = new List<LevelPerformanceRecord>();
            }

            var existing = GetBest(data, incoming.LevelIndex);
            if (existing == null)
            {
                data.BestPerformances.Add(Clone(incoming));
                return;
            }

            if (incoming.BestMoves > 0)
            {
                if (existing.BestMoves <= 0 || incoming.BestMoves < existing.BestMoves)
                {
                    existing.BestMoves = incoming.BestMoves;
                }
            }

            if (incoming.BestEfficiency > existing.BestEfficiency)
            {
                existing.BestEfficiency = incoming.BestEfficiency;
            }

            if (GradeRank(incoming.BestGrade) > GradeRank(existing.BestGrade))
            {
                existing.BestGrade = incoming.BestGrade;
            }
        }

        private static int GradeRank(PerformanceGrade grade)
        {
            return (int)grade;
        }

        private static LevelPerformanceRecord Clone(LevelPerformanceRecord record)
        {
            return new LevelPerformanceRecord
            {
                LevelIndex = record.LevelIndex,
                BestMoves = record.BestMoves,
                BestEfficiency = record.BestEfficiency,
                BestGrade = record.BestGrade
            };
        }
    }
}
