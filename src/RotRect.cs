using Microsoft.Xna.Framework;
using System;

namespace Dcrew.Spatial {
    /// <summary>A rotated rectangle</summary>
    public struct RotRect {
        struct Line {
            public Vector2 A, B;

            public Line(Vector2 a, Vector2 b) {
                A = a;
                B = b;
            }

            public Vector2 ClosestPoint(Vector2 p) {
                var ab = B - A;
                var distance = Vector2.Dot(p - A, ab) / ab.LengthSquared();
                return distance < 0 ? A : distance > 1 ? B : A + ab * distance;
            }
        }

        /// <summary>Position</summary>
        public Vector2 XY;
        /// <summary>Size of rect (in pixels)</summary>
        public Vector2 Size;
        /// <summary>Rotation (in radians)</summary>
        public float Angle;
        /// <summary>Origin (in pixels)</summary>
        public Vector2 Origin;

        public RotRect(Vector2 xy, Vector2 size, float angle, Vector2 origin) {
            XY = xy;
            Size = size;
            Angle = angle;
            Origin = origin;
        }

        public bool Intersects(Rectangle rectangle) => Intersects(new RotRect(rectangle.Location.ToVector2(), rectangle.Size.ToVector2(), 0, Vector2.Zero));
        public bool Intersects(RotRect rectangle) => IntersectsAnyEdge(rectangle) || rectangle.IntersectsAnyEdge(this);
        public bool Contains(Rectangle rectangle) => Contains(new RotRect(rectangle.Location.ToVector2(), rectangle.Size.ToVector2(), 0, Vector2.Zero));
        public bool Contains(RotRect rectangle) {
            static float IsLeft(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y);
            static bool PointInRectangle(Vector2 x, Vector2 y, Vector2 z, Vector2 w, Vector2 p) => IsLeft(x, y, p) > 0 && IsLeft(y, z, p) > 0 && IsLeft(z, w, p) > 0 && IsLeft(w, x, p) > 0;
            float cos = MathF.Cos(Angle),
             sin = MathF.Sin(Angle),
             x = -Origin.X,
             y = -Origin.Y,
             w = Size.X + x,
             h = Size.Y + y,
             xcos = x * cos,
             ycos = y * cos,
             xsin = x * sin,
             ysin = y * sin,
             wcos = w * cos,
             wsin = w * sin,
             hcos = h * cos,
             hsin = h * sin;
            Vector2 tl2 = new Vector2(xcos - ysin + XY.X, xsin + ycos + XY.Y),
             tr2 = new Vector2(wcos - ysin + XY.X, wsin + ycos + XY.Y),
             br2 = new Vector2(wcos - hsin + XY.X, wsin + hcos + XY.Y),
             bl2 = new Vector2(xcos - hsin + XY.X, xsin + hcos + XY.Y);
            cos = MathF.Cos(rectangle.Angle);
            sin = MathF.Sin(rectangle.Angle);
            x = -rectangle.Origin.X;
            y = -rectangle.Origin.Y;
            w = rectangle.Size.X + x;
            h = rectangle.Size.Y + y;
            xcos = x * cos;
            ycos = y * cos;
            xsin = x * sin;
            ysin = y * sin;
            wcos = w * cos;
            wsin = w * sin;
            hcos = h * cos;
            hsin = h * sin;
            Vector2 tl = new Vector2(xcos - ysin + rectangle.XY.X, xsin + ycos + rectangle.XY.Y),
             tr = new Vector2(wcos - ysin + rectangle.XY.X, wsin + ycos + rectangle.XY.Y),
             br = new Vector2(wcos - hsin + rectangle.XY.X, wsin + hcos + rectangle.XY.Y),
             bl = new Vector2(xcos - hsin + rectangle.XY.X, xsin + hcos + rectangle.XY.Y);
            return PointInRectangle(tl2, tr2, br2, bl2, tl) && PointInRectangle(tl2, tr2, br2, bl2, tr) && PointInRectangle(tl2, tr2, br2, bl2, br) && PointInRectangle(tl2, tr2, br2, bl2, bl);
        }

        bool IntersectsAnyEdge(RotRect rectangle) {
            static float IsLeft(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y);
            static bool PointInRectangle(Vector2 x, Vector2 y, Vector2 z, Vector2 w, Vector2 p) => IsLeft(x, y, p) > 0 && IsLeft(y, z, p) > 0 && IsLeft(z, w, p) > 0 && IsLeft(w, x, p) > 0;
            Vector2 center = Util.Rotate(XY, Size, Angle, Origin).Center.ToVector2();
            float cos = MathF.Cos(Angle),
             sin = MathF.Sin(Angle),
             x = -Origin.X,
             y = -Origin.Y,
             w = Size.X + x,
             h = Size.Y + y,
             xcos = x * cos,
             ycos = y * cos,
             xsin = x * sin,
             ysin = y * sin,
             wcos = w * cos,
             wsin = w * sin,
             hcos = h * cos,
             hsin = h * sin;
            Vector2 tl2 = new Vector2(xcos - ysin + XY.X, xsin + ycos + XY.Y),
             tr2 = new Vector2(wcos - ysin + XY.X, wsin + ycos + XY.Y),
             br2 = new Vector2(wcos - hsin + XY.X, wsin + hcos + XY.Y),
             bl2 = new Vector2(xcos - hsin + XY.X, xsin + hcos + XY.Y);
            cos = MathF.Cos(rectangle.Angle);
            sin = MathF.Sin(rectangle.Angle);
            x = -rectangle.Origin.X;
            y = -rectangle.Origin.Y;
            w = rectangle.Size.X + x;
            h = rectangle.Size.Y + y;
            xcos = x * cos;
            ycos = y * cos;
            xsin = x * sin;
            ysin = y * sin;
            wcos = w * cos;
            wsin = w * sin;
            hcos = h * cos;
            hsin = h * sin;
            Vector2 tl = new Vector2(xcos - ysin + rectangle.XY.X, xsin + ycos + rectangle.XY.Y),
             tr = new Vector2(wcos - ysin + rectangle.XY.X, wsin + ycos + rectangle.XY.Y),
             br = new Vector2(wcos - hsin + rectangle.XY.X, wsin + hcos + rectangle.XY.Y),
             bl = new Vector2(xcos - hsin + rectangle.XY.X, xsin + hcos + rectangle.XY.Y);
            return PointInRectangle(tl2, tr2, br2, bl2, new Line(tl, tr).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(tr, br).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(br, bl).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(bl, tl).ClosestPoint(center));
        }
    }
}