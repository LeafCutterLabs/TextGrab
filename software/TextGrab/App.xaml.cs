using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using TextGrab.Capture;
using TextGrab.Clipboard;
using TextGrab.Core;
using TextGrab.Interop;
using TextGrab.Ocr;
using TextGrab.UI;
using Forms = System.Windows.Forms;

namespace TextGrab;

public partial class App : System.Windows.Application
{
    private readonly OperationGate _gate = new();
    private readonly ClipboardService _clipboard = new();
    private readonly ScreenCaptureService _capture = new();
    private readonly CancellationTokenSource _lifetime = new();
    private TesseractOcrEngine? _ocr;
    private TrayController? _tray;
    private ResultWindow? _result;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureNativeLoading();

        if (e.Args.FirstOrDefault()?.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase) == true)
        {
            var reportPath = e.Args.Skip(1).FirstOrDefault();
            var code = await RunSmokeTestAsync(reportPath);
            Environment.ExitCode = code;
            Shutdown(code);
            return;
        }

        try
        {
            _ocr = new TesseractOcrEngine(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Describe(ex), "TextGrab could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        _tray = new TrayController();
        _result = new ResultWindow(_clipboard);
        _tray.CaptureRequested += CaptureRequested;
        _tray.ClipboardOcrRequested += ClipboardRequested;
        _tray.ShortcutChangeRequested += ShortcutChangeRequested;
        _tray.ExitRequested += ExitRequested;
        if (!_tray.HotkeyRegistered)
        {
            _tray.ShowHotkeyConflict();
            System.Windows.MessageBox.Show("Ctrl+Alt+T is already used by another app. TextGrab is still available from its tray icon.",
                "TextGrab hotkey unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShortcutChangeRequested(object? sender, EventArgs e)
    {
        if (_tray is null) return;
        var dialog = new HotkeyWindow(_tray.CurrentHotkey);
        if (dialog.ShowDialog() != true) return;

        if (_tray.TryChangeHotkey(dialog.SelectedShortcut, out var error))
        {
            _tray.ShowMessage("TextGrab shortcut changed", $"Region capture is now {dialog.SelectedShortcut.DisplayText}. This change lasts until TextGrab exits.");
            return;
        }

        System.Windows.MessageBox.Show(error, "TextGrab shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void ConfigureNativeLoading()
    {
        NativeMethods.SetDefaultDllDirectories(NativeMethods.LoadLibrarySearchDefaultDirs | NativeMethods.LoadLibrarySearchUserDirs);
        NativeMethods.AddDllDirectory(AppContext.BaseDirectory);
        var packageNative = Path.Combine(AppContext.BaseDirectory, "x64");
        if (Directory.Exists(packageNative)) NativeMethods.AddDllDirectory(packageNative);
    }

    private async void CaptureRequested(object? sender, EventArgs e)
    {
        using var operation = _gate.TryEnter();
        if (operation is null)
        {
            _tray?.ShowMessage("TextGrab is busy", "Finish or cancel the current capture first.");
            return;
        }
        try
        {
            _result?.ClearAndHide();
            var selector = new RegionSelector();
            var rect = await selector.SelectAsync(_lifetime.Token);
            if (rect is null) return;
            await Task.Delay(80, _lifetime.Token);
            var image = _capture.Capture(rect.Value);
            await RecognizeAndShowAsync(image, _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void ClipboardRequested(object? sender, EventArgs e)
    {
        using var operation = _gate.TryEnter();
        if (operation is null)
        {
            _tray?.ShowMessage("TextGrab is busy", "Finish or cancel the current operation first.");
            return;
        }
        try
        {
            _result?.ClearAndHide();
            var image = await _clipboard.GetImageAsync(_lifetime.Token);
            await RecognizeAndShowAsync(image, _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task RecognizeAndShowAsync(BitmapSource image, CancellationToken cancellationToken)
    {
        if (_ocr is null) throw new InvalidOperationException("The OCR engine is unavailable.");
        var lines = await _ocr.RecognizeLinesAsync(image, cancellationToken);
        var raw = string.Join("\r\n", lines);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("No text was found in that image. Try a larger or sharper selection.");
        _result ??= new ResultWindow(_clipboard);
        _result.SetResult(raw);
        await _result.CopyCurrentTextAsync("Copied automatically");
    }

    private void ShowError(Exception exception)
    {
        var message = Describe(exception);
        _tray?.ShowMessage("TextGrab", message, Forms.ToolTipIcon.Error);
        System.Windows.MessageBox.Show(message, "TextGrab", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string Describe(Exception exception) => exception switch
    {
        FileNotFoundException => "English or Spanish OCR data is missing. Extract the complete TextGrab ZIP again so both language files are in the tessdata folder.",
        DllNotFoundException => "A native OCR component is missing. Extract the complete TextGrab ZIP again.",
        BadImageFormatException => "An OCR component has the wrong architecture. Extract the complete x64 TextGrab ZIP again.",
        _ => exception.Message
    };

    private void ExitRequested(object? sender, EventArgs e)
    {
        _lifetime.Cancel();
        _result?.ForceClose();
        foreach (Window window in Windows.Cast<Window>().ToArray()) window.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _lifetime.Cancel();
        _tray?.Dispose();
        _ocr?.Dispose();
        _lifetime.Dispose();
        base.OnExit(e);
    }

    private static async Task<int> RunSmokeTestAsync(string? reportPath)
    {
        var report = new SmokeReport();
        try
        {
            using var engine = new TesseractOcrEngine(Path.Combine(AppContext.BaseDirectory, "tessdata"));
            var image = CreateSmokeImage();
            var sw = Stopwatch.StartNew();
            var lines = await engine.RecognizeLinesAsync(image, CancellationToken.None);
            sw.Stop();
            report.Text = string.Join(" ", lines).Trim();
            report.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            report.Success = report.Text.Contains("TEXTGRAB", StringComparison.OrdinalIgnoreCase)
                && report.Text.Contains("12345", StringComparison.Ordinal)
                && report.Text.Contains("ESPANOL", StringComparison.OrdinalIgnoreCase)
                && report.Text.Contains("67890", StringComparison.Ordinal);
            report.NativeModules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Where(m => m.ModuleName.Contains("tesseract", StringComparison.OrdinalIgnoreCase)
                    || m.ModuleName.Contains("leptonica", StringComparison.OrdinalIgnoreCase)
                    || m.ModuleName.StartsWith("vcruntime140", StringComparison.OrdinalIgnoreCase)
                    || m.ModuleName.StartsWith("msvcp140", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.FileName).OrderBy(p => p).ToArray();
            if (!report.Success) report.Error = "OCR output did not contain the expected text.";
        }
        catch (Exception ex)
        {
            report.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (!string.IsNullOrWhiteSpace(reportPath)) await File.WriteAllTextAsync(Path.GetFullPath(reportPath), json);
        Console.WriteLine(json);
        return report.Success ? 0 : 1;
    }

    private static BitmapSource CreateSmokeImage()
    {
        const int width = 720;
        const int height = 245;
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.White);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            using var font = new System.Drawing.Font("Arial", 58, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            graphics.DrawString("TEXTGRAB 12345", font, System.Drawing.Brushes.Black, new System.Drawing.PointF(24, 24), System.Drawing.StringFormat.GenericTypographic);
            graphics.DrawString("ESPANOL 67890", font, System.Drawing.Brushes.Black, new System.Drawing.PointF(24, 116), System.Drawing.StringFormat.GenericTypographic);
        }
        var bounds = new System.Drawing.Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var pixels = new byte[checked(stride * height)];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var source = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private sealed class SmokeReport
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        public long ElapsedMilliseconds { get; set; }
        public string[] NativeModules { get; set; } = [];
        public string? Error { get; set; }
    }
}
