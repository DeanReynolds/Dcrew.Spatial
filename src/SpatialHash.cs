using Dcrew.ObjectPool;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Dcrew.MonoGame._2D_Spatial_Partition
{
    /// <summary>For very fast but approximate spatial partitioning. See <see cref="Spacing"/> before use</summary>
    public static class SpatialHash<T> where T : class, IAABB
    {
        const int DEFAULT_SPACING = 50;

        /// <summary>Set to your largest item collision radius. Default: 50</summary>
        public static int Spacing
        {
            get => _spacing;
            set
            {
                _spacing = value;
                //foreach (var obj in _stored.Keys)
                //    Update(obj);
            }
        }

        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
        public static bool Contains(T item) => _stored.ContainsKey(item);

        static readonly Dictionary<Point, HashSet<T>> _hash = new Dictionary<Point, HashSet<T>>();
        static readonly Dictionary<T, Point> _stored = new Dictionary<T, Point>();

        static int _spacing = DEFAULT_SPACING;

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public static void Add(T item)
        {
            var bucket = Bucket(item);
            Add(item, bucket);
            _stored.Add(item, bucket);
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        /// <returns>True if <paramref name="item"/> was moved to a new bucket, false otherwise</returns>
        public static bool Update(T item)
        {
            Point bucket = Bucket(item),
                i = _stored[item];
            if (bucket == i)
                return false;

            //var t = _hash[i];
            //if (t.Count <= 1)
            //{
            //    t.Clear();
            //    Pool<HashSet<T>>.Free(t);
            //    _hash.Remove(i);
            //}
            //else
            //    t.Remove(item);
            _hash[i].Remove(item);

            Add(item, bucket);
            _stored[item] = bucket;
            return true;
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public static void Remove(T item)
        {
            var i = _stored[item];
            var t = _hash[i];
            if (t.Count <= 1)
            {
                t.Clear();
                Pool<HashSet<T>>.Free(t);
                _hash.Remove(i);
            }
            else
                t.Remove(item);
            _stored.Remove(item);
        }
        /// <summary>Removes all items and buckets from the tree</summary>
        public static void Clear()
        {
            foreach (var t in _hash.Values)
            {
                t.Clear();
                Pool<HashSet<T>>.Free(t);
            }
            _hash.Clear();
            _stored.Clear();
        }
        /// <summary>Query and return the items intersecting <paramref name="pos"/></summary>
        public static IEnumerable<T> Query(Vector2 pos) => Query(new Point((int)(pos.X / Spacing), (int)(pos.Y / Spacing)));
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        public static IEnumerable<T> Query(Rectangle area)
        {
            int x = area.X / Spacing,
                y = area.Y / Spacing;
            int lX = area.Width / Spacing + x + 1,
                lY = area.Height / Spacing + y + 1;
            for (var j = x; j < lX; j += 3)
                for (var k = y; k < lY; k += 3)
                    foreach (var t in InQuery(new Point(j, k)))
                        yield return t;
            int e = area.Width / Spacing % 3;
            if (e > 0)
            {
                for (var k = y; k < lY; k += 3)
                    foreach (var t in InQuery(new Point(e % 2 + lX, k)))
                        yield return t;
                lX += 3;
            }
            e = area.Height / Spacing % 3;
            if (e > 0)
                for (var j = x; j < lX; j += 3)
                    foreach (var t in InQuery(new Point(j, e % 2 + lY)))
                        yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
        /// <param name="origin">Origin of <paramref name="area"/></param>
        public static IEnumerable<T> Query(Rectangle area, float angle, Vector2 origin)
        {
            float cos = MathF.Cos(angle),
                sin = MathF.Sin(angle);
            Vector2 RotatePoint(Vector2 p, Vector2 o)
            {
                float x = p.X - o.X,
                    y = p.Y - o.Y;
                return new Vector2(x * cos - y * sin, x * sin + y * cos);
            }
            origin = new Vector2(area.X + origin.X, area.Y + origin.Y);
            Point tL = RotatePoint(new Vector2(area.X, area.Y), origin).ToPoint(),
                tR = RotatePoint(new Vector2(area.Right, area.Y), origin).ToPoint(),
                bR = RotatePoint(new Vector2(area.Right, area.Bottom), origin).ToPoint(),
                bL = RotatePoint(new Vector2(area.X, area.Bottom), origin).ToPoint(),
                min = new Point(Math.Min(Math.Min(tL.X, tR.X), Math.Min(bR.X, bL.X)), Math.Min(Math.Min(tL.Y, tR.Y), Math.Min(bR.Y, bL.Y))),
                max = new Point(Math.Max(Math.Max(tL.X, tR.X), Math.Max(bR.X, bL.X)), Math.Max(Math.Max(tL.Y, tR.Y), Math.Max(bR.Y, bL.Y)));
            area = new Rectangle(min + area.Location, max - min);
            foreach (var t in Query(area))
                yield return t;
        }

        static Point Bucket(T obj)
        {
            var p = obj.AABB.Center;
            return new Point(p.X / Spacing, p.Y / Spacing);
        }

        static void Add(T obj, Point bucket)
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

        static IEnumerable<T> Query(Point p)
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