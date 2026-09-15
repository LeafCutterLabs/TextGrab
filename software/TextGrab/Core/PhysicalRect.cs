using System.Drawing;

namespace TextGrab.Core;

public readonly record struct PhysicalRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);

    public static PhysicalRect FromPoints(Point a, Point b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new(left, top, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }

    public PhysicalRect Intersect(PhysicalRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? new PhysicalRect(left, top, 0, 0)
            : new PhysicalRect(left, top, right - left, bottom - top);
    }
}
