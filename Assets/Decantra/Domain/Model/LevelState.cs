/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Decantra.Domain.Model
{
    public sealed class LevelState
    {
        public LevelState(IReadOnlyList<Bottle> bottles, int movesUsed, int movesAllowed, int optimalMoves, int levelIndex, int seed, int scrambleMoves = 0, int backgroundPaletteIndex = -1)
        {
            if (bottles == null) throw new ArgumentNullException(nameof(bottles));
            if (bottles.Count == 0) throw new ArgumentOutOfRangeException(nameof(bottles));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            if (scrambleMoves < 0) throw new ArgumentOutOfRangeException(nameof(scrambleMoves));

            Bottles = bottles.Select(b => b.Clone()).ToList();
            MovesUsed = movesUsed;
            MovesAllowed = movesAllowed;
            OptimalMoves = optimalMoves;
            LevelIndex = levelIndex;
            Seed = seed;
            ScrambleMoves = scrambleMoves;
            BackgroundPaletteIndex = backgroundPaletteIndex;
        }

        public IReadOnlyList<Bottle> Bottles { get; }
        public int MovesUsed { get; private set; }
        public int MovesAllowed { get; }
        public int OptimalMoves { get; }
        public int LevelIndex { get; }
        public int Seed { get; }
        public int ScrambleMoves { get; }
        public int BackgroundPaletteIndex { get; }

        public bool IsWin()
        {
            // 1. Every non-empty bottle contains liquid of exactly one color
            for (int i = 0; i < Bottles.Count; i++)
            {
                var bottle = Bottles[i];
                if (bottle.IsEmpty) continue;
                if (!bottle.IsMonochrome) return false;
            }

            // 2. No legal move exists that would reduce the number of bottles used by any color
            for (int i = 0; i < Bottles.Count; i++)
            {
                var source = Bottles[i];
                if (source.IsEmpty) continue;
                if (source.IsSink) continue;

                var color = source.TopColor;

                for (int j = 0; j < Bottles.Count; j++)
                {
                    if (i == j) continue;
                    var target = Bottles[j];

                    // Consolidating into an empty bottle does not reduce the bottle count
                    if (target.IsEmpty) continue;

                    // Target must match color to allow pour
                    if (target.TopColor != color) continue;

                    // If we can pour all of source into target, we can reduce the bottle count
                    if (source.MaxPourAmountInto(target) >= source.Count)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool IsFail()
        {
            if (MovesAllowed <= 0) return MovesUsed > 0 && !IsWin();
            return MovesUsed >= MovesAllowed && !IsWin();
        }

        public bool TryApplyMove(int sourceIndex, int targetIndex, out int poured)
        {
            poured = 0;
            int amount = Decantra.Domain.Rules.MoveRules.GetPourAmount(this, sourceIndex, targetIndex);
            if (amount <= 0) return false;
            var source = Bottles[sourceIndex];
            var target = Bottles[targetIndex];
            source.PourInto(target, amount);
            MovesUsed++;
            poured = amount;
            return true;
        }
    }
}
