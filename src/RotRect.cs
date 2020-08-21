using Microsoft.Xna.Framework;
using System;

namespace Dcrew.Spatial
{
    struct RotRect
    {
        struct Line
        {
            public Vector2 A, B;

            public Line(Vector2 a, Vector2 b)
            {
                A = a;
                B = b;
            }

            public Vector2 ClosestPoint(Vector2 p)
            {
                var ab = B - A;
                var distance = Vector2.Dot(p - A, ab) / ab.LengthSquared();
                return distance < 0 ? A : distance > 1 ? B : A + ab * distance;
            }
        }

        public Rectangle Rect;
        public float Angle;
        public Vector2 Origin;

        public RotRect(Rectangle rect, float angle, Vector2 origin)
        {
            Rect = rect;
            Angle = angle;
            Origin = origin;
        }

        public bool Intersects(Rectangle rectangle) => Intersects(new RotRect(rectangle, 0, Vector2.Zero));
        public bool Intersects(RotRect rectangle) => IntersectsAnyEdge(rectangle) || rectangle.IntersectsAnyEdge(this);

        bool IntersectsAnyEdge(RotRect rectangle)
        {
            static float IsLeft(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y);
            static bool PointInRectangle(Vector2 x, Vector2 y, Vector2 z, Vector2 w, Vector2 p) => (IsLeft(x, y, p) > 0 && IsLeft(y, z, p) > 0 && IsLeft(z, w, p) > 0 && IsLeft(w, x, p) > 0);
            Vector2 center = Util.Rotate(Rect, Angle, Origin).Center.ToVector2(),
                closest;
            float cos = MathF.Cos(Angle),
             sin = MathF.Sin(Angle),
             x = -Origin.X,
             y = -Origin.Y,
             w = Rect.Width + x,
             h = Rect.Height + y,
             xcos = x * cos,
             ycos = y * cos,
             xsin = x * sin,
             ysin = y * sin,
             wcos = w * cos,
             wsin = w * sin,
             hcos = h * cos,
             hsin = h * sin;
            Vector2 tl2 = new Vector2(xcos - ysin + Rect.X, xsin + ycos + Rect.Y),
             tr2 = new Vector2(wcos - ysin + Rect.X, wsin + ycos + Rect.Y),
             br2 = new Vector2(wcos - hsin + Rect.X, wsin + hcos + Rect.Y),
             bl2 = new Vector2(xcos - hsin + Rect.X, xsin + hcos + Rect.Y);
            cos = MathF.Cos(rectangle.Angle);
            sin = MathF.Sin(rectangle.Angle);
            x = -rectangle.Origin.X;
            y = -rectangle.Origin.Y;
            w = rectangle.Rect.Width + x;
            h = rectangle.Rect.Height + y;
            xcos = x * cos;
            ycos = y * cos;
            xsin = x * sin;
            ysin = y * sin;
            wcos = w * cos;
            wsin = w * sin;
            hcos = h * cos;
            hsin = h * sin;
            Vector2 tl = new Vector2(xcos - ysin + rectangle.Rect.X, xsin + ycos + rectangle.Rect.Y),
             tr = new Vector2(wcos - ysin + rectangle.Rect.X, wsin + ycos + rectangle.Rect.Y),
             br = new Vector2(wcos - hsin + rectangle.Rect.X, wsin + hcos + rectangle.Rect.Y),
             bl = new Vector2(xcos - hsin + rectangle.Rect.X, xsin + hcos + rectangle.Rect.Y);
            return PointInRectangle(tl2, tr2, br2, bl2, new Line(tl, tr).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(tr, br).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(br, bl).ClosestPoint(center)) || PointInRectangle(tl2, tr2, br2, bl2, new Line(bl, tl).ClosestPoint(center));
        }
    }
}