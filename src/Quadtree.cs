using Dcrew.ObjectPool;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dcrew.MonoGame._2D_Spatial_Partition
{
    /// <summary>For fast and accurate spatial partitioning. Set <see cref="Bounds"/> before use</summary>
    public static class Quadtree<T> where T : class, IAABB
    {
        class Node : IPoolable
        {
            const int CAPACITY = 8;

            public Rectangle Bounds { get; internal set; }

            public int Count
            {
                get
                {
                    int c = _items.Count;
                    if (_nw != null)
                    {
                        c += _ne.Count;
                        c += _se.Count;
                        c += _sw.Count;
                        c += _nw.Count;
                    }
                    return c;
                }
            }
            public IEnumerable<T> AllItems
            {
                get
                {
                    foreach (var i in _items)
                        yield return i;
                    if (_nw != null)
                    {
                        foreach (var i in _ne.AllItems)
                            yield return i;
                        foreach (var i in _se.AllItems)
                            yield return i;
                        foreach (var i in _sw.AllItems)
                            yield return i;
                        foreach (var i in _nw.AllItems)
                            yield return i;
                    }
                }
            }
            public IEnumerable<T> AllSubItems
            {
                get
                {
                    foreach (var i in _ne.AllItems)
                        yield return i;
                    foreach (var i in _se.AllItems)
                        yield return i;
                    foreach (var i in _sw.AllItems)
                        yield return i;
                    foreach (var i in _nw.AllItems)
                        yield return i;
                }
            }

            internal Node _parent, _ne, _se, _sw, _nw;

            internal readonly HashSet<T> _items = new HashSet<T>(CAPACITY);

            public Node Add(T item, Point pos)
            {
                Node Bury(T i, Node n, Point p)
                {
                    if (n._ne.Bounds.Contains(p))
                        return n._ne.Add(i, p);
                    if (n._se.Bounds.Contains(p))
                        return n._se.Add(i, p);
                    if (n._sw.Bounds.Contains(p))
                        return n._sw.Add(i, p);
                    if (n._nw.Bounds.Contains(p))
                        return n._nw.Add(i, p);
                    return n;
                }
                if (_nw == null)
                    if (_items.Count >= CAPACITY && Bounds.Width * Bounds.Height > 1024)
                    {
                        int halfWidth = (int)MathF.Ceiling(Bounds.Width / 2f),
                            halfHeight = (int)MathF.Ceiling(Bounds.Height / 2f);
                        _nw = Pool<Node>.Spawn();
                        _nw.Bounds = new Rectangle(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                        _nw._parent = this;
                        _sw = Pool<Node>.Spawn();
                        int midY = Bounds.Top + halfHeight,
                            height = Bounds.Bottom - midY;
                        _sw.Bounds = new Rectangle(Bounds.Left, midY, halfWidth, height);
                        _sw._parent = this;
                        _ne = Pool<Node>.Spawn();
                        int midX = Bounds.Left + halfWidth,
                            width = Bounds.Right - midX;
                        _ne.Bounds = new Rectangle(midX, Bounds.Top, width, halfHeight);
                        _ne._parent = this;
                        _se = Pool<Node>.Spawn();
                        _se.Bounds = new Rectangle(midX, midY, width, height);
                        _se._parent = this;
                        foreach (var i in _items)
                            _stored[i] = (Bury(i, this, _stored[i].Pos), _stored[i].Pos);
                        _items.Clear();
                    }
                    else
                        goto add;
                return Bury(item, this, pos);
            add:
                _items.Add(item);
                return this;
            }
            public void Remove(T item)
            {
                _items.Remove(item);
                if (_parent == null)
                    return;
                _nodesToClean.Add(_parent);
            }
            public IEnumerable<T> Query(Rectangle broad, Rectangle query)
            {
                if (_nw == null)
                {
                    foreach (T i in _items)
                        if (query.Intersects(i.AABB))
                            yield return i;
                    yield break;
                }
                if (_ne.Bounds.Contains(broad))
                {
                    foreach (var i in _ne.Query(broad, query))
                        yield return i;
                    yield break;
                }
                if (_se.Bounds.Contains(broad))
                {
                    foreach (var i in _se.Query(broad, query))
                        yield return i;
                    yield break;
                }
                if (_sw.Bounds.Contains(broad))
                {
                    foreach (var i in _sw.Query(broad, query))
                        yield return i;
                    yield break;
                }
                if (_nw.Bounds.Contains(broad))
                {
                    foreach (var i in _nw.Query(broad, query))
                        yield return i;
                    yield break;
                }
                if (broad.Contains(_ne.Bounds) || _ne.Bounds.Intersects(broad))
                    foreach (var i in _ne.Query(broad, query))
                        yield return i;
                if (broad.Contains(_se.Bounds) || _se.Bounds.Intersects(broad))
                    foreach (var i in _se.Query(broad, query))
                        yield return i;
                if (broad.Contains(_sw.Bounds) || _sw.Bounds.Intersects(broad))
                    foreach (var i in _sw.Query(broad, query))
                        yield return i;
                if (broad.Contains(_nw.Bounds) || _nw.Bounds.Intersects(broad))
                    foreach (var i in _nw.Query(broad, query))
                        yield return i;
            }

            public void OnSpawn() { }
            public void OnFree() => _items.Clear();

            internal void FreeSubNodes()
            {
                if (_nw == null)
                    return;
                _ne.FreeSubNodes();
                _se.FreeSubNodes();
                _sw.FreeSubNodes();
                _nw.FreeSubNodes();
                Pool<Node>.Free(_ne);
                Pool<Node>.Free(_se);
                Pool<Node>.Free(_sw);
                Pool<Node>.Free(_nw);
                _ne = null;
                _se = null;
                _sw = null;
                _nw = null;
            }
            internal void Clean()
            {
                if (_nw == null || Count >= CAPACITY)
                    return;
                foreach (var i in AllSubItems)
                {
                    _items.Add(i);
                    _stored[i] = (this, _stored[i].Pos);
                }
                _ne.FreeSubNodes();
                _se.FreeSubNodes();
                _sw.FreeSubNodes();
                _nw.FreeSubNodes();
                Pool<Node>.Free(_ne);
                Pool<Node>.Free(_se);
                Pool<Node>.Free(_sw);
                Pool<Node>.Free(_nw);
                _ne = null;
                _se = null;
                _sw = null;
                _nw = null;
            }
        }

        sealed class CleanNodes : IGameComponent, IUpdateable
        {
            public bool Enabled => true;

            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            public void Initialize() { }

            public void Update(GameTime gameTime)
            {
                foreach (var n in _nodesToClean)
                    n.Clean();
                _nodesToClean.Clear();
                if (_updates.HasFlag(Updates.AutoCleanNodes))
                {
                    _components.Remove(this);
                    _updates &= ~Updates.AutoCleanNodes;
                }
            }
        }

        sealed class ExpandTree : IGameComponent, IUpdateable
        {
            public bool Enabled => true;

            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            public void Initialize() { }

            public void Update(GameTime gameTime)
            {
                int newLeft = Math.Min(Bounds.Left, _extendToW),
                    newTop = Math.Min(Bounds.Top, _extendToN),
                    newWidth = Bounds.Right - newLeft,
                    newHeight = Bounds.Bottom - newTop;
                Bounds = new Rectangle(newLeft, newTop, Math.Max(newWidth, _extendToE - newLeft + 1), Math.Max(newHeight, _extendToS - newTop + 1));
                _extendToN = int.MaxValue;
                _extendToE = 0;
                _extendToS = 0;
                _extendToW = int.MaxValue;
                if (_updates.HasFlag(Updates.AutoExpandTree))
                {
                    _components.Remove(this);
                    _updates &= ~Updates.AutoExpandTree;
                }
            }
        }

        /// <summary>Set the boundary rect of this tree</summary>
        public static Rectangle Bounds
        {
            get => _mainNode.Bounds;
            set
            {
                var items = _stored.Keys.ToArray();
                _mainNode.FreeSubNodes();
                _mainNode.OnFree();
                _mainNode.Bounds = value;
                static int NodeCount(Point b)
                {
                    var r = 1;
                    if (b.X * b.Y > 1024)
                        r += NodeCount(new Point(b.X / 2, b.Y / 2)) * 4;
                    return r;
                }
                Pool<Node>.EnsureCount(NodeCount(new Point(value.Width, value.Height)));
                foreach (var i in items)
                {
                    var pos = _stored[i].Pos;
                    _stored[i] = (_mainNode.Add(i, pos), pos);
                }
            }
        }

        static Quadtree()
        {
            foreach (var p in typeof(Game).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static))
                if (p.GetValue(_game) is Game g)
                    _game = g;
            _components = _game.Components;
        }

        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
        public static bool Contains(T item) => _stored.ContainsKey(item);
        /// <summary>Return all items</summary>
        public static IEnumerable<T> Items
        {
            get
            {
                foreach (var i in _stored)
                    yield return i.Key;
            }
        }
        /// <summary>Return all items and their container rects</summary>
        public static IEnumerable<(T Item, Rectangle Node)> Bundles
        {
            get
            {
                foreach (var i in _stored)
                    yield return (i.Key, i.Value.Node.Bounds);
            }
        }
        /// <summary>Return all node bounds in this tree</summary>
        public static IEnumerable<Rectangle> Nodes
        {
            get
            {
                IEnumerable<Node> Nodes(Node n)
                {
                    yield return n;
                    if (n._nw == null)
                        yield break;
                    foreach (var n2 in Nodes(n._ne))
                        yield return n2;
                    foreach (var n2 in Nodes(n._se))
                        yield return n2;
                    foreach (var n2 in Nodes(n._sw))
                        yield return n2;
                    foreach (var n2 in Nodes(n._nw))
                        yield return n2;
                }
                foreach (var n in Nodes(_mainNode))
                    yield return n.Bounds;
            }
        }

        static readonly Game _game;
        static readonly GameComponentCollection _components;
        static readonly Node _mainNode = new Node();
        static readonly IDictionary<T, (Node Node, Point Pos)> _stored = new Dictionary<T, (Node, Point)>();
        static readonly CleanNodes _cleanNodes = new CleanNodes();
        static readonly ExpandTree _expandTree = new ExpandTree();

        static (T Item, Point HalfSize, Point Size) _maxSizeAABB;
        static int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        static HashSet<Node> _nodesToClean = new HashSet<Node>();
        static Updates _updates;
        static event AddItem _addItem = InitAdd;

        delegate void AddItem(T item);

        [Flags] enum Updates : byte { ManualMode = 1, AutoCleanNodes = 2, AutoExpandTree = 4, ManualCleanNodes = 8, ManualExpandTree = 16 }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public static void Add(T item)
        {
            _addItem?.Invoke(item);
            var aabb = item.AABB;
            var pos = aabb.Center;
            _stored.Add(item, (_mainNode.Add(item, pos), pos));
            if (aabb.Width > _maxSizeAABB.Size.X || aabb.Height > _maxSizeAABB.Size.Y)
                _maxSizeAABB = (item, new Point((int)MathF.Ceiling(aabb.Width / 2f), (int)MathF.Ceiling(aabb.Height / 2f)), new Point(aabb.Width, aabb.Height));
            TryExpandTree(pos);
        }

        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public static void Remove(T item)
        {
            _stored[item].Node.Remove(item);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            _stored.Remove(item);
            if (ReferenceEquals(item, _maxSizeAABB.Item))
            {
                _maxSizeAABB = (default, Point.Zero, Point.Zero);
                foreach (T i in _stored.Keys)
                    if (i.AABB.Width > _maxSizeAABB.Size.X || i.AABB.Height > _maxSizeAABB.Size.Y)
                        _maxSizeAABB = (i, new Point((int)MathF.Ceiling(i.AABB.Width / 2f), (int)MathF.Ceiling(i.AABB.Height / 2f)), new Point(i.AABB.Width, i.AABB.Height));
            }
        }
        /// <summary>Removes all items and nodes from the tree</summary>
        public static void Clear()
        {
            _mainNode.FreeSubNodes();
            _mainNode.OnFree();
            _stored.Clear();
            _maxSizeAABB = (default, Point.Zero, Point.Zero);
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public static void Update(T item)
        {
            var aabb = item.AABB;
            var newPos = aabb.Center;
            if (aabb.Width > _maxSizeAABB.Size.X || aabb.Height > _maxSizeAABB.Size.Y)
                _maxSizeAABB = (item, new Point((int)MathF.Ceiling(aabb.Width / 2f), (int)MathF.Ceiling(aabb.Height / 2f)), new Point(aabb.Width, aabb.Height));
            if (TryExpandTree(newPos))
                return;
            var c = _stored[item];
            if (c.Node.Bounds.Contains(newPos) || c.Node._parent == null)
            {
                _stored[item] = (c.Node, newPos);
                return;
            }
            c.Node.Remove(item);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            Node GetNewNode(Node n)
            {
                if (n._parent == null)
                    return n;
                if (n._parent.Bounds.Contains(newPos))
                    return n._parent;
                else
                    return GetNewNode(n._parent);
            }
            _stored[item] = (GetNewNode(c.Node).Add(item, newPos), newPos);
        }
        /// <summary>Query and return the items intersecting <paramref name="pos"/></summary>
        public static IEnumerable<T> Query(Point pos)
        {
            foreach (var t in _mainNode.Query(new Rectangle(pos.X - _maxSizeAABB.HalfSize.X, pos.Y - _maxSizeAABB.HalfSize.Y, _maxSizeAABB.Size.X + 1, _maxSizeAABB.Size.Y + 1), new Rectangle(pos, new Point(1))))
                yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="pos"/></summary>
        public static IEnumerable<T> Query(Vector2 pos)
        {
            foreach (var t in _mainNode.Query(new Rectangle((int)MathF.Round(pos.X - _maxSizeAABB.HalfSize.X), (int)MathF.Round(pos.Y - _maxSizeAABB.HalfSize.Y), _maxSizeAABB.Size.X + 1, _maxSizeAABB.Size.Y + 1), new Rectangle((int)MathF.Round(pos.X), (int)MathF.Round(pos.Y), 1, 1)))
                yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        public static IEnumerable<T> Query(Rectangle area)
        {
            foreach (var t in _mainNode.Query(new Rectangle(area.X - _maxSizeAABB.HalfSize.X, area.Y - _maxSizeAABB.HalfSize.Y, _maxSizeAABB.Size.X + area.Width, _maxSizeAABB.Size.Y + area.Height), area))
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
            foreach (var t in _mainNode.Query(new Rectangle(area.X - _maxSizeAABB.HalfSize.X, area.Y - _maxSizeAABB.HalfSize.Y, _maxSizeAABB.Size.X + area.Width, _maxSizeAABB.Size.Y + area.Height), area))
                yield return t;
        }

        /// <summary>You need to call this if you don't use base.Update() in <see cref="Game.Update(GameTime)"/></summary>
        public static void Update()
        {
            if (_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _components.Remove(_cleanNodes);
                _cleanNodes.Update(null);
            }
            else if (_updates.HasFlag(Updates.ManualCleanNodes))
                _cleanNodes.Update(null);
            if (_updates.HasFlag(Updates.AutoExpandTree))
            {
                _components.Remove(_expandTree);
                _expandTree.Update(null);
            }
            else if (_updates.HasFlag(Updates.ManualExpandTree))
                _expandTree.Update(null);
            _updates = Updates.ManualMode;
        }

        /// <summary>Shrinks the tree to the smallest possible size</summary>
        public static void Shrink()
        {
            if (_stored.Count == 0)
                return;
            Point min = new Point(int.MaxValue),
                max = new Point(int.MinValue);
            foreach (var i in Items)
            {
                var pos = i.AABB.Center;
                if (pos.X < min.X)
                    min.X = pos.X;
                if (pos.X > max.X)
                    max.X = pos.X;
                if (pos.Y < min.Y)
                    min.Y = pos.Y;
                if (pos.Y > max.Y)
                    max.Y = pos.Y;
            }
            Bounds = new Rectangle(min.X, min.Y, max.X - min.X + 1, max.Y - min.Y + 1);
        }

        static bool TryExpandTree(Point pos)
        {
            if (Bounds.Left > pos.X || Bounds.Top > pos.Y || Bounds.Right < pos.X + 1 || Bounds.Bottom < pos.Y + 1)
            {
                if (pos.Y < _extendToN)
                    _extendToN = pos.Y;
                if (pos.X > _extendToE)
                    _extendToE = pos.X;
                if (pos.Y > _extendToS)
                    _extendToS = pos.Y;
                if (pos.X < _extendToW)
                    _extendToW = pos.X;
                if (_updates.HasFlag(Updates.ManualMode))
                    _updates |= Updates.ManualExpandTree;
                else if (!_updates.HasFlag(Updates.AutoExpandTree))
                {
                    _components.Add(_expandTree);
                    _updates |= Updates.AutoExpandTree;
                }
                return true;
            }
            return false;
        }

        static void InitAdd(T item)
        {
            Bounds = new Rectangle(item.AABB.Center, new Point(1));
            _addItem -= InitAdd;
        }
    }
}