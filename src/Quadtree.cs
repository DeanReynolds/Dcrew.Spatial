using Dcrew.ObjectPool;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dcrew.Spatial
{
    /// <summary>For fast and accurate spatial partitioning. Set <see cref="Bounds"/> before use</summary>
    public sealed class Quadtree<T> where T : class, IAABB
    {
        internal sealed class Node : IPoolable
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

            internal Quadtree<T> _qtree;
            internal Node _parent, _ne, _se, _sw, _nw;

            internal readonly HashSet<T> _items = new HashSet<T>(CAPACITY);

            public Node Add(T item, Point pos)
            {
                static Node Bury(T i, Node n, Point p)
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
                        _nw._qtree = _qtree;
                        _nw.Bounds = new Rectangle(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                        _nw._parent = this;
                        _sw = Pool<Node>.Spawn();
                        _sw._qtree = _qtree;
                        int midY = Bounds.Top + halfHeight,
                            height = Bounds.Bottom - midY;
                        _sw.Bounds = new Rectangle(Bounds.Left, midY, halfWidth, height);
                        _sw._parent = this;
                        _ne = Pool<Node>.Spawn();
                        _ne._qtree = _qtree;
                        int midX = Bounds.Left + halfWidth,
                            width = Bounds.Right - midX;
                        _ne.Bounds = new Rectangle(midX, Bounds.Top, width, halfHeight);
                        _ne._parent = this;
                        _se = Pool<Node>.Spawn();
                        _se._qtree = _qtree;
                        _se.Bounds = new Rectangle(midX, midY, width, height);
                        _se._parent = this;
                        foreach (var i in _items)
                            _qtree._item[i] = (Bury(i, this, _qtree._item[i].XY), _qtree._item[i].XY);
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
                _qtree._nodesToClean.Add(_parent);
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
                    _qtree._item[i] = (this, _qtree._item[i].XY);
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

        internal sealed class CleanNodes : IGameComponent, IUpdateable
        {
            readonly Quadtree<T> _qtree;

            public bool Enabled => true;
            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal CleanNodes(Quadtree<T> qtree) => _qtree = qtree;

            public void Initialize() { }

            public void Update(GameTime gameTime)
            {
                foreach (var n in _qtree._nodesToClean)
                    n.Clean();
                _qtree._nodesToClean.Clear();
                if (_qtree._updates.HasFlag(Updates.AutoCleanNodes))
                {
                    _game.Components.Remove(this);
                    _qtree._updates &= ~Updates.AutoCleanNodes;
                }
            }
        }

        internal sealed class ExpandTree : IGameComponent, IUpdateable
        {
            readonly Quadtree<T> _qtree;

            public bool Enabled => true;

            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal ExpandTree(Quadtree<T> qtree) => _qtree = qtree;

            public void Initialize() { }

            public void Update(GameTime gameTime)
            {
                int newLeft = Math.Min(_qtree.Bounds.Left, _qtree._extendToW),
                    newTop = Math.Min(_qtree.Bounds.Top, _qtree._extendToN),
                    newWidth = _qtree.Bounds.Right - newLeft,
                    newHeight = _qtree.Bounds.Bottom - newTop;
                _qtree.Bounds = new Rectangle(newLeft, newTop, Math.Max(newWidth, _qtree._extendToE - newLeft + 1), Math.Max(newHeight, _qtree._extendToS - newTop + 1));
                _qtree._extendToN = int.MaxValue;
                _qtree._extendToE = 0;
                _qtree._extendToS = 0;
                _qtree._extendToW = int.MaxValue;
                if (_qtree._updates.HasFlag(Updates.AutoExpandTree))
                {
                    _game.Components.Remove(this);
                    _qtree._updates &= ~Updates.AutoExpandTree;
                }
            }
        }

        /// <summary>Set the boundary rect of this tree</summary>
        public Rectangle Bounds
        {
            get => _node.Bounds;
            set
            {
                if (_node == null)
                {
                    _node = new Node { Bounds = value, _qtree = this };
                    return;
                }
                var items = _item.Keys.ToArray();
                _node.FreeSubNodes();
                _node.OnFree();
                _node.Bounds = value;
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
                    var xy = _item[i].XY;
                    _item[i] = (_node.Add(i, xy), xy);
                }
            }
        }

        static readonly Game _game;

        static Quadtree()
        {
            foreach (var p in typeof(Game).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static))
                if (p.GetValue(_game) is Game g)
                    _game = g;
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
        /// <summary>Return all items and their container rects</summary>
        public IEnumerable<(T Item, Rectangle Node)> Bundles
        {
            get
            {
                foreach (var i in _item)
                    yield return (i.Key, i.Value.Node.Bounds);
            }
        }
        /// <summary>Return all node bounds in this tree</summary>
        public IEnumerable<Rectangle> Nodes
        {
            get
            {
                static IEnumerable<Node> Nodes(Node n)
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
                foreach (var n in Nodes(_node))
                    yield return n.Bounds;
            }
        }
        /// <summary>Return count of all nodes</summary>
        public int NodeCount
        {
            get
            {
                static int NodeCount(Node n)
                {
                    int count = 1;
                    if (n._nw == null)
                        return count;
                    count += NodeCount(n._ne);
                    count += NodeCount(n._se);
                    count += NodeCount(n._sw);
                    count += NodeCount(n._nw);
                    return count;
                }
                return NodeCount(_node);
            }
        }

        internal readonly Dictionary<T, (Node Node, Point XY)> _item = new Dictionary<T, (Node, Point)>();
        internal readonly CleanNodes _cleanNodes;
        internal readonly ExpandTree _expandTree;
        internal readonly HashSet<Node> _nodesToClean = new HashSet<Node>();

        (T Item, int Size, int HalfSize) _maxWidthItem,
            _maxHeightItem;
        int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        Updates _updates;
        Node _node;

        readonly Stack<Node> _nodesToQuery = new Stack<Node>();

        delegate void AddItem(T item);

        [Flags] enum Updates : byte { ManualMode = 1, AutoCleanNodes = 2, AutoExpandTree = 4, ManualCleanNodes = 8, ManualExpandTree = 16 }

        public Quadtree()
        {
            _cleanNodes = new CleanNodes(this);
            _expandTree = new ExpandTree(this);
        }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public void Add(T item)
        {
            var aabb = Util.Rotate(item.AABB, item.Angle, item.Origin);
            Insert(item, aabb);
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Update(T item)
        {
            var aabb = Util.Rotate(item.AABB, item.Angle, item.Origin);
            var xy = aabb.Center;
            if (aabb.Width > _maxWidthItem.Size)
                _maxWidthItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxHeightItem.Size)
                _maxHeightItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            if (TryExpandTree(xy))
                return;
            var c = _item[item];
            if (c.Node.Bounds.Contains(xy) || c.Node._parent == null)
            {
                _item[item] = (c.Node, xy);
                return;
            }
            c.Node.Remove(item);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            Node GetNewNode(Node n)
            {
                if (n._parent == null)
                    return n;
                if (n._parent.Bounds.Contains(xy))
                    return n._parent;
                else
                    return GetNewNode(n._parent);
            }
            _item[item] = (GetNewNode(c.Node).Add(item, xy), xy);
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Remove(T item)
        {
            _item[item].Node.Remove(item);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            _item.Remove(item);
            if (ReferenceEquals(item, _maxWidthItem.Item))
            {
                _maxWidthItem = (default, 0, 0);
                foreach (T i in _item.Keys)
                {
                    var aabb = Util.Rotate(item.AABB, item.Angle, item.Origin);
                    if (aabb.Width > _maxWidthItem.Size)
                        _maxWidthItem = (i, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
                }
            }
            if (ReferenceEquals(item, _maxHeightItem.Item))
            {
                _maxHeightItem = (default, 0, 0);
                foreach (T i in _item.Keys)
                {
                    var aabb = Util.Rotate(item.AABB, item.Angle, item.Origin);
                    if (i.AABB.Height > _maxHeightItem.Size)
                        _maxHeightItem = (i, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
                }
            }
        }
        /// <summary>Removes all items and nodes from the tree</summary>
        public void Clear()
        {
            _node.FreeSubNodes();
            _node.OnFree();
            _item.Clear();
            _maxWidthItem = (default, 0, 0);
            _maxHeightItem = (default, 0, 0);
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Point xy)
        {
            foreach (var t in Query(new Rectangle(xy.X, xy.Y, 1, 1)))
                yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Vector2 xy)
        {
            foreach (var t in Query(new Rectangle((int)MathF.Round(xy.X), (int)MathF.Round(xy.Y), 1, 1)))
                yield return t;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        public IEnumerable<T> Query(Rectangle area)
        {
            _nodesToQuery.Clear();
            _nodesToQuery.Push(_node);
            Node node;
            var broad = new Rectangle(area.X - _maxWidthItem.HalfSize, area.Y - _maxHeightItem.HalfSize, _maxWidthItem.Size + area.Width, _maxHeightItem.Size + area.Height);
            do
            {
                node = _nodesToQuery.Pop();
                if (node._nw == null)
                {
                    foreach (T i in node._items)
                        if (area.Intersects(i.AABB))
                            yield return i;
                    continue;
                }
                if (node._ne.Bounds.Intersects(broad))
                    _nodesToQuery.Push(node._ne);
                if (node._se.Bounds.Intersects(broad))
                    _nodesToQuery.Push(node._se);
                if (node._sw.Bounds.Intersects(broad))
                    _nodesToQuery.Push(node._sw);
                if (node._nw.Bounds.Intersects(broad))
                    _nodesToQuery.Push(node._nw);
            }
            while (_nodesToQuery.Count > 0);
            yield break;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        /// <param name="area">Area (rectangle)</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
        /// <param name="origin">Origin (in pixels) of <paramref name="area"/></param>
        public IEnumerable<T> Query(Rectangle area, float angle, Vector2 origin)
        {
            area = Util.Rotate(area, angle, origin);
            foreach (var t in Query(area))
                yield return t;
        }

        /// <summary>You need to call this if you don't use base.Update() in <see cref="Game.Update(GameTime)"/></summary>
        public void Update()
        {
            if (_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _game.Components.Remove(_cleanNodes);
                _cleanNodes.Update(null);
            }
            else if (_updates.HasFlag(Updates.ManualCleanNodes))
                _cleanNodes.Update(null);
            if (_updates.HasFlag(Updates.AutoExpandTree))
            {
                _game.Components.Remove(_expandTree);
                _expandTree.Update(null);
            }
            else if (_updates.HasFlag(Updates.ManualExpandTree))
                _expandTree.Update(null);
            _updates = Updates.ManualMode;
        }

        /// <summary>Shrinks the tree to the smallest possible size</summary>
        public void Shrink()
        {
            if (_item.Count == 0)
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

        internal void Insert(T item, Rectangle aabb)
        {
            if (_node == null)
                Bounds = new Rectangle(aabb.Center, new Point(1));
            var xy = aabb.Center;
            if (aabb.Width > _maxWidthItem.Size)
                _maxWidthItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxHeightItem.Size)
                _maxHeightItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            _item.Add(item, (_node.Add(item, xy), xy));
            TryExpandTree(xy);
        }

        bool TryExpandTree(Point pos)
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
                    _game.Components.Add(_expandTree);
                    _updates |= Updates.AutoExpandTree;
                }
                return true;
            }
            return false;
        }
    }
}