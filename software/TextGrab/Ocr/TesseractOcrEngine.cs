using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tesseract;

namespace TextGrab.Ocr;

public sealed class TesseractOcrEngine : IOcrEngine
{
    public const long MaxPixels = 50_000_000;
    private const string RecognitionLanguages = "eng+spa";
    private readonly string _dataPath;
    private bool _disposed;

    public TesseractOcrEngine(string dataPath)
    {
        _dataPath = dataPath;
        foreach (var language in RecognitionLanguages.Split('+'))
        {
            var modelPath = Path.Combine(dataPath, $"{language}.traineddata");
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"The {language} OCR data is missing.", modelPath);
        }
    }

    public Task<IReadOnlyList<string>> RecognizeLinesAsync(BitmapSource image, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(image);
        if ((long)image.PixelWidth * image.PixelHeight > MaxPixels)
            throw new InvalidOperationException("That image is too large. Select an area smaller than 50 megapixels.");
        var frozen = Normalize(image);
        return Task.Run<IReadOnlyList<string>>(() => Recognize(frozen, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<string> Recognize(BitmapSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        byte[]? bmpBytes = null;
        try
        {
            source.CopyPixels(pixels, stride, 0);
            cancellationToken.ThrowIfCancellationRequested();
            bmpBytes = CreateBmp(pixels, source.PixelWidth, source.PixelHeight, stride);
            using var pix = Pix.LoadFromMemory(bmpBytes);
            using var engine = new TesseractEngine(_dataPath, RecognitionLanguages, EngineMode.LstmOnly);
            using var page = engine.Process(pix, PageSegMode.Auto);
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.GetText() ?? string.Empty;
            return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        }
        finally
        {
            if (bmpBytes is not null) CryptographicOperations.ZeroMemory(bmpBytes);
            CryptographicOperations.ZeroMemory(pixels);
        }
    }

    private static BitmapSource Normalize(BitmapSource image)
    {
        BitmapSource result = image.Format == PixelFormats.Bgra32
            ? image
            : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        if (!result.IsFrozen) result.Freeze();
        return result;
    }

    private static byte[] CreateBmp(byte[] bgra, int width, int height, int stride)
    {
        const int header = 54;
        var dataSize = checked(stride * height);
        var bmp = new byte[checked(header + dataSize)];
        BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(0, 2), 0x4D42);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2, 4), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10, 4), header);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22, 4), -height);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(28, 2), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(34, 4), dataSize);
        Buffer.BlockCopy(bgra, 0, bmp, header, dataSize);
        return bmp;
    }

    public void Dispose() => _disposed = true;
}
