using Dcrew.ObjectPool;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Dcrew.Spatial
{
    /// <summary>For very fast but approximate spatial partitioning. See <see cref="Spacing"/> before use</summary>
    public class SpatialHash<T> where T : class, IAABB
    {
        const int DEFAULT_SPACING = 50;

        /// <summary>Set to your largest item collision radius. Default: 50</summary>
        public int Spacing
        {
            get => _spacing;
            set
            {
                _halfSpacing = (_spacing = value) / 2;
                var bundles = _item.ToArray();
                foreach (var (_, n) in bundles)
                    if (_hash.ContainsKey(n))
                    {
                        var t = _hash[n];
                        t.Clear();
                        Pool<HashSet<T>>.Free(t);
                        _hash.Remove(n);
                    }
                foreach (var (i, _) in bundles)
                {
                    var b = Bucket(i);
                    Add(i, b);
                    _item[i] = b;
                }
            }
        }

        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
        public bool Contains(T item) => _item.ContainsKey(item);
        /// <summary>Return all items</summary>
        public IEnumerable<T> Items
        {
            get
            {
                foreach (var i in _item)
                    yield return i.Key;
            }
        }
        /// <summary>Return count of all items</summary>
        public int ItemCount => _item.Count;
        /// <summary>Return all items and their container points</summary>
        public IEnumerable<(T Item, Vector2 Node)> Bundles
        {
            get
            {
                foreach (var i in _item)
                    yield return (i.Key, new Vector2(i.Value.X * _spacing + _halfSpacing, i.Value.Y * _spacing + _halfSpacing));
            }
        }

        readonly Dictionary<Point, HashSet<T>> _hash = new Dictionary<Point, HashSet<T>>();
        readonly Dictionary<T, Point> _item = new Dictionary<T, Point>();

        int _spacing = 0,
            _halfSpacing;

        public SpatialHash(int spacing = DEFAULT_SPACING) => _halfSpacing = (_spacing = spacing) / 2;

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public void Add(T item)
        {
            var bucket = Bucket(item);
            Add(item, bucket);
            _item.Add(item, bucket);
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        /// <returns>True if <paramref name="item"/> was moved to a new bucket, false otherwise</returns>
        public bool Update(T item)
        {
            Point bucket = Bucket(item),
                i = _item[item];
            if (bucket == i)
                return false;
            var t = _hash[i];
            if (t.Count <= 1)
            {
                t.Clear();
                Pool<HashSet<T>>.Free(t);
                _hash.Remove(i);
            }
            else
                t.Remove(item);
            Add(item, bucket);
            _item[item] = bucket;
            return true;
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Remove(T item)
        {
            var i = _item[item];
            var t = _hash[i];
            if (t.Count <= 1)
            {
                t.Clear();
                Pool<HashSet<T>>.Free(t);
                _hash.Remove(i);
            }
            else
                t.Remove(item);
            _item.Remove(item);
        }
        /// <summary>Removes all items and buckets from the tree</summary>
        public void Clear()
        {
            foreach (var t in _hash.Values)
            {
                t.Clear();
                Pool<HashSet<T>>.Free(t);
            }
            _hash.Clear();
            _item.Clear();
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Point xy) => InQuery(new Point(xy.X / Spacing, xy.Y / Spacing));
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Vector2 xy) => InQuery(new Point((int)(xy.X / Spacing), (int)(xy.Y / Spacing)));
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        public IEnumerable<T> Query(Rectangle area)
        {
            int x = area.X / Spacing,
                y = area.Y / Spacing;
            int lX = area.Width / Spacing + x + 1,
                lY = area.Height / Spacing + y + 1;
            for (var j = x; j < lX; j += 3)
                for (var k = y; k < lY; k += 3)
                    foreach (var t in InQuery(new Point(j, k)))
                        if (t.AABB.Intersects(area))
                            yield return t;
            int e = area.Width / Spacing % 3;
            if (e > 0)
            {
                for (var k = y; k < lY; k += 3)
                    foreach (var t in InQuery(new Point(e % 2 + lX, k)))
                        if (t.AABB.Intersects(area))
                            yield return t;
                lX += 3;
            }
            e = area.Height / Spacing % 3;
            if (e > 0)
                for (var j = x; j < lX; j += 3)
                    foreach (var t in InQuery(new Point(j, e % 2 + lY)))
                        if (t.AABB.Intersects(area))
                            yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        /// <param name="area">Area (rectangle)</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
        /// <param name="origin">Origin of <paramref name="area"/></param>
        public IEnumerable<T> Query(Rectangle area, float angle, Vector2 origin)
        {
            area = Util.Rotate(area, angle, origin);
            foreach (var t in Query(area))
                yield return t;
        }

        internal Point Bucket(T item)
        {
            var aabb = Util.Rotate(item.AABB, item.Angle, item.Origin);
            var pos = aabb.Center;
            return new Point(pos.X / Spacing, pos.Y / Spacing);
        }

        internal void Add(T obj, Point bucket)
        {
            if (!_hash.ContainsKey(bucket))
            {
                var set = Pool<HashSet<T>>.Spawn();
                set.Add(obj);
                _hash.Add(bucket, set);
                return;
            }
            _hash[bucket].Add(obj);
        }

        IEnumerable<T> InQuery(Point p)
        {
            var bucket = p;
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X - 1, p.Y);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X + 1, p.Y);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X, p.Y - 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X, p.Y + 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X - 1, p.Y - 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X + 1, p.Y - 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X - 1, p.Y + 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = new Point(p.X + 1, p.Y + 1);
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
        }
    }
}