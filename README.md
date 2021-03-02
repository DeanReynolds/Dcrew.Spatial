# Dcrew.Spatial
 A set of highly-optimized, flexible and powerful 2D spatial partitions for [MonoGame](https://github.com/MonoGame/MonoGame)

## Build
### [NuGet](https://www.nuget.org/packages/Dcrew.Spatial) [![NuGet ver](https://img.shields.io/nuget/v/Dcrew.Spatial)](https://www.nuget.org/packages/Dcrew.Spatial) [![NuGet downloads](https://img.shields.io/nuget/dt/Dcrew.Spatial)](https://www.nuget.org/packages/Dcrew.Spatial)

## How to use
1. Make a Quadtree variable
```cs
Quadtree tree = new Quadtree(x: 0, y: 0, width: 500, height: 500, maxItems: 100, maxDepth: 8);
```

2. Add/Update an item(s)
```cs
tree.Update(0, x: 5, y: 5, width: 80, height: 25);
```

3. Query an area(s)
```cs
using (var query = tree.Query(new Point(3, 4))) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Query(new Vector2(32.5f, 25))) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Query(new Rectangle(x: 7, y: 2, width: 32, height: 27))) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Query(new Rectangle(x: 7, y: 2, width: 32, height: 27), rotation: 0, origin: Vector2.Zero)) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Query(new Point(3, 4), radius: 10)) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Linecast(new Vector2(3, 4), new Vector2(8, 12), thickness: 3)) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Raycast(new Vector2(3, 4), direction: new Vector2(.5f, .75f), thickness: 3)) {
 foreach (int i in query) {
  // ...
 }
}
using (var query = tree.Raycast(new Vector2(3, 4), rotation: MathF.PI, thickness: 3)) {
 foreach (int i in query) {
  // ...
 }
}
```

4. When an item moves, update it!
```cs
tree.Update(0, x: 5, y: 5, width: 80, height: 25);
```

5. Did an entity despawn? Remove it!
```cs
tree.Remove(0);
```

6. Call tree update at the end of every game update!
```cs
tree.Update();
```
