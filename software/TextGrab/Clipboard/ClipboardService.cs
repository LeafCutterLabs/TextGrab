using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TextGrab.Clipboard;

public sealed class ClipboardService
{
    private const int Attempts = 6;

    public async Task<BitmapSource> GetImageAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!System.Windows.Clipboard.ContainsImage())
                    throw new InvalidOperationException("The clipboard does not contain an image.");
                var image = System.Windows.Clipboard.GetImage()
                    ?? throw new InvalidOperationException("The clipboard image could not be read.");
                image.Freeze();
                return image;
            }
            catch (COMException)
            {
                if (attempt == Attempts - 1)
                    throw new InvalidOperationException("The clipboard is busy. Close any clipboard manager and try again.");
                await Task.Delay(35 * (attempt + 1), cancellationToken);
            }
        }
        throw new InvalidOperationException("The clipboard is busy. Close any clipboard manager and try again.");
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
                return;
            }
            catch (COMException)
            {
                if (attempt == Attempts - 1)
                    throw new InvalidOperationException("The clipboard is busy. Try copying again.");
                await Task.Delay(35 * (attempt + 1), cancellationToken);
            }
        }
        throw new InvalidOperationException("The clipboard is busy. Try copying again.");
    }
}
