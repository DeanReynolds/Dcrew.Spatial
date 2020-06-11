using System;

namespace Dcrew.Spatial
{
    static class Pool<T> where T : class, new()
    {
        const int _defCap = 4;

        public static int Count { get; private set; }

        public static int Size => _arr.Length;

        static T[] _arr = new T[0];

        public static void EnsureCount(int size)
        {
            if (Count >= size)
                return;
            lock (_arr)
            {
                SetArrSize(size);
                var n = size - Count;
                for (var i = 0; i < n; i++)
                    _arr[Count++] = new T();
            }
        }
        public static void ExpandSize(int amount)
        {
            lock (_arr)
            {
                SetArrSize(_arr.Length + amount);
                for (var i = 0; i < amount; i++)
                    _arr[Count++] = new T();
            }
        }

        public static T Spawn()
        {
            T obj;
            lock (_arr)
            {
                if (Count == 0)
                    ExpandSize(_defCap); // This method also locks _arr, but the lock is http://en.wikipedia.org/wiki/Reentrant_mutex
                obj = _arr[--Count];
                _arr[Count] = default;
            }
            return obj;
        }
        public static void Free(T obj)
        {
            lock (_arr)
            {
                if (Count == _arr.Length)
                    SetArrSize(_arr.Length + _defCap);
                _arr[Count++] = obj;
            }
        }

        static void SetArrSize(int amount)
        {
            var newArr = new T[amount];
            Array.Copy(_arr, 0, newArr, 0, Count);
            _arr = newArr;
        }
    }
}