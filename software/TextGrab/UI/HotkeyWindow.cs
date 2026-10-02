using System.Windows;
using System.Windows.Controls;
using TextGrab.Core;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace TextGrab.UI;

public sealed class HotkeyWindow : Window
{
    private readonly ShortcutEditor _capture;
    private readonly ShortcutEditor _history;
    private readonly WpfTextBlock _error;

    public HotkeySpec CaptureShortcut { get; private set; }
    public HotkeySpec HistoryShortcut { get; private set; }

    public HotkeyWindow(HotkeySpec capture, HotkeySpec history)
    {
        CaptureShortcut = capture;
        HistoryShortcut = history;
        Title = "TextGrab keyboard shortcuts";
        Icon = AppIcon.CreateImageSource();
        Width = 430;
        Height = 310;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        var heading = new WpfTextBlock
        {
            Text = "Choose keyboard shortcuts",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };

        _capture = new ShortcutEditor("Capture region", capture);
        _history = new ShortcutEditor("Snip history", history);

        _error = new WpfTextBlock
        {
            Foreground = System.Windows.Media.Brushes.Firebrick,
            Margin = new Thickness(0, 9, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        var note = new WpfTextBlock
        {
            Text = "Changes last until TextGrab exits and create no settings file.",
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        var apply = new WpfButton { Content = "Apply", IsDefault = true, MinWidth = 82, Padding = new Thickness(12, 5, 12, 5) };
        var cancel = new WpfButton { Content = "Cancel", IsCancel = true, MinWidth = 82, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
        apply.Click += ApplyClicked;
        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(heading);
        panel.Children.Add(_capture.Content);
        panel.Children.Add(_history.Content);
        panel.Children.Add(_error);
        panel.Children.Add(note);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!_capture.TryGetShortcut(out var capture) || !_history.TryGetShortcut(out var history))
        {
            _error.Text = "Select at least one modifier for each shortcut.";
            return;
        }
        if (capture == history)
        {
            _error.Text = "Capture region and Snip history must use different shortcuts.";
            return;
        }

        CaptureShortcut = capture;
        HistoryShortcut = history;
        DialogResult = true;
    }

    private static IReadOnlyList<HotkeyChoice> AvailableKeys { get; } = CreateChoices();

    private static IReadOnlyList<HotkeyChoice> CreateChoices()
    {
        var choices = new List<HotkeyChoice>();
        for (var key = 'A'; key <= 'Z'; key++) choices.Add(new(key.ToString(), key));
        for (var key = '0'; key <= '9'; key++) choices.Add(new(key.ToString(), key));
        for (var index = 1; index <= 11; index++) choices.Add(new($"F{index}", (uint)(0x70 + index - 1)));
        return choices;
    }

    private sealed class ShortcutEditor
    {
        private readonly WpfCheckBox _control;
        private readonly WpfCheckBox _alt;
        private readonly WpfCheckBox _shift;
        private readonly WpfComboBox _key;

        internal ShortcutEditor(string label, HotkeySpec current)
        {
            _control = new WpfCheckBox { Content = "Ctrl", IsChecked = current.Control, Margin = new Thickness(0, 0, 12, 0) };
            _alt = new WpfCheckBox { Content = "Alt", IsChecked = current.Alt, Margin = new Thickness(0, 0, 12, 0) };
            _shift = new WpfCheckBox { Content = "Shift", IsChecked = current.Shift, Margin = new Thickness(0, 0, 14, 0) };
            _key = new WpfComboBox { Width = 92, ItemsSource = AvailableKeys, DisplayMemberPath = nameof(HotkeyChoice.Name) };
            _key.SelectedItem = AvailableKeys.FirstOrDefault(choice => choice.VirtualKey == current.VirtualKey) ?? AvailableKeys[0];

            var controls = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            controls.Children.Add(_control);
            controls.Children.Add(_alt);
            controls.Children.Add(_shift);
            controls.Children.Add(_key);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var title = new WpfTextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title, 0);
            Grid.SetColumn(controls, 1);
            row.Children.Add(title);
            row.Children.Add(controls);
            Content = row;
        }

        internal FrameworkElement Content { get; }

        internal bool TryGetShortcut(out HotkeySpec shortcut)
        {
            var choice = (HotkeyChoice?)_key.SelectedItem;
            shortcut = choice is null
                ? HotkeySpec.Default
                : new HotkeySpec(
                    _control.IsChecked == true,
                    _alt.IsChecked == true,
                    _shift.IsChecked == true,
                    choice.VirtualKey,
                    choice.Name);
            return choice is not null && shortcut.HasModifier;
        }
    }

    private sealed record HotkeyChoice(string Name, uint VirtualKey);
}
