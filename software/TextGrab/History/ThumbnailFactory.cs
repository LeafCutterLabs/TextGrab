using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TextGrab.History;

public static class ThumbnailFactory
{
    public const int MaxWidth = 120;
    public const int MaxHeight = 72;

    public static BitmapSource Create(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            throw new ArgumentException("The source image is empty.", nameof(source));

        var scale = Math.Min(1d, Math.Min((double)MaxWidth / source.PixelWidth, (double)MaxHeight / source.PixelHeight));
        BitmapSource scaled = source;
        if (scale < 1d)
        {
            var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            transformed.Freeze();
            scaled = transformed;
        }

        if (scaled.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            scaled = converted;
        }

        var stride = checked(scaled.PixelWidth * 4);
        var pixels = new byte[checked(stride * scaled.PixelHeight)];
        scaled.CopyPixels(pixels, stride, 0);
        var detached = BitmapSource.Create(
            scaled.PixelWidth,
            scaled.PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        detached.Freeze();
        return detached;
    }
}
