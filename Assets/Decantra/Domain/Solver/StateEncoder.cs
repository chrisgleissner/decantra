/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
                sb.Append(bottle.IsSink ? 'S' : 'N');
                for (int j = 0; j < bottle.Slots.Count; j++)
                {
                    var color = bottle.Slots[j];
                    sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
                }
                sb.Append('|');
            }
            return sb.ToString();
        }

        public static string EncodeCanonical(LevelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var signatures = new string[state.Bottles.Count];
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var bottle = state.Bottles[i];
                var sb = new StringBuilder(bottle.Slots.Count + 2);
                sb.Append(bottle.IsSink ? 'S' : 'N');
                for (int j = 0; j < bottle.Slots.Count; j++)
                {
                    var color = bottle.Slots[j];
                    sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
                }
                signatures[i] = sb.ToString();
            }

            Array.Sort(signatures, StringComparer.Ordinal);
            var combined = new StringBuilder(signatures.Length * 5);
            for (int i = 0; i < signatures.Length; i++)
            {
                combined.Append(signatures[i]);
                combined.Append('|');
            }
            return combined.ToString();
        }
    }
}
