namespace Decantra.Domain.Persistence
{
    public sealed class ProgressData
    {
        public int HighestUnlockedLevel { get; set; }
        public int CurrentLevel { get; set; }
        public int CurrentSeed { get; set; }
        public int CurrentScore { get; set; }
        public int HighScore { get; set; }
    }
}
