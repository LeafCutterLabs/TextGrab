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
    private readonly WpfCheckBox _control;
    private readonly WpfCheckBox _alt;
    private readonly WpfCheckBox _shift;
    private readonly WpfComboBox _key;
    private readonly WpfTextBlock _error;

    public HotkeySpec SelectedShortcut { get; private set; }

    public HotkeyWindow(HotkeySpec current)
    {
        SelectedShortcut = current;
        Title = "TextGrab keyboard shortcut";
        Icon = AppIcon.CreateImageSource();
        Width = 390;
        Height = 235;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        var heading = new WpfTextBlock
        {
            Text = "Choose the shortcut for region capture",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };

        _control = new WpfCheckBox { Content = "Ctrl", IsChecked = current.Control, Margin = new Thickness(0, 0, 14, 0) };
        _alt = new WpfCheckBox { Content = "Alt", IsChecked = current.Alt, Margin = new Thickness(0, 0, 14, 0) };
        _shift = new WpfCheckBox { Content = "Shift", IsChecked = current.Shift, Margin = new Thickness(0, 0, 18, 0) };
        _key = new WpfComboBox { Width = 105, ItemsSource = AvailableKeys, DisplayMemberPath = nameof(HotkeyChoice.Name) };
        _key.SelectedItem = AvailableKeys.FirstOrDefault(choice => choice.VirtualKey == current.VirtualKey) ?? AvailableKeys[0];

        var shortcutRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        shortcutRow.Children.Add(_control);
        shortcutRow.Children.Add(_alt);
        shortcutRow.Children.Add(_shift);
        shortcutRow.Children.Add(_key);

        _error = new WpfTextBlock
        {
            Foreground = System.Windows.Media.Brushes.Firebrick,
            Margin = new Thickness(0, 9, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        var note = new WpfTextBlock
        {
            Text = "The change lasts until TextGrab exits and creates no settings file.",
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        var apply = new WpfButton { Content = "Apply", IsDefault = true, MinWidth = 82, Padding = new Thickness(12, 5, 12, 5) };
        var cancel = new WpfButton { Content = "Cancel", IsCancel = true, MinWidth = 82, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
        apply.Click += ApplyClicked;
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(heading);
        panel.Children.Add(shortcutRow);
        panel.Children.Add(_error);
        panel.Children.Add(note);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        var choice = (HotkeyChoice?)_key.SelectedItem;
        if (choice is null || (_control.IsChecked != true && _alt.IsChecked != true && _shift.IsChecked != true))
        {
            _error.Text = "Select at least one modifier: Ctrl, Alt, or Shift.";
            return;
        }

        SelectedShortcut = new HotkeySpec(
            _control.IsChecked == true,
            _alt.IsChecked == true,
            _shift.IsChecked == true,
            choice.VirtualKey,
            choice.Name);
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

    private sealed record HotkeyChoice(string Name, uint VirtualKey);
}
