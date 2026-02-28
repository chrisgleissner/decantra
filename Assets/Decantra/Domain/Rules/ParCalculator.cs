/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    public static class ParCalculator
    {
        public static int ComputePar(int optimalMoves, int movesAllowed)
        {
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));
            if (movesAllowed < optimalMoves)
            {
                throw new ArgumentOutOfRangeException(nameof(movesAllowed), "movesAllowed must be greater than or equal to optimalMoves.");
            }

            int slack = movesAllowed - optimalMoves;

            int buffer;
            if (slack <= 2)
                buffer = 2;
            else if (slack <= 5)
                buffer = 1;
            else
                buffer = 0;

            int par = optimalMoves + buffer;
            if (par > movesAllowed) par = movesAllowed;
            return par;
        }
    }
}
