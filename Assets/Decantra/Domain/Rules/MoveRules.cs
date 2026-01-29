using System;
using Decantra.Domain.Model;

namespace Decantra.Domain.Rules
{
    public static class MoveRules
    {
        public static bool IsValidMove(LevelState state, int sourceIndex, int targetIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (sourceIndex == targetIndex) return false;
            if (sourceIndex < 0 || sourceIndex >= state.Bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= state.Bottles.Count) return false;
            var source = state.Bottles[sourceIndex];
            var target = state.Bottles[targetIndex];
            return source.MaxPourAmountInto(target) > 0;
        }
    }
}
