/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
