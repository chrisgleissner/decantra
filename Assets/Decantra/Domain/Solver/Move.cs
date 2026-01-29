namespace Decantra.Domain.Solver
{
    public readonly struct Move
    {
        public Move(int source, int target, int amount)
        {
            Source = source;
            Target = target;
            Amount = amount;
        }

        public int Source { get; }
        public int Target { get; }
        public int Amount { get; }
    }
}
