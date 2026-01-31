using System;
using Decantra.Domain.Model;

namespace Decantra.Domain.Rules
{
    public static class LevelStartValidator
    {
        public static bool TryValidate(LevelState state, out string error)
        {
            error = null;
            if (state == null)
            {
                error = "State is null.";
                return false;
            }

            if (state.IsWin())
            {
                error = "State is already solved.";
                return false;
            }

            if (!HasAnyLegalMove(state))
            {
                error = "State has no legal opening moves.";
                return false;
            }

            return true;
        }

        public static bool HasAnyLegalMove(LevelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    if (MoveRules.GetPourAmount(state, i, j) > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
