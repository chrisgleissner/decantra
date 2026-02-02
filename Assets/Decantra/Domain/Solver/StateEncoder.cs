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
    public readonly struct StateKey : IEquatable<StateKey>
    {
        public readonly int Count;
        public readonly ulong B0;
        public readonly ulong B1;
        public readonly ulong B2;
        public readonly ulong B3;
        public readonly ulong B4;
        public readonly ulong B5;
        public readonly ulong B6;
        public readonly ulong B7;
        public readonly ulong B8;

        public StateKey(int count, ulong b0, ulong b1, ulong b2, ulong b3, ulong b4, ulong b5, ulong b6, ulong b7, ulong b8)
        {
            Count = count;
            B0 = b0;
            B1 = b1;
            B2 = b2;
            B3 = b3;
            B4 = b4;
            B5 = b5;
            B6 = b6;
            B7 = b7;
            B8 = b8;
        }

        public bool Equals(StateKey other)
        {
            return Count == other.Count &&
                   B0 == other.B0 &&
                   B1 == other.B1 &&
                   B2 == other.B2 &&
                   B3 == other.B3 &&
                   B4 == other.B4 &&
                   B5 == other.B5 &&
                   B6 == other.B6 &&
                   B7 == other.B7 &&
                   B8 == other.B8;
        }

        public override bool Equals(object obj)
        {
            return obj is StateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Count);
            hash.Add(B0);
            hash.Add(B1);
            hash.Add(B2);
            hash.Add(B3);
            hash.Add(B4);
            hash.Add(B5);
            hash.Add(B6);
            hash.Add(B7);
            hash.Add(B8);
            return hash.ToHashCode();
        }
    }

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

        public static StateKey EncodeCanonicalKey(LevelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int count = state.Bottles.Count;
            var signatures = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                signatures[i] = EncodeBottle(state.Bottles[i]);
            }

            Array.Sort(signatures);

            ulong b0 = count > 0 ? signatures[0] : 0;
            ulong b1 = count > 1 ? signatures[1] : 0;
            ulong b2 = count > 2 ? signatures[2] : 0;
            ulong b3 = count > 3 ? signatures[3] : 0;
            ulong b4 = count > 4 ? signatures[4] : 0;
            ulong b5 = count > 5 ? signatures[5] : 0;
            ulong b6 = count > 6 ? signatures[6] : 0;
            ulong b7 = count > 7 ? signatures[7] : 0;
            ulong b8 = count > 8 ? signatures[8] : 0;

            return new StateKey(count, b0, b1, b2, b3, b4, b5, b6, b7, b8);
        }

        private static ulong EncodeBottle(Bottle bottle)
        {
            ulong sig = 0;
            sig |= (ulong)(bottle.IsSink ? 1 : 0);
            sig |= (ulong)(bottle.Capacity & 0xF) << 1;

            int shift = 5;
            var slots = bottle.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                int value = slots[i].HasValue ? ((int)slots[i].Value + 1) : 0;
                sig |= (ulong)(value & 0xF) << shift;
                shift += 4;
            }

            return sig;
        }
    }
}
