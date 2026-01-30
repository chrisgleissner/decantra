using System;

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
    }
}
