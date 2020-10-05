using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Dcrew.Spatial {
    /// <summary>For fast and accurate spatial partitioning. Set <see cref="Bounds"/> before use.</summary>
    public sealed class Quadtree<T> : IEnumerable<T> where T : class, IBounds {
        internal sealed class Node {
            internal sealed class FItem {
                internal T Item;
                internal FItem Next;
            }

            internal const int CAPACITY = 16;

            internal Node Parent, NE, SE, SW, NW;
            internal int ItemCount, cX, cY;
            internal byte Depth;

            internal IEnumerable<T> Items {
                get {
                    if (ItemCount > 0) {
                        var nodeItems = _firstItem;
                        do {
                            yield return nodeItems.Item;
                            if (nodeItems.Next == null)
                                break;
                            nodeItems = nodeItems.Next;
                        }
                        while (true);
                    }
                    yield break;
                }
            }

            internal FItem _firstItem;

            internal void Add(T i) {
                if (ItemCount > 0) {
                    var nodeItems = _firstItem;
                    for (var j = 1; j < ItemCount; j++)
                        nodeItems = nodeItems.Next;
                    nodeItems.Next = Pool<FItem>.Spawn();
                    nodeItems.Next.Item = i;
                } else {
                    _firstItem = Pool<FItem>.Spawn();
                    _firstItem.Item = i;
                }
                ItemCount++;
            }
            internal void Remove(T i) {
                FItem prev = null;
                var nodeItems = _firstItem;
                while (nodeItems.Item != i) {
                    prev = nodeItems;
                    nodeItems = nodeItems.Next;
                }
                if (prev != null) {
                    prev.Next = nodeItems.Next;
                    nodeItems.Item = null;
                    nodeItems.Next = null;
                    Pool<FItem>.Free(nodeItems);
                } else {
                    var next = _firstItem.Next;
                    _firstItem.Item = null;
                    _firstItem.Next = null;
                    Pool<FItem>.Free(_firstItem);
                    _firstItem = next;
                }
                ItemCount--;
            }
            internal void Clear() {
                if (ItemCount > 0) {
                    var nodeItems = _firstItem;
                    var next = nodeItems.Next;
                    nodeItems.Item = null;
                    nodeItems.Next = null;
                    Pool<FItem>.Free(nodeItems);
                    _firstItem = null;
                    nodeItems = next;
                    while (nodeItems != null) {
                        next = nodeItems.Next;
                        nodeItems.Item = null;
                        nodeItems.Next = null;
                        Pool<FItem>.Free(nodeItems);
                        nodeItems = next;
                    }
                    ItemCount = 0;
                }
            }
        }

        internal sealed class CleanNodes : IGameComponent, IUpdateable {
            readonly Quadtree<T> _tree;

            public bool Enabled => true;
            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal CleanNodes(Quadtree<T> tree) => _tree = tree;

            public void Initialize() { }
            public void Update(GameTime gameTime) {
                foreach (var n in _tree._nodesToClean)
                    if (n.NW != null) {
                        var count = 0;
                        _tree._toProcess.Push(n.NE);
                        _tree._toProcess.Push(n.SE);
                        _tree._toProcess.Push(n.SW);
                        _tree._toProcess.Push(n.NW);
                        Node node;
                        do {
                            node = _tree._toProcess.Pop();
                            count += node.ItemCount;
                            if (count > Node.CAPACITY) {
                                _tree._toProcess.Clear();
                                break;
                            }
                            if (node.NW == null)
                                continue;
                            _tree._toProcess.Push(node.NE);
                            _tree._toProcess.Push(node.SE);
                            _tree._toProcess.Push(node.SW);
                            _tree._toProcess.Push(node.NW);
                        }
                        while (_tree._toProcess.Count > 0);
                        if (count > Node.CAPACITY)
                            continue;
                        _tree._toProcess.Push(n.NE);
                        _tree._toProcess.Push(n.SE);
                        _tree._toProcess.Push(n.SW);
                        _tree._toProcess.Push(n.NW);
                        n.NE = null;
                        n.SE = null;
                        n.SW = null;
                        n.NW = null;
                        do {
                            node = _tree._toProcess.Pop();
                            foreach (var i in node.Items) {
                                n.Add(i);
                                _tree._item[i] = (n, _tree._item[i].XY);
                            }
                            node.Clear();
                            Pool<Node>.Free(node);
                            if (n.ItemCount > Node.CAPACITY && n.Depth < 6)
                                _tree._nodesToSubdivide.Add(n);
                            if (node.NW == null)
                                continue;
                            _tree._toProcess.Push(node.NE);
                            _tree._toProcess.Push(node.SE);
                            _tree._toProcess.Push(node.SW);
                            _tree._toProcess.Push(node.NW);
                            node.NE = null;
                            node.SE = null;
                            node.SW = null;
                            node.NW = null;
                        }
                        while (_tree._toProcess.Count > 0);
                    }
                _tree._nodesToClean.Clear();
                foreach (var n in _tree._nodesToSubdivide) {
                    var depth = (byte)(n.Depth + 1);
                    int halfWidth = _tree._bounds.Width >> depth,
                        halfHeight = _tree._bounds.Height >> depth;
                    n.NW = Pool<Node>.Spawn();
                    n.NW.cX = n.cX - halfWidth;
                    n.NW.cY = n.cY - halfHeight;
                    n.NW.Depth = depth;
                    n.NW.Parent = n;
                    n.SW = Pool<Node>.Spawn();
                    n.SW.cX = n.cX - halfWidth;
                    n.SW.cY = n.cY + halfHeight;
                    n.SW.Depth = depth;
                    n.SW.Parent = n;
                    n.NE = Pool<Node>.Spawn();
                    n.NE.cX = n.cX + halfWidth;
                    n.NE.cY = n.cY - halfHeight;
                    n.NE.Depth = depth;
                    n.NE.Parent = n;
                    n.SE = Pool<Node>.Spawn();
                    n.SE.cX = n.cX + halfWidth;
                    n.SE.cY = n.cY + halfHeight;
                    n.SE.Depth = depth;
                    n.SE.Parent = n;
                    var nodeItems = n._firstItem;
                    do {
                        var ii = _tree._item[nodeItems.Item];
                        var n2 = ii.XY.X < n.cX ? ii.XY.Y < n.cY ? n.NW : n.SW : ii.XY.Y < n.cY ? n.NE : n.SE;
                        n2.Add(nodeItems.Item);
                        _tree._item[nodeItems.Item] = (n2, ii.XY);
                        if (n2.ItemCount > Node.CAPACITY && n2.Depth < 6)
                            _tree._nodesToSubdivide.Add(n2);
                        if (nodeItems.Next == null)
                            break;
                        nodeItems = nodeItems.Next;
                    } while (true);
                    n.Clear();
                }
                _tree._nodesToSubdivide.Clear();
                if (_tree._updates.HasFlag(Updates.AutoCleanNodes)) {
                    _game.Components.Remove(this);
                    _tree._updates &= ~Updates.AutoCleanNodes;
                }
            }
        }
        internal sealed class ExpandTree : IGameComponent, IUpdateable {
            readonly Quadtree<T> _tree;

            public bool Enabled => true;

            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal ExpandTree(Quadtree<T> tree) => _tree = tree;

            public void Initialize() { }
            public void Update(GameTime gameTime) {
                int newLeft = Math.Min(_tree.Bounds.Left, _tree._extendToW),
                    newTop = Math.Min(_tree.Bounds.Top, _tree._extendToN),
                    newWidth = _tree.Bounds.Right - newLeft,
                    newHeight = _tree.Bounds.Bottom - newTop;
                _tree.Bounds = new Rectangle(newLeft, newTop, Math.Max(newWidth, _tree._extendToE - newLeft + 1), Math.Max(newHeight, _tree._extendToS - newTop + 1));
                _tree._extendToN = int.MaxValue;
                _tree._extendToE = int.MinValue;
                _tree._extendToS = int.MinValue;
                _tree._extendToW = int.MaxValue;
                if (_tree._updates.HasFlag(Updates.AutoExpandTree)) {
                    _game.Components.Remove(this);
                    _tree._updates &= ~Updates.AutoExpandTree;
                }
            }
        }

        internal const int MIN_SIZE = 4,
            MAX_DEPTH = 8;

        /// <summary>Set the boundary rect of this tree.</summary>
        public Rectangle Bounds {
            get => _bounds;
            set {
                if (_root.NW != null) {
                    _toProcess.Push(_root.NE);
                    _toProcess.Push(_root.SE);
                    _toProcess.Push(_root.SW);
                    _toProcess.Push(_root.NW);
                    _root.NE = null;
                    _root.SE = null;
                    _root.SW = null;
                    _root.NW = null;
                    Node node;
                    do {
                        node = _toProcess.Pop();
                        node.Clear();
                        Pool<Node>.Free(node);
                        if (node.NW == null)
                            continue;
                        _toProcess.Push(node.NE);
                        _toProcess.Push(node.SE);
                        _toProcess.Push(node.SW);
                        _toProcess.Push(node.NW);
                        node.NE = null;
                        node.SE = null;
                        node.SW = null;
                        node.NW = null;
                    }
                    while (_toProcess.Count > 0);
                }
                _root.Clear();
                _bounds = value;
                var center = value.Center;
                _root.cX = center.X;
                _root.cY = center.Y;
                int r = 1,
                    w = value.Width,
                    h = value.Height;
                while (w >= MIN_SIZE && h >= MIN_SIZE) {
                    r += 4;
                    w /= 2;
                    h /= 2;
                }
                Pool<Node>.EnsureCount(r);
                foreach (var i in _safeItem._set) {
                    var aabb = i.Bounds.AABB;
                    var n = Insert(i, _root, aabb.Center);
                    _item[i] = (n, aabb.Center);
                    TrySubdivide(n);
                }
                _nodesToClean.Clear();
            }
        }

        static readonly Game _game;

        static Quadtree() {
            foreach (var p in typeof(Game).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static))
                if (p.GetValue(_game) is Game g)
                    _game = g;
        }

        /// <summary>Returns true if <paramref name="item"/> is in the tree.</summary>
        public bool Contains(T item) => _safeItem.Contains(item);
        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        public IEnumerator<T> GetEnumerator() => _safeItem.GetEnumerator();
        /// <summary>Return count of all items.</summary>
        public int ItemCount => _safeItem.Count;
        ///// <summary>Return all items and their container rects.</summary>
        //public IEnumerable<(T Item, Rectangle Node)> Bundles {
        //    get {
        //        foreach (var i in _item)
        //            yield return (i.Key, i.Value.Node.Bounds);
        //    }
        //}
        /// <summary>Return all node bounds in this tree.</summary>
        public IEnumerable<Rectangle> Nodes {
            get {
                _toProcess.Push(_root);
                Node n;
                do {
                    n = _toProcess.Pop();
                    int halfWidth = _bounds.Width >> n.Depth,
                        halfHeight = _bounds.Height >> n.Depth;
                    var bounds = new Rectangle(n.cX - halfWidth, n.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    yield return bounds;
                    if (n.NW != null) {
                        _toProcess.Push(n.NE);
                        _toProcess.Push(n.SE);
                        _toProcess.Push(n.SW);
                        _toProcess.Push(n.NW);
                    }
                }
                while (_toProcess.Count > 0);
            }
        }
        /// <summary>Return count of all nodes.</summary>
        public int NodeCount {
            get {
                var count = 0;
                _toProcess.Push(_root);
                Node node;
                do {
                    node = _toProcess.Pop();
                    count++;
                    if (node.NW == null)
                        continue;
                    _toProcess.Push(node.NE);
                    _toProcess.Push(node.SE);
                    _toProcess.Push(node.SW);
                    _toProcess.Push(node.NW);
                }
                while (_toProcess.Count > 0);
                return count;
            }
        }

        internal readonly Node _root;
        internal readonly Dictionary<T, (Node Node, Point XY)> _item = new Dictionary<T, (Node, Point)>();
        internal readonly SafeHashSet<T> _safeItem = new SafeHashSet<T>();
        internal readonly Stack<Node> _toProcess = new Stack<Node>();

        Rectangle _bounds;
        (T Item, int Size, int HalfSize) _maxRadiusItem;
        int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        Updates _updates;

        readonly HashSet<Node> _nodesToClean = new HashSet<Node>(),
            _nodesToSubdivide = new HashSet<Node>();
        readonly CleanNodes _cleanNodes;
        readonly ExpandTree _expandTree;

        [Flags] enum Updates : byte { ManualMode = 1, AutoCleanNodes = 2, AutoExpandTree = 4, ManualCleanNodes = 8, ManualExpandTree = 16 }

        /// <summary>Construct an empty quadtree with no bounds (will auto expand).</summary>
        public Quadtree() {
            _root = new Node();
            _cleanNodes = new CleanNodes(this);
            _expandTree = new ExpandTree(this);
        }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE.</summary>
        public void Add(T item) {
            var aabb = item.Bounds.AABB;
            if (aabb.Width > _maxRadiusItem.Size)
                _maxRadiusItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxRadiusItem.Size)
                _maxRadiusItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            _safeItem.Add(item);
            if (_safeItem.Count == 1 && _bounds.IsEmpty) {
                Bounds = new Rectangle(aabb.Center, new Point(1));
                return;
            }
            if (TryExpandTree(aabb.Center))
                return;
            var n = Insert(item, _root, aabb.Center);
            _item.Add(item, (n, aabb.Center));
            TrySubdivide(n);
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree.</summary>
        /// <returns>True if item is in the tree and has been updated, otherwise false.</returns>
        public bool Update(T item) {
            var aabb = item.Bounds.AABB;
            var xy = aabb.Center;
            if (aabb.Width > _maxRadiusItem.Size)
                _maxRadiusItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxRadiusItem.Size)
                _maxRadiusItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            if (_item.TryGetValue(item, out var v)) {
                if (TryExpandTree(xy))
                    return true;
                int halfWidth = _bounds.Width >> v.Node.Depth,
                    halfHeight = _bounds.Height >> v.Node.Depth;
                var bounds = new Rectangle(v.Node.cX - halfWidth, v.Node.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                if (bounds.Contains(xy)) {
                    //if (v.Node.Parent == null || (Math.Sign(xy.X - v.Node.Parent.cX) == Math.Sign(v.XY.X - v.Node.Parent.cX) && Math.Sign(xy.Y - v.Node.Parent.cY) == Math.Sign(v.XY.Y - v.Node.Parent.cY))) {
                    _item[item] = (v.Node, xy);
                    return true;
                }
                v.Node.Remove(item);
                if (v.Node.Parent != null)
                    _nodesToClean.Add(v.Node.Parent);
                if (_updates.HasFlag(Updates.ManualMode))
                    _updates |= Updates.ManualCleanNodes;
                else if (!_updates.HasFlag(Updates.AutoCleanNodes)) {
                    _game.Components.Add(_cleanNodes);
                    _updates |= Updates.AutoCleanNodes;
                }
                var n = v.Node;
                do {
                    if (n.Parent == null)
                        break;
                    halfWidth = _bounds.Width >> n.Depth;
                    halfHeight = _bounds.Height >> n.Depth;
                    bounds = new Rectangle(n.cX - halfWidth, n.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (bounds.Contains(xy)) {
                        //if (Math.Sign(xy.X - n.Parent.cX) == Math.Sign(v.XY.X - n.Parent.cX) && Math.Sign(xy.Y - n.Parent.cY) == Math.Sign(v.XY.Y - n.Parent.cY)) {
                        n = n.Parent;
                        break;
                    }
                    n = n.Parent;
                }
                while (true);
                var n2 = Insert(item, n, xy);
                _item[item] = (n2, xy);
                TrySubdivide(n2);
                return true;
            }
            return false;
        }
        /// <summary>Removes <paramref name="item"/> from the tree.</summary>
        /// <returns>True if item was in the tree and was removed, otherwise false.</returns>
        public bool Remove(T item) {
            if (_item.TryGetValue(item, out var v)) {
                v.Node.Remove(item);
                if (v.Node.Parent != null)
                    _nodesToClean.Add(v.Node.Parent);
                if (_updates.HasFlag(Updates.ManualMode))
                    _updates |= Updates.ManualCleanNodes;
                else if (!_updates.HasFlag(Updates.AutoCleanNodes)) {
                    _game.Components.Add(_cleanNodes);
                    _updates |= Updates.AutoCleanNodes;
                }
                _item.Remove(item);
                _safeItem.Remove(item);
                if (ReferenceEquals(item, _maxRadiusItem.Item)) {
                    _maxRadiusItem = (default, 0, 0);
                    foreach (T i in _safeItem) {
                        var aabb = i.Bounds.AABB;
                        if (aabb.Width > _maxRadiusItem.Size)
                            _maxRadiusItem = (i, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
                        if (aabb.Height > _maxRadiusItem.Size)
                            _maxRadiusItem = (i, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
                    }
                }
                if (_safeItem.Count == 0)
                    _bounds = Rectangle.Empty;
                return true;
            }
            return false;
        }
        /// <summary>Removes all items and nodes from the tree.</summary>
        public void Clear() {
            //_root.FreeNodes();
            _root.Clear();
            _item.Clear();
            _safeItem.Clear();
            _maxRadiusItem = (default, 0, 0);
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/>.</summary>
        public IEnumerable<T> Query(Point xy) => Query(new RotRect(xy.X, xy.Y, 1, 1));
        /// <summary>Query and return the items intersecting <paramref name="xy"/>.</summary>
        public IEnumerable<T> Query(Vector2 xy) => Query(new RotRect((int)MathF.Round(xy.X), (int)MathF.Round(xy.Y), 1, 1));
        /// <summary>Query and return the items intersecting <paramref name="area"/>.</summary>
        /// <param name="area">Area (rectangle).</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/>.</param>
        /// <param name="origin">Origin (in pixels) of <paramref name="area"/>.</param>
        public IEnumerable<T> Query(Rectangle area, float angle = 0, Vector2 origin = default) => Query(new RotRect(area.Location.ToVector2(), area.Size.ToVector2(), angle, origin));
        /// <summary>Query and return the items intersecting <paramref name="rect"/>.</summary>
        public IEnumerable<T> Query(RotRect rect) {
            var n = _root;
            var broad = rect;
            broad.Inflate(_maxRadiusItem.HalfSize, _maxRadiusItem.HalfSize);
            do {
                int halfWidth = _bounds.Width >> n.Depth,
                    halfHeight = _bounds.Height >> n.Depth;
                var bounds = new Rectangle(n.cX - halfWidth, n.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                if (n.NW == null) {
                    if (n.ItemCount > 0) {
                        var nodeItems = n._firstItem;
                        if (rect.Contains(bounds)) {
                            do {
                                yield return nodeItems.Item;
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            }
                            while (true);
                        } else {
                            do {
                                if (rect.Intersects(nodeItems.Item.Bounds))
                                    yield return nodeItems.Item;
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            }
                            while (true);
                        }
                    }
                } else if (broad.Intersects(bounds)) {
                    halfWidth = _bounds.Width >> (n.Depth + 1);
                    halfHeight = _bounds.Height >> (n.Depth + 1);
                    bounds = new Rectangle(n.NE.cX - halfWidth, n.NE.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(n.NE);
                    bounds = new Rectangle(n.SE.cX - halfWidth, n.SE.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(n.SE);
                    bounds = new Rectangle(n.SW.cX - halfWidth, n.SW.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(n.SW);
                    bounds = new Rectangle(n.NW.cX - halfWidth, n.NW.cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(n.NW);
                }
                if (_toProcess.Count == 0)
                    break;
                n = _toProcess.Pop();
            }
            while (true);
            yield break;
        }

        /// <summary>You need to call this each frame if you don't use base.Update() in <see cref="Game.Update(GameTime)"/>.</summary>
        public void Update() {
            if (_updates.HasFlag(Updates.AutoCleanNodes)) {
                _game.Components.Remove(_cleanNodes);
                _cleanNodes.Update(null);
            } else if (_updates.HasFlag(Updates.ManualCleanNodes))
                _cleanNodes.Update(null);
            if (_updates.HasFlag(Updates.AutoExpandTree)) {
                _game.Components.Remove(_expandTree);
                _expandTree.Update(null);
            } else if (_updates.HasFlag(Updates.ManualExpandTree))
                _expandTree.Update(null);
            _updates = Updates.ManualMode;
        }

        /// <summary>Shrinks the tree to the smallest possible size.</summary>
        public void Shrink() {
            if (_item.Count == 0)
                return;
            Point min = new Point(int.MaxValue),
                max = new Point(int.MinValue);
            foreach (var i in _safeItem) {
                var xy = i.Bounds.AABB.Center;
                if (xy.X < min.X)
                    min.X = xy.X;
                if (xy.X > max.X)
                    max.X = xy.X;
                if (xy.Y < min.Y)
                    min.Y = xy.Y;
                if (xy.Y > max.Y)
                    max.Y = xy.Y;
            }
            if (Bounds.X != min.X || Bounds.Y != min.Y || Bounds.Width != max.X - min.X + 1 || Bounds.Height != max.Y - min.Y + 1)
                Bounds = new Rectangle(min.X, min.Y, max.X - min.X + 1, max.Y - min.Y + 1);
        }

        Node Insert(T item, Node n, Point xy) {
            do {
                if (n.NW != null) {
                    n = xy.X < n.cX ? xy.Y < n.cY ? n.NW : n.SW : xy.Y < n.cY ? n.NE : n.SE;
                    continue;
                }
                n.Add(item);
                return n;
            } while (true);
        }

        void TrySubdivide(Node n) {
            if (n.ItemCount > Node.CAPACITY && n.Depth < 6) {
                var depth = (byte)(n.Depth + 1);
                int halfWidth = _bounds.Width >> depth,
                    halfHeight = _bounds.Height >> depth;
                n.NW = Pool<Node>.Spawn();
                n.NW.cX = n.cX - halfWidth;
                n.NW.cY = n.cY - halfHeight;
                n.NW.Depth = depth;
                n.NW.Parent = n;
                n.SW = Pool<Node>.Spawn();
                n.SW.cX = n.cX - halfWidth;
                n.SW.cY = n.cY + halfHeight;
                n.SW.Depth = depth;
                n.SW.Parent = n;
                n.NE = Pool<Node>.Spawn();
                n.NE.cX = n.cX + halfWidth;
                n.NE.cY = n.cY - halfHeight;
                n.NE.Depth = depth;
                n.NE.Parent = n;
                n.SE = Pool<Node>.Spawn();
                n.SE.cX = n.cX + halfWidth;
                n.SE.cY = n.cY + halfHeight;
                n.SE.Depth = depth;
                n.SE.Parent = n;
                var nItems = n._firstItem;
                do {
                    var ii = _item[nItems.Item];
                    var n2 = ii.XY.X < n.cX ? ii.XY.Y < n.cY ? n.NW : n.SW : ii.XY.Y < n.cY ? n.NE : n.SE;
                    n2.Add(nItems.Item);
                    _item[nItems.Item] = (n2, ii.XY);
                    if (nItems.Next == null)
                        break;
                    nItems = nItems.Next;
                } while (true);
                n.Clear();
            }
        }

        bool TryExpandTree(Point xy) {
            if (Bounds.Left > xy.X || Bounds.Top > xy.Y || Bounds.Right < xy.X + 1 || Bounds.Bottom < xy.Y + 1) {
                if (xy.Y < _extendToN)
                    _extendToN = xy.Y;
                if (xy.X > _extendToE)
                    _extendToE = xy.X;
                if (xy.Y > _extendToS)
                    _extendToS = xy.Y;
                if (xy.X < _extendToW)
                    _extendToW = xy.X;
                if (_updates.HasFlag(Updates.ManualMode))
                    _updates |= Updates.ManualExpandTree;
                else if (!_updates.HasFlag(Updates.AutoExpandTree)) {
                    _game.Components.Add(_expandTree);
                    _updates |= Updates.AutoExpandTree;
                }
                return true;
            }
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => _safeItem.GetEnumerator();
    }
}