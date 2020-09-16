using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Dcrew.Spatial {
    /// <summary>For fast and accurate spatial partitioning. Set <see cref="Bounds"/> before use</summary>
    public sealed class Quadtree<T> : IEnumerable<T> where T : class, IBounds {
        internal sealed class Node {
            internal sealed class FItem {
                internal T Item;
                internal FItem Next;
            }

            internal const int CAPACITY = 16;

            internal Rectangle Bounds;
            internal Quadtree<T> Tree;
            internal Node Parent, NE, SE, SW, NW;
            internal uint ItemCount;

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

            internal void FreeNodes() {
                if (NW == null)
                    return;
                Tree._toProcess.Push(NE);
                Tree._toProcess.Push(SE);
                Tree._toProcess.Push(SW);
                Tree._toProcess.Push(NW);
                NE = null;
                SE = null;
                SW = null;
                NW = null;
                Node node;
                do {
                    node = Tree._toProcess.Pop();
                    node.Clear();
                    Pool<Node>.Free(node);
                    if (node.NW == null)
                        continue;
                    Tree._toProcess.Push(node.NE);
                    Tree._toProcess.Push(node.SE);
                    Tree._toProcess.Push(node.SW);
                    Tree._toProcess.Push(node.NW);
                    node.NE = null;
                    node.SE = null;
                    node.SW = null;
                    node.NW = null;
                }
                while (Tree._toProcess.Count > 0);
            }
            internal void Clean() {
                var count = 0U;
                Tree._toProcess.Push(NE);
                Tree._toProcess.Push(SE);
                Tree._toProcess.Push(SW);
                Tree._toProcess.Push(NW);
                Node node;
                do {
                    node = Tree._toProcess.Pop();
                    count += node.ItemCount;
                    if (count >= CAPACITY) {
                        Tree._toProcess.Clear();
                        return;
                    }
                    if (node.NW == null)
                        continue;
                    Tree._toProcess.Push(node.NE);
                    Tree._toProcess.Push(node.SE);
                    Tree._toProcess.Push(node.SW);
                    Tree._toProcess.Push(node.NW);
                }
                while (Tree._toProcess.Count > 0);
                if (count >= CAPACITY)
                    return;
                Tree._toProcess.Push(NE);
                Tree._toProcess.Push(SE);
                Tree._toProcess.Push(SW);
                Tree._toProcess.Push(NW);
                NE = null;
                SE = null;
                SW = null;
                NW = null;
                do {
                    node = Tree._toProcess.Pop();
                    foreach (var i in node.Items) {
                        Add(i);
                        Tree._item[i] = (this, Tree._item[i].XY, Tree._item[i].AABB);
                    }
                    node.Clear();
                    Pool<Node>.Free(node);
                    if (node.NW == null)
                        continue;
                    Tree._toProcess.Push(node.NE);
                    Tree._toProcess.Push(node.SE);
                    Tree._toProcess.Push(node.SW);
                    Tree._toProcess.Push(node.NW);
                    node.NE = null;
                    node.SE = null;
                    node.SW = null;
                    node.NW = null;
                }
                while (Tree._toProcess.Count > 0);
            }
        }

        internal sealed class CleanNodes : IGameComponent, IUpdateable {
            readonly Quadtree<T> _qtree;

            public bool Enabled => true;
            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal CleanNodes(Quadtree<T> qtree) => _qtree = qtree;

            public void Initialize() { }

            public void Update(GameTime gameTime) {
                foreach (var n in _qtree._nodesToClean)
                    if (n.NW != null)
                        n.Clean();
                _qtree._nodesToClean.Clear();
                if (_qtree._updates.HasFlag(Updates.AutoCleanNodes)) {
                    _game.Components.Remove(this);
                    _qtree._updates &= ~Updates.AutoCleanNodes;
                }
            }
        }
        internal sealed class ExpandTree : IGameComponent, IUpdateable {
            readonly Quadtree<T> _qtree;

            public bool Enabled => true;

            public int UpdateOrder => int.MaxValue;

            public event EventHandler<EventArgs> EnabledChanged;
            public event EventHandler<EventArgs> UpdateOrderChanged;

            internal ExpandTree(Quadtree<T> qtree) => _qtree = qtree;

            public void Initialize() { }

            public void Update(GameTime gameTime) {
                int newLeft = Math.Min(_qtree.Bounds.Left, _qtree._extendToW),
                    newTop = Math.Min(_qtree.Bounds.Top, _qtree._extendToN),
                    newWidth = _qtree.Bounds.Right - newLeft,
                    newHeight = _qtree.Bounds.Bottom - newTop;
                _qtree.Bounds = new Rectangle(newLeft, newTop, Math.Max(newWidth, _qtree._extendToE - newLeft + 1), Math.Max(newHeight, _qtree._extendToS - newTop + 1));
                _qtree._extendToN = int.MaxValue;
                _qtree._extendToE = 0;
                _qtree._extendToS = 0;
                _qtree._extendToW = int.MaxValue;
                if (_qtree._updates.HasFlag(Updates.AutoExpandTree)) {
                    _game.Components.Remove(this);
                    _qtree._updates &= ~Updates.AutoExpandTree;
                }
            }
        }

        internal const int MIN_SIZE = 4,
            MAX_DEPTH = 8;

        /// <summary>Set the boundary rect of this tree</summary>
        public Rectangle Bounds {
            get => _root.Bounds;
            set {
                _root.FreeNodes();
                _root.Clear();
                _root.Bounds = value;
                int r = 1,
                    w = value.Width,
                    h = value.Height,
                    depth = 0;
                while (w >= MIN_SIZE && h >= MIN_SIZE/*&&depth<MAX_DEPTH*/) {
                    r += 4;
                    w /= 2;
                    h /= 2;
                    depth++;
                }
                Pool<Node>.EnsureCount(r);
                foreach (var i in _item2) {
                    var aabb = Util.Rotate(i.Bounds.XY, i.Bounds.Size, i.Bounds.Angle, i.Bounds.Origin);
                    _item[i] = (Insert(i, _root, aabb), aabb.Center, aabb);
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

        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
        public bool Contains(T item) => _item.ContainsKey(item);
        /// <summary>Returns an enumerator that iterates through the collection</summary>
        public IEnumerator<T> GetEnumerator() => _item2.GetEnumerator();
        /// <summary>Return count of all items</summary>
        public int ItemCount => _item2.Count;
        /// <summary>Return all items and their container rects</summary>
        public IEnumerable<(T Item, Rectangle Node, Rectangle ActualAABB)> Bundles {
            get {
                foreach (var i in _item)
                    yield return (i.Key, i.Value.Node.Bounds, i.Value.AABB);
            }
        }
        /// <summary>Return all node bounds in this tree</summary>
        public IEnumerable<Rectangle> Nodes {
            get {
                _toProcess.Push(_root);
                Node node;
                do {
                    node = _toProcess.Pop();
                    if (node.NW == null) {
                        yield return node.Bounds;
                        continue;
                    }
                    _toProcess.Push(node.NE);
                    _toProcess.Push(node.SE);
                    _toProcess.Push(node.SW);
                    _toProcess.Push(node.NW);
                }
                while (_toProcess.Count > 0);
            }
        }
        /// <summary>Return count of all nodes</summary>
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
        internal readonly Dictionary<T, (Node Node, Point XY, Rectangle AABB)> _item = new Dictionary<T, (Node, Point, Rectangle)>();
        internal readonly HashSet<T> _item2 = new HashSet<T>();
        internal readonly Stack<Node> _toProcess = new Stack<Node>();

        (T Item, int Size, int HalfSize) _maxWidthItem,
            _maxHeightItem;
        int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        Updates _updates;

        readonly HashSet<Node> _nodesToClean = new HashSet<Node>();
        readonly CleanNodes _cleanNodes;
        readonly ExpandTree _expandTree;

        [Flags] enum Updates : byte { ManualMode = 1, AutoCleanNodes = 2, AutoExpandTree = 4, ManualCleanNodes = 8, ManualExpandTree = 16 }

        public Quadtree() {
            _root = new Node() { Tree = this };
            _cleanNodes = new CleanNodes(this);
            _expandTree = new ExpandTree(this);
        }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
        public void Add(T item) {
            var aabb = Util.Rotate(item.Bounds.XY, item.Bounds.Size, item.Bounds.Angle, item.Bounds.Origin);
            if (aabb.Width > _maxWidthItem.Size)
                _maxWidthItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxHeightItem.Size)
                _maxHeightItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            if (_item2.Count == 0 && _root.Bounds == Rectangle.Empty)
                Bounds = new Rectangle(aabb.Center, new Point(1));
            _item2.Add(item);
            if (TryExpandTree(aabb.Center))
                return;
            _item.Add(item, (Insert(item, _root, aabb), aabb.Center, aabb));
        }
        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Update(T item) {
            var aabb = Util.Rotate(item.Bounds.XY, item.Bounds.Size, item.Bounds.Angle, item.Bounds.Origin);
            var xy = aabb.Center;
            if (aabb.Width > _maxWidthItem.Size)
                _maxWidthItem = (item, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxHeightItem.Size)
                _maxHeightItem = (item, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            if (TryExpandTree(xy))
                return;
            var c = _item[item];
            if (c.Node.Bounds.Contains(xy) || c.Node.Parent == null) {
                _item[item] = (c.Node, xy, aabb);
                return;
            }
            c.Node.Remove(item);
            _nodesToClean.Add(c.Node.Parent);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes)) {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            var node = c.Node;
            do {
                if (node.Parent == null)
                    break;
                if (node.Parent.Bounds.Contains(xy)) {
                    node = node.Parent;
                    break;
                }
                node = node.Parent;
            }
            while (true);
            _item[item] = (Insert(item, node, aabb), xy, aabb);
        }
        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
        public void Remove(T item) {
            var node = _item[item].Node;
            node.Remove(item);
            if (node.Parent != null)
                _nodesToClean.Add(node.Parent);
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes)) {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
            _item.Remove(item);
            _item2.Remove(item);
            if (ReferenceEquals(item, _maxWidthItem.Item)) {
                _maxWidthItem = (default, 0, 0);
                foreach (T i in _item.Keys) {
                    var aabb = Util.Rotate(i.Bounds.XY, i.Bounds.Size, i.Bounds.Angle, i.Bounds.Origin);
                    if (aabb.Width > _maxWidthItem.Size)
                        _maxWidthItem = (i, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
                }
            }
            if (ReferenceEquals(item, _maxHeightItem.Item)) {
                _maxHeightItem = (default, 0, 0);
                foreach (T i in _item.Keys) {
                    var aabb = Util.Rotate(i.Bounds.XY, i.Bounds.Size, i.Bounds.Angle, i.Bounds.Origin);
                    if (aabb.Height > _maxHeightItem.Size)
                        _maxHeightItem = (i, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
                }
            }
            if (_item2.Count == 0)
                _root.Bounds = Rectangle.Empty;
        }
        /// <summary>Removes all items and nodes from the tree</summary>
        public void Clear() {
            _root.FreeNodes();
            _root.Clear();
            _item.Clear();
            _item2.Clear();
            _maxWidthItem = (default, 0, 0);
            _maxHeightItem = (default, 0, 0);
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Point xy) {
            foreach (var t in Query(new Rectangle(xy.X, xy.Y, 1, 1)))
                yield return t;
            yield break;
        }
        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
        public IEnumerable<T> Query(Vector2 xy) {
            foreach (var t in Query(new Rectangle((int)MathF.Round(xy.X), (int)MathF.Round(xy.Y), 1, 1)))
                yield return t;
            yield break;
        }
        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
        /// <param name="area">Area (rectangle)</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
        /// <param name="origin">Origin (in pixels) of <paramref name="area"/></param>
        public IEnumerable<T> Query(Rectangle area, float angle = 0, Vector2 origin = default) {
            var node = _root;
            var area2 = area;
            area2.Inflate(_maxWidthItem.HalfSize, _maxHeightItem.HalfSize);
            var broad = new RotRect(area2.Location.ToVector2(), area2.Size.ToVector2(), angle, new Vector2(origin.X + _maxWidthItem.HalfSize / 2f, origin.Y + _maxHeightItem.HalfSize / 2f));
            var rect = new RotRect(area.Location.ToVector2(), area.Size.ToVector2(), angle, origin);
            do {
                if (node.NW == null) {
                    if (node.ItemCount > 0) {
                        var nodeItems = node._firstItem;
                        if (rect.Contains(node.Bounds)) {
                            do {
                                yield return nodeItems.Item;
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            }
                            while (true);
                        } else {
                            do {
                                var aabb = new RotRect(nodeItems.Item.Bounds.XY, nodeItems.Item.Bounds.Size, nodeItems.Item.Bounds.Angle, nodeItems.Item.Bounds.Origin);
                                if (rect.Intersects(aabb))
                                    yield return nodeItems.Item;
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            }
                            while (true);
                        }
                    }
                } else if (broad.Intersects(node.Bounds)) {
                    if (broad.Intersects(node.NE.Bounds))
                        _toProcess.Push(node.NE);
                    if (broad.Intersects(node.SE.Bounds))
                        _toProcess.Push(node.SE);
                    if (broad.Intersects(node.SW.Bounds))
                        _toProcess.Push(node.SW);
                    if (broad.Intersects(node.NW.Bounds))
                        _toProcess.Push(node.NW);
                }
                if (_toProcess.Count == 0)
                    break;
                node = _toProcess.Pop();
            }
            while (true);
            yield break;
        }

        /// <summary>You need to call this each frame if you don't use base.Update() in <see cref="Game.Update(GameTime)"/></summary>
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

        /// <summary>Shrinks the tree to the smallest possible size</summary>
        public void Shrink() {
            if (_item.Count == 0)
                return;
            Point min = new Point(int.MaxValue),
                max = new Point(int.MinValue);
            foreach (var i in _item2) {
                var pos = i.Bounds.XY.ToPoint();
                if (pos.X < min.X)
                    min.X = pos.X;
                if (pos.X > max.X)
                    max.X = pos.X;
                if (pos.Y < min.Y)
                    min.Y = pos.Y;
                if (pos.Y > max.Y)
                    max.Y = pos.Y;
            }
            if (Bounds.X != min.X || Bounds.Y != min.Y || Bounds.Width != max.X - min.X + 1 || Bounds.Height != max.Y - min.Y + 1)
                Bounds = new Rectangle(min.X, min.Y, max.X - min.X + 1, max.Y - min.Y + 1);
        }

        internal Node Insert(T item, Node node, Rectangle aabb) {
            var xy = aabb.Center;
            _toProcess.Push(node);
            do {
                node = _toProcess.Pop();
                if (node.NW != null) {
                    if (node.NE.Bounds.Contains(xy))
                        _toProcess.Push(node.NE);
                    else if (node.SE.Bounds.Contains(xy))
                        _toProcess.Push(node.SE);
                    else if (node.SW.Bounds.Contains(xy))
                        _toProcess.Push(node.SW);
                    else
                        _toProcess.Push(node.NW);
                } else if (node.ItemCount + 1 >= Node.CAPACITY && node.Bounds.Width >= MIN_SIZE && node.Bounds.Height >= MIN_SIZE) {
                    int halfWidth = (int)MathF.Ceiling(node.Bounds.Width / 2f),
                        halfHeight = (int)MathF.Ceiling(node.Bounds.Height / 2f);
                    node.NW = Pool<Node>.Spawn();
                    node.NW.Tree = this;
                    node.NW.Bounds = new Rectangle(node.Bounds.Left, node.Bounds.Top, halfWidth, halfHeight);
                    node.NW.Parent = node;
                    node.SW = Pool<Node>.Spawn();
                    node.SW.Tree = this;
                    int midY = node.Bounds.Top + halfHeight,
                        height = node.Bounds.Bottom - midY;
                    node.SW.Bounds = new Rectangle(node.Bounds.Left, midY, halfWidth, height);
                    node.SW.Parent = node;
                    node.NE = Pool<Node>.Spawn();
                    node.NE.Tree = this;
                    int midX = node.Bounds.Left + halfWidth,
                        width = node.Bounds.Right - midX;
                    node.NE.Bounds = new Rectangle(midX, node.Bounds.Top, width, halfHeight);
                    node.NE.Parent = node;
                    node.SE = Pool<Node>.Spawn();
                    node.SE.Tree = this;
                    node.SE.Bounds = new Rectangle(midX, midY, width, height);
                    node.SE.Parent = node;
                    var nodeItems = node._firstItem;
                    do {
                        var n = node.NW;
                        var ii = _item[nodeItems.Item];
                        if (node.NE.Bounds.Contains(ii.XY))
                            n = node.NE;
                        else if (node.SE.Bounds.Contains(ii.XY))
                            n = node.SE;
                        else if (node.SW.Bounds.Contains(ii.XY))
                            n = node.SW;
                        n.Add(nodeItems.Item);
                        _item[nodeItems.Item] = (n, ii.XY, ii.AABB);
                        if (nodeItems.Next == null)
                            break;
                        nodeItems = nodeItems.Next;
                    }
                    while (true);
                    node.Clear();
                    if (node.NE.Bounds.Contains(xy))
                        _toProcess.Push(node.NE);
                    else if (node.SE.Bounds.Contains(xy))
                        _toProcess.Push(node.SE);
                    else if (node.SW.Bounds.Contains(xy))
                        _toProcess.Push(node.SW);
                    else
                        _toProcess.Push(node.NW);
                } else {
                    _toProcess.Clear();
                    node.Add(item);
                    return node;
                }
            }
            while (_toProcess.Count > 0);
            return null;
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

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)_item2).GetEnumerator();
    }
}