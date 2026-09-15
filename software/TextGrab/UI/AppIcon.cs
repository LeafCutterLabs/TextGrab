using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace TextGrab.UI;

public static class AppIcon
{
    private const string ResourceName = "TextGrab.Assets.TextGrab.ico";

    public static BitmapSource CreateImageSource()
    {
        using var stream = OpenStream();
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var image = decoder.Frames.MaxBy(frame => frame.PixelWidth * frame.PixelHeight)
            ?? throw new InvalidOperationException("The embedded application icon has no image frames.");
        image.Freeze();
        return image;
    }

    public static Icon CreateDrawingIcon()
    {
        using var stream = OpenStream();
        using var source = new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        return (Icon)source.Clone();
    }

    private static Stream OpenStream() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded application icon '{ResourceName}' was not found.");
}
