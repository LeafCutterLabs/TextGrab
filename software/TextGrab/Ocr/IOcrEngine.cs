using System.Windows.Media.Imaging;

namespace TextGrab.Ocr;

public interface IOcrEngine : IDisposable
{
    Task<IReadOnlyList<string>> RecognizeLinesAsync(BitmapSource image, CancellationToken cancellationToken);
}
