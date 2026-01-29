using System.Collections.Generic;

namespace Decantra.Domain.Solver
{
    public sealed class SolverResult
    {
        public SolverResult(int optimalMoves, IReadOnlyList<Move> path)
        {
            OptimalMoves = optimalMoves;
            Path = path;
        }

        public int OptimalMoves { get; }
        public IReadOnlyList<Move> Path { get; }
    }
}
