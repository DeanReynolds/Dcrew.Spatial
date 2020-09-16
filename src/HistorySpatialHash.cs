using Apos.History;
using Microsoft.Xna.Framework;
using System.Collections;
using System.Collections.Generic;

namespace Dcrew.Spatial {
    /// <summary>A <see cref="SpatialHash{T}"/> using <see cref="Apos.History"/> to allow for undoing/redoing</summary>
    public class HistorySpatialHash<T> : History, IEnumerable<T> where T : class, IBounds {
        /// <summary>Set to your largest item collision radius. Default: 50</summary>
        public int Spacing {
            get => _tree.Spacing;
            set {
                var spacing = _tree.Spacing;
                _futureSetup.Add(() => {
                    spacing = _tree.Spacing;
                    _tree.Spacing = value;
                });
                _pastSetup.Add(() => { _tree.Spacing = spacing; });
            }
        }

        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
        public bool Contains(T item) => _tree.Contains(item);
        /// <summary>Returns an enumerator that iterates through the collection</summary>
        public IEnumerator<T> GetEnumerator() => _tree.GetEnumerator();
        /// <summary>Return count of all items</summary>
        public int ItemCount => _tree.ItemCount;
        /// <summary>Return all items and their container rects</summary>
        public IEnumerable<(T Item, Vector2 Node)> Bundles => _tree.Bundles;

        readonly SpatialHash<T> _tree = new SpatialHash<T>();

        HistorySpatialHash() : base(new Optional.Option<HistoryHandler>()) { }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public void Add(T item) {
            _futureSetup.Add(() => { _tree.Add(item); });
            _pastSetup.Add(() => { _tree.Remove(item); });
            TryCommit();
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Update(T item) {
            var bucket = _tree.Bucket(item);
            _futureSetup.Add(() => {
                bucket = _tree.Bucket(item);
                _tree.Update(item);
            });
            _pastSetup.Add(() => {
                _tree.Remove(item);
                _tree.Add(item, bucket);
            });
            TryCommit();
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Remove(T item) {
            _futureSetup.Add(() => { _tree.Remove(item); });
            _pastSetup.Add(() => { _tree.Add(item); });
            TryCommit();
        }
        /// <summary>Removes all items and nodes from the tree</summary>
        public void Clear() {
            (T Item, Point Bucket)[] items = new (T, Point)[0];
            _futureSetup.Add(() => {
                items = new (T, Point)[_tree.ItemCount];
                var i = 0;
                foreach (var item in _tree)
                    items[i++] = (item, _tree.Bucket(item));
                _tree.Clear();
            });
            _pastSetup.Add(() => {
                foreach (var (Item, Bucket) in items)
                    _tree.Add(Item, Bucket);
            });
            TryCommit();
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Point xy) => _tree.Query(xy);
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Vector2 xy) => _tree.Query(xy);
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        public IEnumerable<T> Query(Rectangle area) => _tree.Query(area);
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        /// <param name="area">Area (rectangle)</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
        /// <param name="origin">Origin (in pixels) of <paramref name="area"/></param>
        public IEnumerable<T> Query(Rectangle area, float angle, Vector2 origin) => _tree.Query(area, angle, origin);

        IEnumerator IEnumerable.GetEnumerator() => _tree.GetEnumerator();
    }
}