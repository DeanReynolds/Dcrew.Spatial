namespace Dcrew.Spatial {
    /// <summary>Oriented bounding box</summary>
    public interface IBounds {
        /// <summary>A <see cref="RotRect"/> defining the bounds of this object.</summary>
        RotRect Bounds { get; }
    }
}