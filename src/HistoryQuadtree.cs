//using Apos.History;
//using Microsoft.Xna.Framework;
//using System.Collections;
//using System.Collections.Generic;

//namespace Dcrew.Spatial
//{
//    /// <summary>A <see cref="Quadtree{T}"/> using <see cref="Apos.History"/> to allow for undoing/redoing</summary>
//    public class HistoryQuadtree<T> : History, IEnumerable<T> where T : class, IBounds
//    {
//        /// <summary>Set the boundary rect of this tree</summary>
//        public Rectangle Bounds
//        {
//            get => _tree.Bounds;
//            set
//            {
//                var bounds = _tree.Bounds;
//                _futureSetup.Add(() =>
//                {
//                    bounds = _tree.Bounds;
//                    _tree.Bounds = value;
//                });
//                _pastSetup.Add(() => { _tree.Bounds = bounds; });
//            }
//        }

//        /// <summary>Returns true if <paramref name="item"/> is in the tree</summary>
//        public bool Contains(T item) => _tree.Contains(item);
//        /// <summary>Returns an enumerator that iterates through the collection</summary>
//        public IEnumerator<T> GetEnumerator() => _tree.GetEnumerator();
//        /// <summary>Return count of all items</summary>
//        public int ItemCount => _tree.ItemCount;
//        /// <summary>Return all items and their container rects</summary>
//        public IEnumerable<(T Item, Rectangle Node)> Bundles => _tree.Bundles;
//        /// <summary>Return all node bounds in this tree</summary>
//        public IEnumerable<Rectangle> Nodes => _tree.Nodes;
//        /// <summary>Return count of all nodes</summary>
//        public int NodeCount => _tree.NodeCount;

//        readonly Quadtree<T> _tree = new Quadtree<T>();

//        HistoryQuadtree() : base(new Optional.Option<HistoryHandler>()) { }

//        /// <summary>Inserts <paramref name="item"/> into the tree. ONLY USE IF <paramref name="item"/> ISN'T ALREADY IN THE TREE</summary>
//        public void Add(T item)
//        {
//            var bounds = _tree.Bounds;
//            _futureSetup.Add(() =>
//            {
//                bounds = _tree.Bounds;
//                _tree.Add(item);
//            });
//            _pastSetup.Add(() =>
//            {
//                _tree.Remove(item);
//                _tree.Bounds = bounds;
//            });
//            TryCommit();
//        }
//        /// <summary>Updates <paramref name="item"/>'s position in the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
//        public void Update(T item)
//        {
//            var ii = _tree._item[item];
//            var bounds = _tree.Bounds;
//            _futureSetup.Add(() =>
//            {
//                ii = _tree._item[item];
//                bounds = _tree.Bounds;
//                _tree.Update(item);
//            });
//            _pastSetup.Add(() =>
//            {
//                _tree._item[item].Node.Remove(item);
//                _tree._item[item] = (_tree.Insert(item, ii.Node, bounds.Center), ii.XY);
//                _tree.Bounds = bounds;
//            });
//            TryCommit();
//        }
//        /// <summary>Removes <paramref name="item"/> from the tree. ONLY USE IF <paramref name="item"/> IS ALREADY IN THE TREE</summary>
//        public void Remove(T item)
//        {
//            _futureSetup.Add(() => _tree.Remove(item));
//            _pastSetup.Add(() => _tree.Add(item));
//            TryCommit();
//        }
//        /// <summary>Removes all items and nodes from the tree</summary>
//        public void Clear()
//        {
//            (T Item, Rectangle AABB)[] items = new (T, Rectangle)[0];
//            _futureSetup.Add(() =>
//            {
//                items = new (T, Rectangle)[_tree.ItemCount];
//                var i = 0;
//                foreach (var item in _tree)
//                    items[i++] = (item, Util.Rotate(item.Bounds.XY, item.Bounds.Size, item.Bounds.Angle, item.Bounds.Origin));
//                _tree.Clear();
//            });
//            _pastSetup.Add(() =>
//            {
//                foreach (var (Item, AABB) in items)
//                    _tree.Insert(Item, _tree._root, AABB.Center);
//            });
//            TryCommit();
//        }
//        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
//        public IEnumerable<T> Query(Point xy) => _tree.Query(xy);
//        /// <summary>Query and return the items intersecting <paramref name="xy"/></summary>
//        public IEnumerable<T> Query(Vector2 xy) => _tree.Query(xy);
//        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
//        public IEnumerable<T> Query(Rectangle area) => _tree.Query(area);
//        /// <summary>Query and return the items intersecting <paramref name="area"/></summary>
//        /// <param name="area">Area (rectangle)</param>
//        /// <param name="angle">Rotation (in radians) of <paramref name="area"/></param>
//        /// <param name="origin">Origin (in pixels) of <paramref name="area"/></param>
//        public IEnumerable<T> Query(Rectangle area, float angle, Vector2 origin) => _tree.Query(area, angle, origin);

//        /// <summary>You need to call this if you don't use base.Update() in <see cref="Game.Update(GameTime)"/></summary>
//        public void Update()
//        {
//            var bounds = _tree.Bounds;
//            _futureSetup.Add(() =>
//            {
//                bounds = _tree.Bounds;
//                _tree.Update();
//            });
//            _pastSetup.Add(() => { _tree.Bounds = bounds; });
//            TryCommit();
//        }

//        /// <summary>Shrinks the tree to the smallest possible size</summary>
//        public void Shrink()
//        {
//            var bounds = _tree.Bounds;
//            _futureSetup.Add(() =>
//            {
//                bounds = _tree.Bounds;
//                _tree.Shrink();
//            });
//            _pastSetup.Add(() => { _tree.Bounds = bounds; });
//            TryCommit();
//        }

//        IEnumerator IEnumerable.GetEnumerator() => _tree.GetEnumerator();
//    }
//}