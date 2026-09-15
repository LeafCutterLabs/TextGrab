using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextGrab.Core;
using TextGrab.Interop;

namespace TextGrab.Capture;

public sealed class ScreenCaptureService
{
    public BitmapSource Capture(PhysicalRect rect)
    {
        if (rect.IsEmpty) throw new ArgumentException("Select an area with non-zero width and height.", nameof(rect));
        if ((long)rect.Width * rect.Height > Ocr.TesseractOcrEngine.MaxPixels)
            throw new InvalidOperationException("That screen area is too large. Select an area smaller than 50 megapixels.");
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw new Win32Exception("Could not access the desktop image.");
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var oldObject = IntPtr.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a capture buffer.");
            var info = new NativeMethods.BitmapInfo
            {
                Header = new NativeMethods.BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                    Width = rect.Width,
                    Height = -rect.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = NativeMethods.BiRgb,
                    SizeImage = checked((uint)(rect.Width * rect.Height * 4))
                }
            };
            bitmap = NativeMethods.CreateDIBSection(screenDc, ref info, NativeMethods.DibRgbColors, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate the capture buffer.");
            oldObject = NativeMethods.SelectObject(memoryDc, bitmap);
            if (!NativeMethods.BitBlt(memoryDc, 0, 0, rect.Width, rect.Height, screenDc, rect.X, rect.Y, NativeMethods.Srccopy | NativeMethods.CaptureBlt))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not capture that screen area.");

            var stride = checked(rect.Width * 4);
            var image = BitmapSource.Create(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null, bits, checked(stride * rect.Height), stride);
            image.Freeze();
            return image;
        }
        finally
        {
            if (oldObject != IntPtr.Zero && memoryDc != IntPtr.Zero) NativeMethods.SelectObject(memoryDc, oldObject);
            if (bitmap != IntPtr.Zero) NativeMethods.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
