/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
