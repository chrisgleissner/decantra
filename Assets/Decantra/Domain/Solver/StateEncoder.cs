using System;
using System.Text;
using Decantra.Domain.Model;

namespace Decantra.Domain.Solver
{
    public static class StateEncoder
    {
        public static string Encode(LevelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var sb = new StringBuilder(state.Bottles.Count * 5);
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var bottle = state.Bottles[i];
                for (int j = 0; j < bottle.Slots.Count; j++)
                {
                    var color = bottle.Slots[j];
                    sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
                }
                sb.Append('|');
            }
            return sb.ToString();
        }
    }
}
