using System;
using System.Collections.Generic;

namespace Decantra.Domain.Solver
{
    internal sealed class PriorityQueue<T>
    {
        private readonly List<T> _data;
        private readonly IComparer<T> _comparer;

        public PriorityQueue(IComparer<T> comparer)
        {
            _data = new List<T>();
            _comparer = comparer ?? Comparer<T>.Default;
        }

        public int Count => _data.Count;

        public void Enqueue(T item)
        {
            _data.Add(item);
            int ci = _data.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (_comparer.Compare(_data[ci], _data[pi]) >= 0) break;
                T tmp = _data[ci];
                _data[ci] = _data[pi];
                _data[pi] = tmp;
                ci = pi;
            }
        }

        public T Dequeue()
        {
            int li = _data.Count - 1;
            T frontItem = _data[0];
            _data[0] = _data[li];
            _data.RemoveAt(li);

            --li;
            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;
                int rc = ci + 1;
                if (rc <= li && _comparer.Compare(_data[rc], _data[ci]) < 0) ci = rc;
                if (_comparer.Compare(_data[pi], _data[ci]) <= 0) break;
                T tmp = _data[pi];
                _data[pi] = _data[ci];
                _data[ci] = tmp;
                pi = ci;
            }
            return frontItem;
        }
    }
}
