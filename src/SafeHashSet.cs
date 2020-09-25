using System.Collections;
using System.Collections.Generic;

namespace Dcrew.Spatial {
    sealed class SafeHashSet<T> : ICollection<T>, IEnumerable<T> {
        public int Count => _set.Count;
        public bool IsReadOnly => false;

        bool _isDirty;

        internal readonly HashSet<T> _set,
            _dirty;

        public SafeHashSet() : this(4) { }
        public SafeHashSet(int capacity) {
            _set = new HashSet<T>(capacity);
            _dirty = new HashSet<T>(capacity);
        }

        public void Add(T item) {
            _set.Add(item);
            _isDirty = true;
        }
        public bool Remove(T item) {
            if (_set.Remove(item)) {
                _isDirty = true;
                return true;
            }
            return false;
        }
        public void Clear() => _set.Clear();
        public bool Contains(T item) => _set.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _set.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() {
            if (_isDirty) {
                _dirty.Clear();
                _dirty.UnionWith(_set);
                _isDirty = false;
            }
            return _dirty.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}