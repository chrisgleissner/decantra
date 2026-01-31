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
            for (int i = 0; i < Bottles.Count; i++)
            {
                var bottle = Bottles[i];
                if (bottle.IsEmpty) continue;
                if (!bottle.IsFull) return false;
                if (!bottle.IsSolvedBottle()) return false;
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
