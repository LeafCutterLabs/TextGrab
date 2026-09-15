using System.Windows;
using System.Windows.Media;
using TextGrab.Core;

namespace TextGrab.Capture;

internal sealed class SelectionAdorner : FrameworkElement
{
    private PhysicalRect _selection;
    private readonly PhysicalRect _monitor;

    public SelectionAdorner(PhysicalRect monitor) => _monitor = monitor;

    public void SetSelection(PhysicalRect selection)
    {
        _selection = selection;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(92, 0, 0, 0)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        var local = _selection.Intersect(_monitor);
        if (local.IsEmpty) return;
        var sx = ActualWidth / _monitor.Width;
        var sy = ActualHeight / _monitor.Height;
        var rect = new Rect((local.X - _monitor.X) * sx, (local.Y - _monitor.Y) * sy, local.Width * sx, local.Height * sy);
        dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 255, 255, 255)), new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 2), rect);
    }
}
