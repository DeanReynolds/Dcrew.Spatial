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
            internal Rectangle Bounds;

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

        public interface INodes {
            public HashSet<Rectangle> Nodes => _nodes;

            internal HashSet<Rectangle> _nodes { get; set; }
        }

        public class ItemSet : HashSet<T>, INodes, IDisposable {
            public HashSet<Rectangle> Nodes => _nodes;

            HashSet<Rectangle> _nodes { get; set; }
            HashSet<Rectangle> INodes._nodes {
                get => _nodes;
                set => _nodes = value;
            }

            public void Dispose() {
                Clear();
                _nodes.Clear();
                Pool<HashSet<Rectangle>>.Free(_nodes);
                Pool<ItemSet>.Free(this);
            }
        }

        public class ItemList : List<T>, INodes, IDisposable {
            public HashSet<Rectangle> Nodes => _nodes;

            HashSet<Rectangle> _nodes { get; set; }
            HashSet<Rectangle> INodes._nodes {
                get => _nodes;
                set => _nodes = value;
            }

            public void Dispose() {
                Clear();
                _nodes.Clear();
                Pool<HashSet<Rectangle>>.Free(_nodes);
                Pool<ItemList>.Free(this);
            }
        }

        internal sealed class CleanNodes : GameComponent {
            readonly Quadtree<T> _tree;

            internal CleanNodes(Game game, Quadtree<T> tree) : base(game) => _tree = tree;

            public override void Update(GameTime gameTime) {
                Node n3;
                foreach (var n2 in _tree._nodesToGrow) {
                    n3 = n2;
                start:;
                    var bounds = Rectangle.Empty;
                    if (n3.NW != null) {
                        if (n3.NW.Bounds.Width != 0)
                            bounds = n3.NW.Bounds;
                        if (n3.NE.Bounds.Width != 0)
                            bounds = bounds.Width == 0 ? n3.NE.Bounds : Rectangle.Union(bounds, n3.NE.Bounds);
                        if (n3.SE.Bounds.Width != 0)
                            bounds = bounds.Width == 0 ? n3.SE.Bounds : Rectangle.Union(bounds, n3.SE.Bounds);
                        if (n3.SW.Bounds.Width != 0)
                            bounds = bounds.Width == 0 ? n3.SW.Bounds : Rectangle.Union(bounds, n3.SW.Bounds);
                        if (bounds != n3.Bounds) {
                            n3.Bounds = bounds;
                            if (n3.Parent != null) {
                                n3 = n3.Parent;
                                goto start;
                            }
                            continue;
                        }
                    }
                    if (n3.ItemCount > 0) {
                        int l = int.MaxValue,
                            r = int.MinValue,
                            t = int.MaxValue,
                            b = int.MinValue;
                        var nodeItems = n3._firstItem;
                        do {
                            var bs = nodeItems.Item.Bounds.AABB;
                            l = l <= bs.Left ? l : bs.Left;
                            r = r >= bs.Right ? r : bs.Right;
                            t = t <= bs.Top ? t : bs.Top;
                            b = b >= bs.Bottom ? b : bs.Bottom;
                            if (nodeItems.Next == null)
                                break;
                            nodeItems = nodeItems.Next;
                        } while (true);
                        bounds = new Rectangle(l, t, r - l, b - t);
                    }
                    if (n3.Bounds != bounds) {
                        n3.Bounds = bounds;
                        if (n3.Parent != null) {
                            n3 = n3.Parent;
                            goto start;
                        }
                    }
                }
                foreach (var n in _tree._nodesToSubdivide)
                    if (_tree.TrySubdivide(n))
                        _tree._nodesToClean.Remove(n);
                foreach (var n in _tree._nodesToClean)
                    if (n.NW != null) {
                        var count = 0;
                        _tree._toProcess.Push(n.NE);
                        _tree._toProcess.Push(n.SE);
                        _tree._toProcess.Push(n.SW);
                        _tree._toProcess.Push(n.NW);
                        Node sn;
                        do {
                            sn = _tree._toProcess.Pop();
                            count += sn.ItemCount;
                            if (count > Node.CAPACITY) {
                                _tree._toProcess.Clear();
                                break;
                            }
                            if (sn.NW == null)
                                continue;
                            _tree._toProcess.Push(sn.NE);
                            _tree._toProcess.Push(sn.SE);
                            _tree._toProcess.Push(sn.SW);
                            _tree._toProcess.Push(sn.NW);
                        } while (_tree._toProcess.Count > 0);
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
                            sn = _tree._toProcess.Pop();
                            if (sn.ItemCount > 0) {
                                var nodeItems = sn._firstItem;
                                do {
                                    n.Add(nodeItems.Item);
                                    _tree._item[nodeItems.Item] = (n, _tree._item[nodeItems.Item].XY);
                                    if (nodeItems.Next == null)
                                        break;
                                    nodeItems = nodeItems.Next;
                                } while (true);
                                sn.Clear();
                                sn.Bounds = Rectangle.Empty;
                            }
                            Pool<Node>.Free(sn);
                            if (sn.NW == null)
                                continue;
                            _tree._toProcess.Push(sn.NE);
                            _tree._toProcess.Push(sn.SE);
                            _tree._toProcess.Push(sn.SW);
                            _tree._toProcess.Push(sn.NW);
                            sn.NE = null;
                            sn.SE = null;
                            sn.SW = null;
                            sn.NW = null;
                        } while (_tree._toProcess.Count > 0);
                    }
                _tree._nodesToGrow.Clear();
                _tree._nodesToSubdivide.Clear();
                _tree._nodesToClean.Clear();
                if (_tree._updates.HasFlag(Updates.AutoCleanNodes)) {
                    _game.Components.Remove(this);
                    _tree._updates &= ~Updates.AutoCleanNodes;
                }
            }
        }
        internal sealed class ExpandTree : GameComponent {
            readonly Quadtree<T> _tree;

            internal ExpandTree(Game game, Quadtree<T> tree) : base(game) => _tree = tree;

            public override void Update(GameTime gameTime) {
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
                    Node n;
                    do {
                        n = _toProcess.Pop();
                        n.Clear();
                        n.Bounds = Rectangle.Empty;
                        Pool<Node>.Free(n);
                        if (n.NW == null)
                            continue;
                        _toProcess.Push(n.NE);
                        _toProcess.Push(n.SE);
                        _toProcess.Push(n.SW);
                        _toProcess.Push(n.NW);
                        n.NE = null;
                        n.SE = null;
                        n.SW = null;
                        n.NW = null;
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
                _nodesToClean.Clear();
                _nodesToSubdivide.Clear();
                _nodesToGrow.Clear();
                foreach (var i in _safeItem._set) {
                    var aabb = i.Bounds.AABB;
                    var n = Insert(i, _root, aabb.Center);
                    _item[i] = (n, aabb.Center);
                    if (n.ItemCount > Node.CAPACITY && n.Depth < 6)
                        _nodesToSubdivide.Add(n);
                }
                QueueClean();
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
        /// <summary>Return all items and their container rects.</summary>
        public IEnumerable<(T Item, Rectangle Node)> Bundles {
            get {
                foreach (var i in _item)
                    yield return (i.Key, i.Value.Node.Bounds);
            }
        }

        internal readonly Node _root;
        internal readonly Dictionary<T, (Node Node, Point XY)> _item = new Dictionary<T, (Node, Point)>();
        internal readonly SafeHashSet<T> _safeItem = new SafeHashSet<T>();
        internal readonly Stack<Node> _toProcess = new Stack<Node>();

        Rectangle _bounds;
        int _extendToN = int.MaxValue,
            _extendToE = int.MinValue,
            _extendToS = int.MinValue,
            _extendToW = int.MaxValue;
        Updates _updates;

        readonly HashSet<Node> _nodesToClean = new HashSet<Node>(),
            _nodesToSubdivide = new HashSet<Node>(),
            _nodesToGrow = new HashSet<Node>();
        readonly CleanNodes _cleanNodes;
        readonly ExpandTree _expandTree;

        [Flags] enum Updates : byte { ManualMode = 1, AutoCleanNodes = 2, AutoExpandTree = 4, ManualCleanNodes = 8, ManualExpandTree = 16 }

        /// <summary>Construct an empty quadtree with no bounds (will auto expand).</summary>
        public Quadtree() {
            _root = new Node();
            _cleanNodes = new CleanNodes(_game, this);
            _expandTree = new ExpandTree(_game, this);
        }

        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE.</summary>
        public void Add(T item) {
            var xy = item.Bounds.Center.ToPoint();
            _safeItem.Add(item);
            if (_safeItem.Count == 1 && _bounds.IsEmpty) {
                Bounds = new Rectangle(xy, new Point(1));
                return;
            }
            if (TryExpandTree(xy))
                return;
            var n = Insert(item, _root, xy);
            _item.Add(item, (n, xy));
            if (n.ItemCount > Node.CAPACITY && n.Depth < 6)
                _nodesToSubdivide.Add(n);
            _nodesToGrow.Add(n);
            QueueClean();
        }
        /// <summary>Updates the given item from the tree if it exists.</summary>
        /// <returns>True if item is in the tree and has been updated, otherwise false.</returns>
        public bool Update(T item) {
            var xy = item.Bounds.Center.ToPoint();
            if (TryExpandTree(xy))
                return true;
            if (!_item.TryGetValue(item, out var v))
                return false;
            if (v.Node == _root) {
                _item[item] = (v.Node, xy);
                if (_root.ItemCount > Node.CAPACITY && _root.Depth < 6)
                    _nodesToSubdivide.Add(_root);
                _nodesToGrow.Add(_root);
                QueueClean();
                return true;
            }
            int halfWidth = _bounds.Width >> v.Node.Depth,
                halfHeight = _bounds.Height >> v.Node.Depth;
            var bounds = new Rectangle(v.Node.cX - halfWidth, v.Node.cY - halfHeight, halfWidth << 1, halfHeight << 1);
            if (bounds.Contains(xy)) {
                //if (Math.Sign(xy.X - v.Node.Parent.cX) == Math.Sign(v.XY.X - v.Node.Parent.cX) && Math.Sign(xy.Y - v.Node.Parent.cY) == Math.Sign(v.XY.Y - v.Node.Parent.cY)) {
                _item[item] = (v.Node, xy);
                _nodesToGrow.Add(v.Node);
                QueueClean();
                return true;
            }
            v.Node.Remove(item);
            _nodesToGrow.Add(v.Node);
            _nodesToClean.Add(v.Node.Parent);
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
            if (n2.ItemCount > Node.CAPACITY && n2.Depth < 6)
                _nodesToSubdivide.Add(n2);
            _nodesToGrow.Add(n2);
            QueueClean();
            return true;
        }
        /// <summary>Removes the given item from the tree if it exists.</summary>
        /// <returns>True if item was in the tree and was removed, otherwise false.</returns>
        public bool Remove(T item) {
            if (!_item.TryGetValue(item, out var v))
                return false;
            v.Node.Remove(item);
            if (v.Node.Parent != null)
                _nodesToClean.Add(v.Node.Parent);
            QueueClean();
            _item.Remove(item);
            _safeItem.Remove(item);
            if (_item.Count == 0)
                _bounds = Rectangle.Empty;
            return true;
        }
        /// <summary>Removes all items and nodes from the tree.</summary>
        public void Clear() {
            if (_root.NW != null) {
                _toProcess.Push(_root.NE);
                _toProcess.Push(_root.SE);
                _toProcess.Push(_root.SW);
                _toProcess.Push(_root.NW);
                _root.NE = null;
                _root.SE = null;
                _root.SW = null;
                _root.NW = null;
                Node n;
                do {
                    n = _toProcess.Pop();
                    n.Clear();
                    n.Bounds = Rectangle.Empty;
                    Pool<Node>.Free(n);
                    if (n.NW == null)
                        continue;
                    _toProcess.Push(n.NE);
                    _toProcess.Push(n.SE);
                    _toProcess.Push(n.SW);
                    _toProcess.Push(n.NW);
                    n.NE = null;
                    n.SE = null;
                    n.SW = null;
                    n.NW = null;
                } while (_toProcess.Count > 0);
            }
            _root.Clear();
            _item.Clear();
            _safeItem.Clear();
            _bounds = Rectangle.Empty;
        }

        /// <summary>Find all items intersecting the given position.</summary>
        /// <param name="xy">Position.</param>
        /// <returns>Items that overlap the given position.</returns>
        public ItemSet QueryPoint(Point xy) => Query<ItemSet>(new RotRect(new Vector2(xy.X, xy.Y), Vector2.One));
        /// <summary>Find all items intersecting the given position.</summary>
        /// <param name="xy">Position.</param>
        /// <returns>Items that overlap the given position.</returns>
        public ItemSet QueryPoint(Vector2 xy) => Query<ItemSet>(new RotRect(new Vector2((int)MathF.Round(xy.X), (int)MathF.Round(xy.Y)), Vector2.One));
        /// <summary>Find all items inside of the given rectangle.</summary>
        /// <param name="area">Area.</param>
        /// <param name="rotation">Rotation (in radians) of the rectangle.</param>
        /// <param name="origin">Origin of rectangle.</param>
        /// <returns>Items that overlap the given rectangle.</returns>
        public ItemSet QueryRect(Rectangle area, float rotation = 0, Vector2 origin = default) => Query<ItemSet>(new RotRect(area.Location.ToVector2(), area.Size.ToVector2(), rotation, origin));
        /// <summary>Find all items inside of the given rectangle.</summary>
        /// <param name="xy">Position of the rectangle.</param>
        /// <param name="size">Size of the rectangle.</param>
        /// <param name="rotation">Rotation (in radians) of the rectangle.</param>
        /// <param name="origin">Origin of the rectangle.</param>
        /// <returns>Items that overlap the given rectangle.</returns>
        public ItemSet QueryRect(Vector2 xy, Vector2 size, float rotation = default, Vector2 origin = default) => Query<ItemSet>(new RotRect(xy, size, rotation, origin));
        /// <summary>Find all items inside of the given rectangle.</summary>
        /// <returns>Items that overlap the given rectangle.</returns>
        public ItemSet QueryRect(RotRect value) => Query<ItemSet>(value);
        /// <summary>Find all items within the radius of the given position.</summary>
        /// <param name="xy">Position.</param>
        /// <param name="radius">Radius.</param>
        /// <returns>Items that are within <paramref name="radius"/> of <paramref name="xy"/>.</returns>
        public ItemSet QueryRadius(Vector2 xy, float radius) => Query<ItemSet>(xy, radius);
        /// <summary>Find all items intersecting the given line, ordered closest to <paramref name="start"/> first.</summary>
        /// <param name="start">Start point.</param>
        /// <param name="end">End point.</param>
        /// <param name="thickness">Thickness (width) of line.</param>
        /// <returns>Items that intersect the given line.</returns>
        public ItemList LineCast(Vector2 start, Vector2 end, float thickness = 1) {
            var items = Query<ItemList>(new RotRect(start, new Vector2(MathF.Sqrt(Vector2.DistanceSquared(start, end)), thickness), MathF.Atan2(end.Y - start.Y, end.X - start.X), new Vector2(0, thickness / 2)));
            items.Sort((T x, T y) => Vector2.DistanceSquared(start, x.Bounds.XY) < Vector2.DistanceSquared(start, x.Bounds.XY) ? -1 : 0);
            return items;
        }
        /// <summary>Find all items intersecting the given ray, ordered closest to <paramref name="start"/> first.</summary>
        /// <param name="start">Start point.</param>
        /// <param name="direction">Direction of the ray.</param>
        /// <param name="thickness">Thickness (width) of ray.</param>
        /// <returns>Items that intersect the given ray.</returns>
        public ItemList Raycast(Vector2 start, Vector2 direction, float thickness = 1) {
            var end = start + (direction * float.MaxValue);
            var items = Query<ItemList>(new RotRect(start, new Vector2(MathF.Sqrt(Vector2.DistanceSquared(start, end)), thickness), MathF.Atan2(direction.Y, direction.X), new Vector2(0, thickness / 2)));
            items.Sort((T x, T y) => Vector2.DistanceSquared(start, x.Bounds.XY) < Vector2.DistanceSquared(start, x.Bounds.XY) ? -1 : 0);
            return items;
        }
        /// <summary>Find all items intersecting the given ray, ordered closest to <paramref name="start"/> first.</summary>
        /// <param name="start">Start point.</param>
        /// <param name="rotation">Rotation (in radians).</param>
        /// <param name="thickness">Thickness (width) of ray.</param>
        /// <returns>Items that intersect the given ray.</returns>
        public ItemList Raycast(Vector2 start, float rotation, float thickness = 1) {
            var end = new Vector2(start.X + (MathF.Cos(rotation) * float.MaxValue), start.Y + (MathF.Sin(rotation) * float.MaxValue));
            var items = Query<ItemList>(new RotRect(start, new Vector2(MathF.Sqrt(Vector2.DistanceSquared(start, end)), thickness), rotation, new Vector2(0, thickness / 2)));
            items.Sort((T x, T y) => Vector2.DistanceSquared(start, x.Bounds.XY) < Vector2.DistanceSquared(start, x.Bounds.XY) ? -1 : 0);
            return items;
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

        bool TrySubdivide(Node n) {
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
                return true;
            }
            return false;
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

        void QueueClean() {
            if (_updates.HasFlag(Updates.ManualMode))
                _updates |= Updates.ManualCleanNodes;
            else if (!_updates.HasFlag(Updates.AutoCleanNodes)) {
                _game.Components.Add(_cleanNodes);
                _updates |= Updates.AutoCleanNodes;
            }
        }

        TCollection Query<TCollection>(RotRect value) where TCollection : class, ICollection<T>, INodes, new() {
            var items = Pool<TCollection>.Spawn();
            items._nodes = Pool<HashSet<Rectangle>>.Spawn();
            var n = _root;
            do {
                if (n.NW == null) {
                    if (n.ItemCount > 0) {
                        items._nodes.Add(n.Bounds);
                        var nodeItems = n._firstItem;
                        if (value.Contains(n.Bounds))
                            do {
                                items.Add(nodeItems.Item);
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            } while (true);
                        else
                            do {
                                if (value.Intersects(nodeItems.Item.Bounds))
                                    items.Add(nodeItems.Item);
                                if (nodeItems.Next == null)
                                    break;
                                nodeItems = nodeItems.Next;
                            } while (true);
                    }
                } else {
                    if (value.Intersects(n.NE.Bounds))
                        _toProcess.Push(n.NE);
                    if (value.Intersects(n.SE.Bounds))
                        _toProcess.Push(n.SE);
                    if (value.Intersects(n.SW.Bounds))
                        _toProcess.Push(n.SW);
                    if (value.Intersects(n.NW.Bounds))
                        _toProcess.Push(n.NW);
                }
                if (_toProcess.Count == 0)
                    break;
                n = _toProcess.Pop();
            } while (true);
            return items;
        }
        TCollection Query<TCollection>(Vector2 xy, float radius) where TCollection : class, ICollection<T>, INodes, new() {
            var rSqr = radius * radius;
            var items = Pool<TCollection>.Spawn();
            items._nodes = Pool<HashSet<Rectangle>>.Spawn();
            var n = _root;
            do {
                items._nodes.Add(n.Bounds);
                if (n.NW == null) {
                    if (n.ItemCount > 0) {
                        var nodeItems = n._firstItem;
                        do {
                            if (Vector2.DistanceSquared(xy, nodeItems.Item.Bounds.ClosestPoint(xy)) <= rSqr)
                                items.Add(nodeItems.Item);
                            if (nodeItems.Next == null)
                                break;
                            nodeItems = nodeItems.Next;
                        } while (true);
                    }
                } else {
                    if (Vector2.DistanceSquared(xy, new RotRect(n.NE.Bounds).ClosestPoint(xy)) <= rSqr)
                        _toProcess.Push(n.NE);
                    if (Vector2.DistanceSquared(xy, new RotRect(n.SE.Bounds).ClosestPoint(xy)) <= rSqr)
                        _toProcess.Push(n.SE);
                    if (Vector2.DistanceSquared(xy, new RotRect(n.SW.Bounds).ClosestPoint(xy)) <= rSqr)
                        _toProcess.Push(n.SW);
                    if (Vector2.DistanceSquared(xy, new RotRect(n.NW.Bounds).ClosestPoint(xy)) <= rSqr)
                        _toProcess.Push(n.NW);
                }
                if (_toProcess.Count == 0)
                    break;
                n = _toProcess.Pop();
            } while (true);
            return items;
        }

        IEnumerator IEnumerable.GetEnumerator() => _safeItem.GetEnumerator();
    }
}