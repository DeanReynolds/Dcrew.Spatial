using Microsoft.Xna.Framework;

namespace Dcrew.MonoGame._2D_Spatial_Partition
{
    /// <summary>Axis-aligned 2D bounding-box/rectangle</summary>
    public interface IAABB
    {
        /// <summary>2D bounding box</summary>
        Rectangle AABB { get; }
        /// <summary>Rotation (in radians)</summary>
        float Angle { get; }
        /// <summary>Origin (in pixels) of <see cref="AABB"/></summary>
        Vector2 Origin { get; }
    }
}