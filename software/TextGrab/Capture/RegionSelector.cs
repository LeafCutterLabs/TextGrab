using System.Drawing;
using System.Windows.Threading;
using TextGrab.Core;
using TextGrab.Interop;
using Forms = System.Windows.Forms;

namespace TextGrab.Capture;

public sealed class RegionSelector
{
    private readonly List<SelectionOverlayWindow> _windows = [];
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private TaskCompletionSource<PhysicalRect?>? _completion;
    private Point _start;
    private bool _dragging;

    public RegionSelector()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, Poll, Dispatcher.CurrentDispatcher);
        _timer.Stop();
    }

    public async Task<PhysicalRect?> SelectAsync(CancellationToken cancellationToken)
    {
        if (_completion is not null) throw new InvalidOperationException("A selection is already active.");
        _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var b = screen.Bounds;
            var window = new SelectionOverlayWindow(new PhysicalRect(b.X, b.Y, b.Width, b.Height));
            window.SelectionStarted += Begin;
            window.SelectionCancelled += Cancel;
            _windows.Add(window);
            window.Show();
        }
        _windows.FirstOrDefault()?.Activate();
        using var registration = cancellationToken.Register(() => _dispatcher.BeginInvoke(Cancel));
        return await _completion.Task;
    }

    private void Begin(object? sender, EventArgs e)
    {
        if (_dragging || !NativeMethods.GetCursorPos(out var p)) return;
        _start = new Point(p.X, p.Y);
        _dragging = true;
        _timer.Start();
    }

    private void Poll(object? sender, EventArgs e)
    {
        if (!_dragging || !NativeMethods.GetCursorPos(out var p)) return;
        var current = new Point(p.X, p.Y);
        var selection = PhysicalRect.FromPoints(_start, current);
        foreach (var window in _windows) window.SetSelection(selection);
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) == 0)
            Complete(selection.IsEmpty ? null : selection);
    }

    private void Cancel(object? sender = null, EventArgs? e = null) => Complete(null);

    private void Complete(PhysicalRect? result)
    {
        if (_completion is null) return;
        _dragging = false;
        _timer.Stop();
        foreach (var window in _windows)
        {
            window.SelectionStarted -= Begin;
            window.SelectionCancelled -= Cancel;
            window.Close();
        }
        _windows.Clear();
        var completion = _completion;
        _completion = null;
        completion.TrySetResult(result);
    }
}
