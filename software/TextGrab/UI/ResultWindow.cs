using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TextGrab.Clipboard;
using TextGrab.Core;
using TextGrab.History;
using TextGrab.Interop;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TextGrab.UI;

public sealed class ResultWindow : Window
{
    private readonly WpfTextBox _text;
    private readonly TextBlock _status;
    private readonly ClipboardService _clipboard;
    private readonly HistoryStore _history;

    private bool _allowClose;
    private bool _replacingText;
    private HistoryEntry? _activeEntry;

    public ResultWindow(ClipboardService clipboard, HistoryStore history)
    {
        _clipboard = clipboard;
        _history = history;
        Title = "TextGrab result";
        Icon = AppIcon.CreateImageSource();
        Width = 620;
        Height = 390;
        MinWidth = 360;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var copy = new WpfButton { Content = "Copy All", Padding = new Thickness(14, 5, 14, 5), IsDefault = false };
        _status = new TextBlock { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var guidance = new TextBlock
        {
            Text = "Edit text, then copy.",
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = System.Windows.SystemColors.GrayTextBrush
        };
        var bar = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 9, 10, 8) };
        bar.Children.Add(guidance);
        bar.Children.Add(copy);
        bar.Children.Add(_status);

        _text = new WpfTextBox
        {
            Text = string.Empty,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(10, 0, 10, 10),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 15
        };
        var grid = new Grid { Background = System.Windows.SystemColors.WindowBrush };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(bar, 0);
        Grid.SetRow(_text, 1);
        grid.Children.Add(bar);
        grid.Children.Add(_text);
        Content = grid;

        _text.TextChanged += (_, _) =>
        {
            _status.Text = string.Empty;
            if (!_replacingText && _activeEntry is not null)
                _history.UpdateText(_activeEntry.Id, _text.Text);
        };
        copy.Click += async (_, _) => await CopyCurrentTextAsync();
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            ClearAndHide();
        };
        Loaded += (_, _) => _text.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    public void SetResult(HistoryEntry entry)
    {
        _activeEntry = null;
        ReplaceTextAndClearUndo(entry.Text);
        _activeEntry = entry;
        _status.Text = string.Empty;
        if (!IsVisible) Show();
        PositionOnCursorMonitor();
        Activate();
        _text.Focus();
    }

    public void ClearAndHide()
    {
        _activeEntry = null;
        ReplaceTextAndClearUndo(string.Empty);
        _status.Text = string.Empty;
        Hide();
    }

    internal WpfTextBox EditorForTesting => _text;
    internal string StatusForTesting => _status.Text;
    internal void SetStatusForTesting(string value) => _status.Text = value;

    internal void LoadResultText(string raw)
    {
        _activeEntry = null;
        ReplaceTextAndClearUndo(TextCleanup.Clean(raw));
        _status.Text = string.Empty;
    }

    private void ReplaceTextAndClearUndo(string value)
    {
        // Disabling undo clears WPF's private undo manager, including references
        // to text from the prior capture. Re-enable it for normal current-result edits.
        _replacingText = true;
        try
        {
            _text.IsUndoEnabled = false;
            _text.Text = value;
            _text.IsUndoEnabled = true;
        }
        finally
        {
            _replacingText = false;
        }
    }

    public void ForceClose()
    {
        ClearAndHide();
        _allowClose = true;
        Close();
    }

    public void PositionOnCursorMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor)) return;
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var area = screen.WorkingArea;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var x = Math.Clamp(cursor.X + 18, area.Left, Math.Max(area.Left, area.Right - width));
        var y = Math.Clamp(cursor.Y + 18, area.Top, Math.Max(area.Top, area.Bottom - height));
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SwpNoActivate);
    }

    public async Task CopyCurrentTextAsync(string successMessage = "Copied")
    {
        try
        {
            await _clipboard.SetTextAsync(_text.Text, CancellationToken.None);
            _status.Text = successMessage;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }
}
