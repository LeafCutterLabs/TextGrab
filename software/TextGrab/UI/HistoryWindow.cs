using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TextGrab.Clipboard;
using TextGrab.History;
using TextGrab.Interop;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfImage = System.Windows.Controls.Image;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;

namespace TextGrab.UI;

public sealed class HistoryWindow : Window
{
    private readonly HistoryStore _store;
    private readonly ClipboardService _clipboard;
    private readonly WpfListBox _list;
    private readonly TextBlock _empty;
    private readonly TextBlock _status;
    private bool _allowClose;

    public HistoryWindow(HistoryStore store, ClipboardService clipboard)
    {
        _store = store;
        _clipboard = clipboard;
        Title = "TextGrab snip history";
        Icon = AppIcon.CreateImageSource();
        Width = 560;
        Height = 550;
        MinWidth = 430;
        MinHeight = 280;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        Topmost = true;

        _status = new TextBlock
        {
            Margin = new Thickness(12, 7, 12, 10),
            MinHeight = 20,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            TextWrapping = TextWrapping.Wrap
        };

        var heading = new TextBlock
        {
            Text = "Snip history",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var hint = new TextBlock
        {
            Text = "Enter copies · Delete removes · Esc closes",
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 3, 0, 0)
        };
        var titles = new StackPanel();
        titles.Children.Add(heading);
        titles.Children.Add(hint);

        var clear = new WpfButton
        {
            Content = "Clear All",
            Padding = new Thickness(12, 5, 12, 5),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        clear.Click += (_, _) =>
        {
            _store.Clear();
            _status.Text = "History cleared";
        };

        var header = new Grid { Margin = new Thickness(12, 10, 12, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(titles, 0);
        Grid.SetColumn(clear, 1);
        header.Children.Add(titles);
        header.Children.Add(clear);

        _list = new WpfListBox
        {
            Margin = new Thickness(10, 0, 10, 0),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            BorderBrush = System.Windows.SystemColors.ActiveBorderBrush
        };
        _list.MouseDoubleClick += async (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(_list, source) is WpfListBoxItem)
                await RestoreSelectedAsync();
        };
        _list.PreviewMouseWheel += (_, e) =>
        {
            MoveSelection(e.Delta < 0 ? 1 : -1);
            e.Handled = true;
        };

        _empty = new TextBlock
        {
            Text = "No snips yet. Capture a region or OCR a clipboard image to start this session's history.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            MaxWidth = 360,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24)
        };
        var body = new Grid();
        body.Children.Add(_list);
        body.Children.Add(_empty);

        var root = new Grid { Background = System.Windows.SystemColors.WindowBrush };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(_status, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(_status);
        Content = root;

        _store.Changed += StoreChanged;
        Deactivated += (_, _) => HidePicker();
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            HidePicker();
        };
        PreviewKeyDown += WindowKeyDown;
    }

    public void ShowPicker()
    {
        _status.Text = string.Empty;
        RebuildItems();
        if (!IsVisible) Show();
        PositionOnCursorMonitor();
        Activate();
        if (_list.Items.Count > 0) _list.Focus();
    }

    public void HidePicker()
    {
        _list.Items.Clear();
        if (IsVisible) Hide();
    }

    public void ForceClose()
    {
        _store.Changed -= StoreChanged;
        _list.Items.Clear();
        _allowClose = true;
        Close();
    }

    internal int ItemCountForTesting => _list.Items.Count;
    internal bool EmptyStateForTesting => _empty.Visibility == Visibility.Visible;
    internal void PrepareForTesting() => RebuildItems();

    private void StoreChanged(object? sender, EventArgs e)
    {
        if (IsVisible) RebuildItems();
    }

    private void RebuildItems()
    {
        var selectedId = SelectedEntry?.Id;
        _list.Items.Clear();
        foreach (var entry in _store.Entries)
        {
            _list.Items.Add(new WpfListBoxItem
            {
                Tag = entry,
                Content = CreateEntryContent(entry),
                Padding = new Thickness(6),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch
            });
        }

        _empty.Visibility = _list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _list.Visibility = _list.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_list.Items.Count == 0) return;

        var selected = _list.Items.Cast<WpfListBoxItem>()
            .FirstOrDefault(item => ((HistoryEntry)item.Tag).Id == selectedId);
        _list.SelectedItem = selected ?? _list.Items[0];
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private static FrameworkElement CreateEntryContent(HistoryEntry entry)
    {
        var image = new WpfImage
        {
            Source = entry.Thumbnail,
            Width = ThumbnailFactory.MaxWidth,
            Height = ThumbnailFactory.MaxHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var preview = new TextBlock
        {
            Text = CreateTextPreview(entry.Text),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 44,
            VerticalAlignment = VerticalAlignment.Center
        };
        var age = new TextBlock
        {
            Text = FormatAge(entry.CreatedAt, DateTimeOffset.Now),
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var text = new StackPanel { Margin = new Thickness(12, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(preview);
        text.Children.Add(age);

        var row = new Grid { MinHeight = 82 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(image, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(image);
        row.Children.Add(text);
        return row;
    }

    private async void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Home:
                SelectIndex(0);
                e.Handled = true;
                break;
            case Key.End:
                SelectIndex(_list.Items.Count - 1);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                await RestoreSelectedAsync();
                break;
            case Key.Delete:
                e.Handled = true;
                if (SelectedEntry is { } entry) _store.Remove(entry.Id);
                break;
            case Key.Escape:
                e.Handled = true;
                HidePicker();
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_list.Items.Count == 0) return;
        var current = _list.SelectedIndex < 0 ? 0 : _list.SelectedIndex;
        SelectIndex(Math.Clamp(current + delta, 0, _list.Items.Count - 1));
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _list.Items.Count) return;
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private HistoryEntry? SelectedEntry => (_list.SelectedItem as WpfListBoxItem)?.Tag as HistoryEntry;

    private async Task RestoreSelectedAsync()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        try
        {
            await _clipboard.SetTextAsync(entry.Text, CancellationToken.None);
            HidePicker();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void PositionOnCursorMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor)) return;
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var area = screen.WorkingArea;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var x = Math.Clamp(cursor.X + 18, area.Left, Math.Max(area.Left, area.Right - width));
        var y = Math.Clamp(cursor.Y + 18, area.Top, Math.Max(area.Top, area.Bottom - height));
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SwpNoActivate);
    }

    internal static string CreateTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text)) return "(empty text)";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        return string.Join(Environment.NewLine, lines.Take(2));
    }

    internal static string FormatAge(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var age = now - createdAt;
        if (age < TimeSpan.Zero || age < TimeSpan.FromMinutes(1)) return "Just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours} hr ago";
        return $"{(int)age.TotalDays} day{((int)age.TotalDays == 1 ? string.Empty : "s")} ago";
    }
}
