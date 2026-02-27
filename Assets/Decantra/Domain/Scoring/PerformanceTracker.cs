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
        public readonly struct CompletionFeedback
        {
            public CompletionFeedback(string message, bool isPersonalBest)
            {
                Message = message;
                IsPersonalBest = isPersonalBest;
            }

            public string Message { get; }
            public bool IsPersonalBest { get; }
        }

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

        public static CompletionFeedback RecordCompletion(
            ProgressData data,
            int levelIndex,
            int stars,
            int moves,
            int optimalMoves,
            float efficiency,
            PerformanceGrade grade)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            if (data.BestPerformances == null)
            {
                data.BestPerformances = new List<LevelPerformanceRecord>();
            }

            int clampedStars = Math.Max(0, Math.Min(5, stars));
            int safeMoves = Math.Max(0, moves);
            int deviation = Math.Max(0, safeMoves - Math.Max(0, optimalMoves));

            var existing = GetBest(data, levelIndex);
            bool hadRecord = existing != null;
            int previousBestStars = hadRecord ? existing.BestStars : 0;
            int previousBestMoves = hadRecord ? existing.BestMoves : 0;
            bool improvedStars = clampedStars > previousBestStars;
            bool improvedMoves = safeMoves > 0 && (previousBestMoves <= 0 || safeMoves < previousBestMoves);
            bool isPersonalBest = improvedStars || improvedMoves || !hadRecord;

            if (!hadRecord)
            {
                existing = new LevelPerformanceRecord { LevelIndex = levelIndex };
                data.BestPerformances.Add(existing);
            }

            existing.TimesCompleted = Math.Max(0, existing.TimesCompleted) + 1;

            if (improvedStars || existing.BestStars <= 0)
            {
                existing.BestStars = clampedStars;
            }

            if (improvedStars)
            {
                existing.BestMoves = safeMoves;
                existing.BestDeviation = deviation;
            }
            else if (improvedMoves)
            {
                existing.BestMoves = safeMoves;
                existing.BestDeviation = deviation;
            }

            if (efficiency > existing.BestEfficiency)
            {
                existing.BestEfficiency = efficiency;
            }

            if (GradeRank(grade) > GradeRank(existing.BestGrade))
            {
                existing.BestGrade = grade;
            }

            if (isPersonalBest)
            {
                return new CompletionFeedback("Personal best", true);
            }

            if (clampedStars == 5)
            {
                return new CompletionFeedback(previousBestStars == 5 ? "Optimal again" : "Optimal", false);
            }

            return new CompletionFeedback($"{deviation} moves to optimal", false);
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
                BestStars = record.BestStars,
                BestMoves = record.BestMoves,
                BestDeviation = record.BestDeviation,
                TimesCompleted = record.TimesCompleted,
                BestEfficiency = record.BestEfficiency,
                BestGrade = record.BestGrade
            };
        }
    }
}
