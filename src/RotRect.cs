using Microsoft.Xna.Framework;

namespace Dcrew.Spatial
{
    struct RotRect
    {
        public Rectangle Rect;
        public float Angle;
        public Vector2 Origin;

        public Vector2 TopLeft
        {
            get
            {
                var topLeft = new Vector2(Rect.Left, Rect.Top);
                return RotatePoint(topLeft, topLeft + Origin, Angle);
            }
        }
        public Vector2 TopRight => RotatePoint(new Vector2(Rect.Right, Rect.Top), new Vector2(Rect.Left, Rect.Top) + Origin, Angle);
        public Vector2 BottomLeft => RotatePoint(new Vector2(Rect.Left, Rect.Bottom), new Vector2(Rect.Left, Rect.Top) + Origin, Angle);
        public Vector2 BottomRight => RotatePoint(new Vector2(Rect.Right, Rect.Bottom), new Vector2(Rect.Left, Rect.Top) + Origin, Angle);

        public RotRect(Rectangle rect, float angle, Vector2 origin)
        {
            Rect = rect;
            Angle = angle;
            Origin = origin;
        }

        public bool Intersects(Rectangle rectangle) => Intersects(new RotRect(rectangle, 0, Vector2.Zero));
        public bool Intersects(RotRect rectangle)
        {
            Vector2 axisTop = TopRight - TopLeft,
                axisRight = TopRight - BottomRight,
                axisOTop = rectangle.TopRight - rectangle.TopLeft,
                axisORight = rectangle.TopRight - rectangle.BottomRight;
            if (!IsAxisColliding(rectangle, axisTop) ||
                !IsAxisColliding(rectangle, axisRight) ||
                !rectangle.IsAxisColliding(this, axisOTop) ||
                !rectangle.IsAxisColliding(this, axisORight))
                return false;
            else return true;
        }

        bool IsAxisColliding(RotRect rect, Vector2 axis)
        {
            float s1 = Vector2.Dot(TopLeft, axis),
                s2 = Vector2.Dot(TopRight, axis),
                s3 = Vector2.Dot(BottomLeft, axis),
                s4 = Vector2.Dot(BottomRight, axis),
                min = s1,
                max = s1;
            if (s2 < min)
                min = s2;
            if (s3 < min)
                min = s3;
            if (s4 < min)
                min = s4;
            if (s2 > max)
                max = s2;
            if (s3 > max)
                max = s3;
            if (s4 > max)
                max = s4;
            float os1 = Vector2.Dot(rect.TopLeft, axis),
                os2 = Vector2.Dot(rect.TopRight, axis),
                os3 = Vector2.Dot(rect.BottomLeft, axis),
                os4 = Vector2.Dot(rect.BottomRight, axis),
                omin = os1,
                omax = os1;
            if (os2 < omin)
                omin = os2;
            if (os3 < omin)
                omin = os3;
            if (os4 < omin)
                omin = os4;
            if (os2 > omax)
                omax = os2;
            if (os3 > omax)
                omax = os3;
            if (os4 > omax)
                omax = os4;
            return max >= omax && min <= omax || omax >= max && omin <= max;
        }

        Vector2 RotatePoint(Vector2 point, Vector2 origin, float rotation) => Vector2.Transform(point - origin, Matrix.CreateRotationZ(rotation)) + origin;
    }
}