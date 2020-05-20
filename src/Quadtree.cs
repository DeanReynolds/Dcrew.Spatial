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
            public const int CAPACITY = 16;

            public Rectangle Bounds { get; internal set; }

            internal Quadtree<T> _tree;
            internal Node _parent, _ne, _se, _sw, _nw;

            internal readonly HashSet<T> _items = new HashSet<T>(CAPACITY);

            public void OnSpawn() { }
            public void OnFree() => _items.Clear();

            internal void FreeNodes()
            {
                if (_nw == null)
                    return;
                _tree._nodesToLoop.Push(_ne);
                _tree._nodesToLoop.Push(_se);
                _tree._nodesToLoop.Push(_sw);
                _tree._nodesToLoop.Push(_nw);
                _ne = null;
                _se = null;
                _sw = null;
                _nw = null;
                Node node;
                do
                {
                    node = _tree._nodesToLoop.Pop();
                    Pool<Node>.Free(node);
                    if (node._nw == null)
                        continue;
                    _tree._nodesToLoop.Push(node._ne);
                    _tree._nodesToLoop.Push(node._se);
                    _tree._nodesToLoop.Push(node._sw);
                    _tree._nodesToLoop.Push(node._nw);
                    node._ne = null;
                    node._se = null;
                    node._sw = null;
                    node._nw = null;
                }
                while (_tree._nodesToLoop.Count > 0);
            }
            internal void Clean()
            {
                if (_nw == null)
                    return;
                var count = 0;
                _tree._nodesToLoop.Push(this);
                Node node;
                do
                {
                    node = _tree._nodesToLoop.Pop();
                    count += node._items.Count;
                    if (node._nw == null)
                        continue;
                    _tree._nodesToLoop.Push(node._ne);
                    _tree._nodesToLoop.Push(node._se);
                    _tree._nodesToLoop.Push(node._sw);
                    _tree._nodesToLoop.Push(node._nw);
                }
                while (_tree._nodesToLoop.Count > 0);
                if (count >= CAPACITY)
                    return;
                _tree._nodesToLoop.Push(_ne);
                _tree._nodesToLoop.Push(_se);
                _tree._nodesToLoop.Push(_sw);
                _tree._nodesToLoop.Push(_nw);
                _ne = null;
                _se = null;
                _sw = null;
                _nw = null;
                do
                {
                    node = _tree._nodesToLoop.Pop();
                    foreach (var i in node._items)
                    {
                        _items.Add(i);
                        _tree._item[i] = (this, _tree._item[i].XY);
                    }
                    Pool<Node>.Free(node);
                    if (node._nw == null)
                        continue;
                    _tree._nodesToLoop.Push(node._ne);
                    _tree._nodesToLoop.Push(node._se);
                    _tree._nodesToLoop.Push(node._sw);
                    _tree._nodesToLoop.Push(node._nw);
                    node._ne = null;
                    node._se = null;
                    node._sw = null;
                    node._nw = null;
                }
                while (_tree._nodesToLoop.Count > 0);
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
            get => _root.Bounds;
            set
            {
                if (_root == null)
                {
                    _root = new Node { Bounds = value, _tree = this };
                    return;
                }
                var items = _item.Keys.ToArray();
                _root.FreeNodes();
                _root.OnFree();
                _root.Bounds = value;
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
                    var aabb = Util.Rotate(i.AABB, i.Angle, i.Origin);
                    _item[i] = (Insert(i, _root, aabb), aabb.Center);
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
                _nodesToLoop.Push(_root);
                Node node;
                do
                {
                    node = _nodesToLoop.Pop();
                    yield return node.Bounds;
                    if (node._nw == null)
                        continue;
                    _nodesToLoop.Push(node._ne);
                    _nodesToLoop.Push(node._se);
                    _nodesToLoop.Push(node._sw);
                    _nodesToLoop.Push(node._nw);
                }
                while (_nodesToLoop.Count > 0);
            }
        }
        /// <summary>Return count of all nodes</summary>
        public int NodeCount
        {
            get
            {
                var count = 0;
                _nodesToLoop.Push(_root);
                Node node;
                do
                {
                    node = _nodesToLoop.Pop();
                    count++;
                    if (node._nw == null)
                        continue;
                    _nodesToLoop.Push(node._ne);
                    _nodesToLoop.Push(node._se);
                    _nodesToLoop.Push(node._sw);
                    _nodesToLoop.Push(node._nw);
                }
                while (_nodesToLoop.Count > 0);
                return count;
            }
        }

        internal Node _root { get; private set; }

        internal readonly Dictionary<T, (Node Node, Point XY)> _item = new Dictionary<T, (Node, Point)>();
        internal readonly CleanNodes _cleanNodes;
        internal readonly ExpandTree _expandTree;
        internal readonly HashSet<Node> _nodesToClean = new HashSet<Node>();
        internal readonly Stack<Node> _nodesToLoop = new Stack<Node>();

        (T Item, int Size, int HalfSize) _maxWidthItem,
            _maxHeightItem;
        int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        Updates _updates;

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
            _item.Add(item, (Insert(item, _root, aabb), aabb.Center));
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
            c.Node._items.Remove(item);
            _nodesToClean.Add(c.Node._parent);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes))
            {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            _nodesToLoop.Push(c.Node);
            Node node;
            do
            {
                node = _nodesToLoop.Pop();
                if (node._parent == null)
                    continue;
                if (node._parent.Bounds.Contains(xy))
                    node = node._parent;
                else
                    _nodesToLoop.Push(node._parent);
            }
            while (_nodesToLoop.Count > 0);
            _item[item] = (Insert(item, node, aabb), xy);
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Remove(T item)
        {
            var node = _item[item].Node;
            node._items.Remove(item);
            if (node._parent != null)
                _nodesToClean.Add(node._parent);
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
            _root.FreeNodes();
            _root.OnFree();
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
            _nodesToLoop.Push(_root);
            Node node;
            var broad = new Rectangle(area.X - _maxWidthItem.HalfSize, area.Y - _maxHeightItem.HalfSize, _maxWidthItem.Size + area.Width, _maxHeightItem.Size + area.Height);
            do
            {
                node = _nodesToLoop.Pop();
                if (node._nw == null)
                {
                    foreach (T i in node._items)
                        if (area.Intersects(i.AABB))
                            yield return i;
                    continue;
                }
                if (node._ne.Bounds.Intersects(broad))
                    _nodesToLoop.Push(node._ne);
                if (node._se.Bounds.Intersects(broad))
                    _nodesToLoop.Push(node._se);
                if (node._sw.Bounds.Intersects(broad))
                    _nodesToLoop.Push(node._sw);
                if (node._nw.Bounds.Intersects(broad))
                    _nodesToLoop.Push(node._nw);
            }
            while (_nodesToLoop.Count > 0);
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

        internal Node Insert(T item, Node node, Rectangle aabb)
        {
            if (_root == null)
            {
                Bounds = new Rectangle(aabb.Center, new Point(1));
                if (node == null)
                    node = _root;
            }
            var xy = aabb.Center;
            if (aabb.Width > _maxWidthItem.Size)
                _maxWidthItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxHeightItem.Size)
                _maxHeightItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            _nodesToLoop.Push(node);
            do
            {
                node = _nodesToLoop.Pop();
                if (node._nw != null)
                {
                    if (node._ne.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._ne);
                    else if (node._se.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._se);
                    else if (node._sw.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._sw);
                    else if (node._nw.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._nw);
                }
                else if (node._items.Count >= Node.CAPACITY && node.Bounds.Width * node.Bounds.Height > 1024)
                {
                    int halfWidth = (int)MathF.Ceiling(node.Bounds.Width / 2f),
                        halfHeight = (int)MathF.Ceiling(node.Bounds.Height / 2f);
                    node._nw = Pool<Node>.Spawn();
                    node._nw._tree = this;
                    node._nw.Bounds = new Rectangle(node.Bounds.Left, node.Bounds.Top, halfWidth, halfHeight);
                    node._nw._parent = node;
                    node._sw = Pool<Node>.Spawn();
                    node._sw._tree = this;
                    int midY = node.Bounds.Top + halfHeight,
                        height = node.Bounds.Bottom - midY;
                    node._sw.Bounds = new Rectangle(node.Bounds.Left, midY, halfWidth, height);
                    node._sw._parent = node;
                    node._ne = Pool<Node>.Spawn();
                    node._ne._tree = this;
                    int midX = node.Bounds.Left + halfWidth,
                        width = node.Bounds.Right - midX;
                    node._ne.Bounds = new Rectangle(midX, node.Bounds.Top, width, halfHeight);
                    node._ne._parent = node;
                    node._se = Pool<Node>.Spawn();
                    node._se._tree = this;
                    node._se.Bounds = new Rectangle(midX, midY, width, height);
                    node._se._parent = node;
                    foreach (var i in node._items)
                    {
                        if (node._ne.Bounds.Contains(_item[i].XY))
                        {
                            node._ne._items.Add(i);
                            _item[i] = (node._ne, _item[i].XY);
                        }
                        else if (node._se.Bounds.Contains(_item[i].XY))
                        {
                            node._se._items.Add(i);
                            _item[i] = (node._se, _item[i].XY);
                        }
                        else if (node._sw.Bounds.Contains(_item[i].XY))
                        {
                            node._sw._items.Add(i);
                            _item[i] = (node._sw, _item[i].XY);
                        }
                        else if (node._nw.Bounds.Contains(_item[i].XY))
                        {
                            node._nw._items.Add(i);
                            _item[i] = (node._nw, _item[i].XY);
                        }
                    }
                    node._items.Clear();
                    if (node._ne.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._ne);
                    else if (node._se.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._se);
                    else if (node._sw.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._sw);
                    else if (node._nw.Bounds.Contains(xy))
                        _nodesToLoop.Push(node._nw);
                }
                else
                {
                    node._items.Add(item);
                    return node;
                }
            }
            while (_nodesToLoop.Count > 0);
            TryExpandTree(xy);
            return null;
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