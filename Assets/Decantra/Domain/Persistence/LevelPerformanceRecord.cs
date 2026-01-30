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
