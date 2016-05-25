using System.Collections.Generic;
using System.Diagnostics;

namespace Es.Ring
{
    public sealed class Ring<T>
    {
        private const int MaxBatchSize = 128;
        private long _readHead;

#pragma warning disable 169
        // Forcing the read and write heads onto different cache lines (assuming 128 byte or less cache lines).
        // This really doesn't make much difference. ~%5 in my tests, which may just be noise.
        // It's probably just cargo cult, but it does capture that we considered the impact
        // of cache line sharing between the two heads. :^)
        //
        // Initial I did this with a struct and FieldOffset, which is slower because it introduces
        // indirect access to the two heads via the struct pointer.
        private long _unused0,
            _unused1,
            _unused2,
            _unused3,
            _unused4,
            _unused5,
            _unused6,
            _unused7,
            _unused8,
            _unused9,
            _unusedA,
            _unusedB,
            _unusedC,
            _unusedD,
            _unusedE,
            _unusedF;
#pragma warning restore 169

        private long _writeHead;

        private readonly int _ringSize;

        // Exposed because if you're using this you care about performance more than abstraction or safety.
        private readonly int _ringMask;
        private readonly T[] _items;

        public Ring(int ringSize)
        {
            _ringSize = ringSize;
            Debug.Assert((ringSize & (ringSize - 1)) == 0, "ringSize MUST be a power of 2");
            Debug.Assert(ringSize > MaxBatchSize);
            _ringMask = _ringSize - 1;
            _items = new T[_ringSize];

        }
        /// <summary>
        /// NOT safe to call from more than one thread, assumes a single producer thread.
        /// </summary>
        public bool TryAdd(T t)
        {
            var writehead = _writeHead;
            var readHead = _readHead;

            var readyToRead = writehead - readHead;
            var readyToWrite = _ringSize - readyToRead;

            if (readyToWrite == 0)
            {
                return false;
            }
            _items[writehead&_ringMask] = t;
            _writeHead = writehead + 1;
            return true;
        }

        /// <summary>
        /// NOT safe to call from more than one thread, assumes a single producer thread.
        /// </summary>
        public bool TryAdd(ICollection<T> ts)
        {
            var writeHead = _writeHead;
            var readHead = _readHead;

            var readyToRead = writeHead - readHead;
            var readyToWrite = _ringSize - readyToRead;

            if (readyToWrite < ts.Count)
            {
                return false;
            }

            foreach (var t in ts)
            {
                _items[writeHead&_ringMask] = t;
                ++writeHead;
            }

            _writeHead = writeHead;

            return true;
        }

        public delegate void ConsumerAction(T[] items, long start, long count, long mask);

        /// <summary>
        /// NOT safe to call from more than one thread, assumes a single producer thread.
        /// end is not inclusive, consume [start,end) and mask to get array location
        /// </summary>
        public bool TryRead(ConsumerAction action)
        {
            var writeHead = _writeHead;
            var readHead = _readHead;

            var readyToRead = writeHead - readHead;
            if (readyToRead == 0)
            {
                return false;
            }
            action(_items, readHead, writeHead, _ringMask);
            _readHead = writeHead;
            return true;
        }
    }
}