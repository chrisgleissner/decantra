/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using Decantra.Domain.Scoring;

namespace Decantra.Domain.Persistence
{
    [Serializable]
    public sealed class LevelPerformanceRecord
    {
        public int LevelIndex;
        public int BestMoves;
        public float BestEfficiency;
        public PerformanceGrade BestGrade;
    }
}
