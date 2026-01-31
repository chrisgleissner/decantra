using System;
using System.Collections.Generic;

namespace Decantra.Domain.Model
{
    public sealed class Bottle
    {
        private readonly ColorId?[] _slots;
        private readonly bool _isSink;

        public Bottle(int capacity, bool isSink = false)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new ColorId?[capacity];
            _isSink = isSink;
        }

        public Bottle(ColorId?[] slots, bool isSink = false)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (slots.Length == 0) throw new ArgumentOutOfRangeException(nameof(slots));
            _slots = (ColorId?[])slots.Clone();
            _isSink = isSink;
        }

        public int Capacity => _slots.Length;
        public bool IsSink => _isSink;
        public bool IsSealed => _isSink && IsFull;
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].HasValue) count++;
                }
                return count;
            }
        }

        public bool IsEmpty => Count == 0;
        public bool IsFull => Count == Capacity;

        public ColorId? TopColor
        {
            get
            {
                for (int i = _slots.Length - 1; i >= 0; i--)
                {
                    if (_slots[i].HasValue) return _slots[i];
                }
                return null;
            }
        }

        public int ContiguousTopCount
        {
            get
            {
                var top = TopColor;
                if (!top.HasValue) return 0;
                int count = 0;
                for (int i = _slots.Length - 1; i >= 0; i--)
                {
                    if (_slots[i] == top) count++;
                    else if (_slots[i].HasValue) break;
                }
                return count;
            }
        }

        public int FreeSpace => Capacity - Count;

        public IReadOnlyList<ColorId?> Slots => _slots;

        public Bottle Clone()
        {
            return new Bottle(_slots, _isSink);
        }

        public bool CanPourInto(Bottle target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (IsSealed) return false;
            if (IsEmpty) return false;
            if (target.IsFull) return false;
            var top = TopColor;
            var targetTop = target.TopColor;
            return !targetTop.HasValue || targetTop == top;
        }

        public int MaxPourAmountInto(Bottle target)
        {
            if (!CanPourInto(target)) return 0;
            return Math.Min(ContiguousTopCount, target.FreeSpace);
        }

        public void PourInto(Bottle target, int amount)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > MaxPourAmountInto(target)) throw new InvalidOperationException("Invalid pour amount");

            var color = TopColor;
            if (!color.HasValue) throw new InvalidOperationException("Source empty");

            int removed = 0;
            for (int i = _slots.Length - 1; i >= 0 && removed < amount; i--)
            {
                if (_slots[i] == color)
                {
                    _slots[i] = null;
                    removed++;
                }
                else if (_slots[i].HasValue)
                {
                    break;
                }
            }

            int inserted = 0;
            for (int i = 0; i < target._slots.Length && inserted < amount; i++)
            {
                if (!target._slots[i].HasValue)
                {
                    target._slots[i] = color;
                    inserted++;
                }
            }
        }

        public bool IsSolvedBottle()
        {
            if (IsEmpty) return false;
            if (!IsFull) return false;
            var color = _slots[0];
            if (!color.HasValue) return false;
            for (int i = 1; i < _slots.Length; i++)
            {
                if (_slots[i] != color) return false;
            }
            return true;
        }

        public bool IsSingleColorOrEmpty()
        {
            ColorId? color = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].HasValue) continue;
                if (!color.HasValue)
                {
                    color = _slots[i];
                    continue;
                }
                if (_slots[i] != color) return false;
            }
            return true;
        }
    }
}
