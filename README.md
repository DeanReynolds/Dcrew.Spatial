# Dcrew.Spatial
 A set of highly-optimized, flexible and powerful 2D spatial partitions for [MonoGame](https://github.com/MonoGame/MonoGame)

## Build
### [NuGet](https://www.nuget.org/packages/Dcrew.Spatial) [![NuGet ver](https://img.shields.io/nuget/v/Dcrew.Spatial)](https://www.nuget.org/packages/Dcrew.Spatial) [![NuGet downloads](https://img.shields.io/nuget/dt/Dcrew.Spatial)](https://www.nuget.org/packages/Dcrew.Spatial)

## How to use
1. An item must inherit IBounds (interface)
```cs
class Item : IBounds {
 public RotRect Bounds; // RotRect is a struct
 public Vector2 XY { // Position
  get => Bounds.XY;
  set => Bounds.XY = value;
 }
 public float X { // X Position
  get => Bounds.XY.X;
  set => Bounds.XY.X = value;
 }
 public float Y { // Y Position
  get => Bounds.XY.Y;
  set => Bounds.XY.Y = value;
 }
 public Vector2 Size { // Size of bound
  get => Bounds.Size;
  set => Bounds.Size = value;
 }
 public float Angle { // Rotation (in radians)
  get => Bounds.Angle;
  set => Bounds.Angle = value;
 }
 public Vector2 Origin { // Origin
  get => Bounds.Origin;
  set => Bounds.Origin = value;
 }
}
```

2. Make a Quadtree variable
```cs
Quadtree<Item> tree = new Quadtree<Item>();
```

3. Add an item(s)
```cs
var itemA = new Item {
 XY = new Vector2(0, 0),
 Size = new Vector2(10, 6),
 Origin = new Vector2(5, 3), // center
 Angle = .5f
};
tree.Add(itemA);
var itemB = new Item {
 XY = new Vector2(30, 20),
 Size = new Vector2(15, 10),
 Origin = new Vector2(7.5f, 5), // center
 Angle = 1.2f
};
tree.Add(itemB);
```

4. Query an area(s)
```cs
foreach (var item in tree.Query(new Point(3, 4)) {
 // ...
}
foreach (var item in tree.Query(new Vector2(32.5f, 25)) {
 // ...
}
foreach (var item in tree.Query(new Rectangle(x: 7, y: 2, width: 32, height: 27)) {
 // ...
}
foreach (var item in tree.Query(new RotRect(xy: new Vector2(7, 2), size: new Vector2(32, 27), angle: 0, origin: Vector2.Zero)) {
 // ...
}
```

5. When an item moves, update it!
```cs
Item itemA; // has its xy, angle, size, or origin changed?
tree.Update(itemA); // call this if so, it's incredibly optimized so don't worry!
```
