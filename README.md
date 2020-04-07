# Dcrew.MonoGame.2D Spatial Partition
 A set of highly-optimized, flexible and powerful 2D spatial partitions for MonoGame

## Build
### [NuGet](https://www.nuget.org/packages/Dcrew.MonoGame.2D_Spatial_Partition) [![NuGet ver](https://img.shields.io/nuget/v/Dcrew.MonoGame.2D_Spatial_Partition)](https://www.nuget.org/packages/Dcrew.MonoGame.2D_Spatial_Partition) [![NuGet downloads](https://img.shields.io/nuget/dt/Dcrew.MonoGame.2D_Spatial_Partition)](https://www.nuget.org/packages/Dcrew.MonoGame.2D_Spatial_Partition)

## Features
- #### [Quadtree](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs)
  - static, using generics where T : class, [IAABB](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/IAABB.cs)
  - Auto expands if items are added/updated outside of its current bounds
  - [Add](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L369)(T item) - Adds **item** to the tree
  - [Update](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L410)(T item) - Updates **item** in the tree to its latest [IAABB](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/IAABB.cs) info
  - [Remove](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L382)(T item) - Removes **item** from the tree
  - [Clear](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L402)() - Clears all items from the tree
  - [Shrink](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L496)() - Shrinks the tree down to its smallest possible size given the items in the tree
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L445)(Point p) - Returns all items intersecting p
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L445)(Point pos) - Returns all items intersecting pos
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L451)(Vector2 pos) - Returns all items intersecting pos
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L457)(Rectangle rect) - Returns all items intersecting rect
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs#L466)(Rectangle rect, float angle, Vector2 origin) - Returns all items intersecting rect rotated by angle (in radians) given origin of rect (in pixels)
- #### [Spatial Hash](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs)
  - static, using generics where T : class, [IAABB](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/IAABB.cs)
  - Infinite
  - [Add](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L68)(T item) - Adds **item** to the tree
  - [Update](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L76)(T item) - Updates **item** in the tree to its latest [IAABB](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/IAABB.cs) info
  - [Remove](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L96)(T item) - Removes **item** from the tree
  - [Clear](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L111)(T item) - Clears all items from the tree
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L122)(Point pos) - Returns all items intersecting or near pos
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L124)(Vector2 pos) - Returns all items intersecting or near pos
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L126)(Rectangle rect) - Returns all items intersecting or near rect
  - [Query](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs#L156)(Rectangle rect, float angle, Vector2 origin) - Returns all items intersecting or near rect rotated by angle (in radians) given origin of rect (in pixels)

### What is [IAABB](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/IAABB.cs)?
#### An interface contract required on classes that use the [Quadtree](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/Quadtree.cs) or [Spatial Hash](https://github.com/DeanReynolds/Dcrew.MonoGame.2D-Spatial-Partition/blob/master/src/SpatialHash.cs), consists of an AABB (rectangle) or axis-aligned bounding box, an Angle (float, in radians) and an Origin (in pixels) of the AABB
