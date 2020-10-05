using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Dcrew.Spatial {
    public struct StackQuadtree {
        struct Node {
            public const int ITEM_CAPACITY = 16;

            internal int _parent, _firstChild, _itemCount, _cX, _cY;
            internal byte _depth;
        }

        struct Item {
            internal int _id, _next, _node;
            internal RotRect _bounds;
            internal Point _xy;
        }

        Rectangle _bounds;
        int _nextNodeId;
        (int ItemId, int Size, int HalfSize) _maxRadiusItem;

        readonly Item[] _items;
        readonly Node[] _nodes;
        readonly Stack<int> _toProcess;

        public StackQuadtree(Rectangle bounds, int itemCapacity) {
            _bounds = bounds;
            var c = bounds.Center;
            _nodes = new Node[2200];
            _nodes[0] = new Node { _cX = c.X, _cY = c.Y, _parent = -1 };
            _nextNodeId = 1;
            _items = new Item[itemCapacity];
            for (var i = 0; i < _items.Length; i++)
                _items[i] = new Item { _id = i, _next = -1, _node = -1 };
            _toProcess = new Stack<int>();
            _maxRadiusItem = (-1, 0, 0);
        }

        public void Insert(int id, Vector2 xy, Vector2 size, float angle = 0, Vector2 origin = default) {
            var bounds = new RotRect(xy, size, angle, origin);
            var aabb = bounds.AABB;
            if (aabb.Width > _maxRadiusItem.Size)
                _maxRadiusItem = (id, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxRadiusItem.Size)
                _maxRadiusItem = (id, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            var center = aabb.Center;
            TrySubdivide(Insert(id, 0, bounds, center));
        }
        /// <summary>Updates position of <paramref name="id"/> in the tree.</summary>
        /// <returns>True if <paramref name="id"/> has been updated, otherwise false.</returns>
        public bool Update(int id, Vector2 xy, Vector2 size, float angle = 0, Vector2 origin = default) {
            var bounds = new RotRect(xy, size, angle, origin);
            var aabb = bounds.AABB;
            if (aabb.Width > _maxRadiusItem.Size)
                _maxRadiusItem = (id, aabb.Width, (int)MathF.Ceiling(aabb.Width / 2f));
            if (aabb.Height > _maxRadiusItem.Size)
                _maxRadiusItem = (id, aabb.Height, (int)MathF.Ceiling(aabb.Height / 2f));
            var center = aabb.Center;
            var item = _items[id];
            var node = _nodes[item._node];
            int halfWidth = _bounds.Width >> node._depth,
                halfHeight = _bounds.Height >> node._depth;
            var bounds2 = new Rectangle(node._cX - halfWidth, node._cY - halfHeight, halfWidth << 1, halfHeight << 1);
            if (bounds2.Contains(center) || node._parent == -1) {
                _items[id]._bounds = bounds;
                _items[id]._xy = center;
                return true;
            }
            if (node._firstChild == id)
                _nodes[item._node]._firstChild = item._next;
            else {
                var prevItem = node._firstChild;
                var curItem = _items[prevItem]._next;
                for (var i = 0; i < node._itemCount; i++) {
                    if (curItem == id) {
                        _items[prevItem]._next = _items[curItem]._next;
                        break;
                    }
                    prevItem = curItem;
                    curItem = _items[curItem]._next;
                }
            }
            _nodes[item._node]._itemCount--;
            var nodeId = item._node;
            do {
                if (node._parent < 0)
                    break;
                var parentNode = _nodes[node._parent];
                halfWidth = _bounds.Width >> parentNode._depth;
                halfHeight = _bounds.Height >> parentNode._depth;
                bounds2 = new Rectangle(parentNode._cX - halfWidth, parentNode._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                if (bounds2.Contains(center)) {
                    nodeId = node._parent;
                    break;
                }
                nodeId = node._parent;
                node = _nodes[nodeId];
            } while (true);
            TrySubdivide(Insert(id, nodeId, bounds, center));
            return true;
        }

        /// <summary>Query and return the items intersecting <paramref name="xy"/>.</summary>
        public IEnumerable<int> Query(Point xy) => Query(new RotRect(xy.X, xy.Y, 1, 1));
        /// <summary>Query and return the items intersecting <paramref name="xy"/>.</summary>
        public IEnumerable<int> Query(Vector2 xy) => Query(new RotRect((int)MathF.Round(xy.X), (int)MathF.Round(xy.Y), 1, 1));
        /// <summary>Query and return the items intersecting <paramref name="area"/>.</summary>
        /// <param name="area">Area.</param>
        /// <param name="angle">Rotation (in radians) of <paramref name="area"/>.</param>
        /// <param name="origin">Origin of <paramref name="area"/>.</param>
        public IEnumerable<int> Query(Rectangle area, float angle = 0, Vector2 origin = default) => Query(new RotRect(area.Location.ToVector2(), area.Size.ToVector2(), angle, origin));
        /// <summary>Query and return the items intersecting <paramref name="rect"/>.</summary>
        public IEnumerable<int> Query(RotRect rect) {
            var broad = rect;
            broad.Inflate(_maxRadiusItem.HalfSize, _maxRadiusItem.HalfSize);
            _toProcess.Push(0);
            do {
                var nodeId = _toProcess.Pop();
                var node = _nodes[nodeId];
                int halfWidth = _bounds.Width >> node._depth,
                    halfHeight = _bounds.Height >> node._depth;
                var bounds = new Rectangle(node._cX - halfWidth, node._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                if (node._itemCount > 0) {
                    var item = _items[node._firstChild];
                    if (rect.Contains(bounds)) {
                        //do {
                        //    yield return item._id;
                        //    if (item._next < 0)
                        //        break;
                        //    item = _items[item._next];
                        //} while (true);
                        var itemId = node._firstChild;
                        for (var i = 0; i < node._itemCount; i++) {
                            item = _items[itemId];
                            yield return item._id;
                            itemId = item._next;
                        }
                    } else
                        do {
                            if (rect.Intersects(item._bounds))
                                yield return item._id;
                            if (item._next < 0)
                                break;
                            item = _items[item._next];
                        } while (true);
                } else if (node._itemCount < 0) {
                    var node2 = _nodes[node._firstChild];
                    halfWidth = _bounds.Width >> node2._depth;
                    halfHeight = _bounds.Height >> node2._depth;
                    bounds = new Rectangle(node2._cX - halfWidth, node2._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(node._firstChild);
                    node2 = _nodes[node._firstChild + 1];
                    halfWidth = _bounds.Width >> node2._depth;
                    halfHeight = _bounds.Height >> node2._depth;
                    bounds = new Rectangle(node2._cX - halfWidth, node2._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(node._firstChild + 1);
                    node2 = _nodes[node._firstChild + 2];
                    halfWidth = _bounds.Width >> node2._depth;
                    halfHeight = _bounds.Height >> node2._depth;
                    bounds = new Rectangle(node2._cX - halfWidth, node2._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(node._firstChild + 2);
                    node2 = _nodes[node._firstChild + 3];
                    halfWidth = _bounds.Width >> node2._depth;
                    halfHeight = _bounds.Height >> node2._depth;
                    bounds = new Rectangle(node2._cX - halfWidth, node2._cY - halfHeight, halfWidth << 1, halfHeight << 1);
                    if (broad.Intersects(bounds))
                        _toProcess.Push(node._firstChild + 3);
                    continue;
                }
            } while (_toProcess.Count > 0);
            yield break;
        }

        int Insert(int id, int nodeId, RotRect bounds, Point xy) {
            do {
                var node = _nodes[nodeId];
                if (node._itemCount < 0) {
                    nodeId = xy.X < node._cX ? xy.Y < node._cY ? node._firstChild : node._firstChild + 3 : xy.Y < node._cY ? node._firstChild + 1 : node._firstChild + 2;
                    continue;
                }
                ForceInsert(id, nodeId, bounds, xy);
                return nodeId;
            } while (true);
        }

        void ForceInsert(int id, int nodeId, RotRect bounds, Point xy) {
            var node = _nodes[nodeId];
            _items[id]._bounds = bounds;
            _items[id]._xy = xy;
            _items[id]._node = nodeId;
            _items[id]._next = -1;
            if (node._itemCount == 0)
                _nodes[nodeId]._firstChild = id;
            else {
                var itemId = node._firstChild;
                var item = _items[itemId];
                for (var i = 1; i < node._itemCount; i++) {
                    itemId = item._next;
                    item = _items[itemId];
                }
                _items[itemId]._next = id;
            }
            _nodes[nodeId]._itemCount++;
        }

        void TrySubdivide(int nodeId) {
            var node = _nodes[nodeId];
            if (node._itemCount > Node.ITEM_CAPACITY && node._depth < 6) {
                var depth = (byte)(node._depth + 1);
                int halfWidth = _bounds.Width >> depth,
                    halfHeight = _bounds.Height >> depth;
                _nodes[_nextNodeId] = new Node {
                    _cX = node._cX - halfWidth,
                    _cY = node._cY - halfHeight,
                    _depth = depth,
                    _parent = nodeId
                };
                _nodes[_nextNodeId + 1] = new Node {
                    _cX = node._cX + halfWidth,
                    _cY = node._cY - halfHeight,
                    _depth = depth,
                    _parent = nodeId
                };
                _nodes[_nextNodeId + 2] = new Node {
                    _cX = node._cX + halfWidth,
                    _cY = node._cY + halfHeight,
                    _depth = depth,
                    _parent = nodeId
                };
                _nodes[_nextNodeId + 3] = new Node {
                    _cX = node._cX - halfWidth,
                    _cY = node._cY + halfHeight,
                    _depth = depth,
                    _parent = nodeId
                };
                var itemId = node._firstChild;
                for (var i = 0; i < node._itemCount; i++) {
                    var item = _items[itemId];
                    int nodeId2;
                    nodeId2 = item._xy.X < node._cX ? item._xy.Y < node._cY ? _nextNodeId : _nextNodeId + 3 : item._xy.Y < node._cY ? _nextNodeId + 1 : _nextNodeId + 2;
                    ForceInsert(itemId, nodeId2, item._bounds, item._xy);
                    itemId = item._next;
                }
                _nodes[nodeId]._firstChild = _nextNodeId;
                _nodes[nodeId]._itemCount = -3;
                _nextNodeId += 4;
            }
        }
    }
}