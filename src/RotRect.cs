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
            Vector2 center = Util.Rotate(Rect, Angle, Origin).Center.ToVector2(),
                closest,
                tl,
                tr,
                br,
                bl;
            float cos = MathF.Cos(rectangle.Angle),
                sin = MathF.Sin(rectangle.Angle),
                x = -rectangle.Origin.X,
                y = -rectangle.Origin.Y,
                w = rectangle.Rect.Width + x,
                h = rectangle.Rect.Height + y,
                xcos = x * cos,
                ycos = y * cos,
                xsin = x * sin,
                ysin = y * sin,
                wcos = w * cos,
                wsin = w * sin,
                hcos = h * cos,
                hsin = h * sin,
                tlx = xcos - ysin + rectangle.Rect.X,
                tly = xsin + ycos + rectangle.Rect.Y,
                trx = wcos - ysin + rectangle.Rect.X,
                tr_y = wsin + ycos + rectangle.Rect.Y,
                brx = wcos - hsin + rectangle.Rect.X,
                bry = wsin + hcos + rectangle.Rect.Y,
                blx = xcos - hsin + rectangle.Rect.X,
                bly = xsin + hcos + rectangle.Rect.Y;
            if (Vector2.DistanceSquared(tl = new Vector2(tlx, tly), center) < Vector2.DistanceSquared(tr = new Vector2(trx, tr_y), center))
                closest = tl;
            else
                closest = tr;
            if (Vector2.DistanceSquared(br = new Vector2(brx, bry), center) < Vector2.DistanceSquared(closest, center))
                closest = br;
            if (Vector2.DistanceSquared(bl = new Vector2(blx, bly), center) < Vector2.DistanceSquared(closest, center))
                closest = bl;
            if (closest == tl)
            {
                Vector2 a = new Line(tl, bl).ClosestPoint(center),
                    b = new Line(tl, tr).ClosestPoint(center);
                if (Vector2.DistanceSquared(a, center) < Vector2.DistanceSquared(b, center))
                    closest = a;
                else
                    closest = b;
            }
            else if (closest == tr)
            {
                Vector2 a = new Line(tr, tl).ClosestPoint(center),
                    b = new Line(tr, br).ClosestPoint(center);
                if (Vector2.DistanceSquared(a, center) < Vector2.DistanceSquared(b, center))
                    closest = a;
                else
                    closest = b;
            }
            else if (closest == br)
            {
                Vector2 a = new Line(br, tr).ClosestPoint(center),
                    b = new Line(br, bl).ClosestPoint(center);
                if (Vector2.DistanceSquared(a, center) < Vector2.DistanceSquared(b, center))
                    closest = a;
                else
                    closest = b;
            }
            else
            {
                Vector2 a = new Line(bl, tl).ClosestPoint(center),
                    b = new Line(bl, br).ClosestPoint(center);
                if (Vector2.DistanceSquared(a, center) < Vector2.DistanceSquared(b, center))
                    closest = a;
                else
                    closest = b;
            }
            var rect = Rect;
            rect.Offset(-Origin);
            cos = MathF.Cos(-Angle);
            sin = MathF.Sin(-Angle);
            float cx = closest.X - Rect.X,
                cy = closest.Y - Rect.Y;
            return rect.Contains(new Vector2(cx * cos + cy * -sin + Rect.X, cx * sin + cy * cos + Rect.Y));
        }
    }
}