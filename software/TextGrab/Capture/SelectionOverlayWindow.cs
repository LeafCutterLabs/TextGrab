using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TextGrab.Core;
using TextGrab.Interop;

namespace TextGrab.Capture;

internal sealed class SelectionOverlayWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private readonly PhysicalRect _bounds;
    private readonly SelectionAdorner _adorner;

    public event EventHandler? SelectionStarted;
    public event EventHandler? SelectionCancelled;

    public SelectionOverlayWindow(PhysicalRect bounds)
    {
        _bounds = bounds;
        _adorner = new SelectionAdorner(bounds);
        Content = _adorner;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.Cross;
        MouseLeftButtonDown += (_, _) => SelectionStarted?.Invoke(this, EventArgs.Empty);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) SelectionCancelled?.Invoke(this, EventArgs.Empty);
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(hwnd, HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, NativeMethods.SwpNoActivate);
        };
    }

    public void SetSelection(PhysicalRect selection) => _adorner.SetSelection(selection);
}
