using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextGrab.Clipboard;
using TextGrab.Core;
using TextGrab.UI;

if (args is ["--render-ui", var outputPath])
{
    RunOnSta(() => RenderResultWindow(outputPath));
    Console.WriteLine($"Rendered result UI to {Path.GetFullPath(outputPath)}");
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("cleanup normalizes line endings", () => Equal("a\r\nb\r\nc", TextCleanup.Clean("a\rb\nc"))),
    ("cleanup trims trailing horizontal whitespace", () => Equal("a\r\n b", TextCleanup.Clean("a  \n b\t"))),
    ("cleanup preserves indentation while removing internal blank lines", () => Equal("  a\r\n b", TextCleanup.Clean("\n  a\n\n b\n"))),
    ("cleanup removes every whitespace-only line", () => Equal("a\r\nb", TextCleanup.Clean(" \t\r\n\r\na\r\n\t\nb\n"))),
    ("cleanup handles empty input", () => Equal(string.Empty, TextCleanup.Clean(null))),
    ("geometry supports negative coordinates", () => Equal(new PhysicalRect(-400, -50, 500, 250), PhysicalRect.FromPoints(new System.Drawing.Point(100, 200), new System.Drawing.Point(-400, -50)))),
    ("geometry intersects monitors", () => Equal(new PhysicalRect(0, 10, 100, 90), new PhysicalRect(-100, 10, 200, 100).Intersect(new PhysicalRect(0, 0, 200, 100)))),
    ("geometry returns empty disjoint intersection", () => True(new PhysicalRect(-100, 0, 50, 50).Intersect(new PhysicalRect(0, 0, 50, 50)).IsEmpty)),
    ("operation gate excludes concurrent work", TestGate),
    ("operation gate releases idempotently", TestGateRelease),
    ("result editor is editable clean-only and purges undo", TestResultEditor),
    ("embedded icons load for WPF and tray", TestIcons),
    ("shortcut dialog exposes modifiers and supported keys", TestHotkeyDialog),
    ("default shortcut is Ctrl+Alt+T", () => Equal("Ctrl+Alt+T", HotkeySpec.Default.DisplayText)),
    ("shortcut formatting follows modifier order", () => Equal("Ctrl+Shift+F5", new HotkeySpec(true, false, true, 0x74, "F5").DisplayText))
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex}");
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static void TestGate()
{
    var gate = new OperationGate();
    using var first = gate.TryEnter();
    True(first is not null);
    True(gate.IsBusy);
    True(gate.TryEnter() is null);
}

static void TestGateRelease()
{
    var gate = new OperationGate();
    var first = gate.TryEnter() ?? throw new Exception("Could not enter gate");
    first.Dispose();
    first.Dispose();
    using var second = gate.TryEnter();
    True(second is not null);
}

static void TestResultEditor() => RunOnSta(() =>
{
    var window = new ResultWindow(new ClipboardService());
    var editor = window.EditorForTesting;
    window.Opacity = 0;
    window.ShowInTaskbar = false;
    window.Left = -10000;
    window.Show();

    window.LoadResultText("  first  \nline\t");
    Equal("  first\r\nline", editor.Text);
    True(!editor.IsReadOnly);
    True(FindVisualChildren<RadioButton>((DependencyObject)window.Content).Count == 0);

    editor.Select(editor.Text.Length, 0);
    editor.SelectedText = " edited";
    Equal("  first\r\nline edited", editor.Text);
    True(editor.CanUndo);

    window.SetStatusForTesting("Copied");
    editor.Select(editor.Text.Length, 0);
    editor.SelectedText = "!";
    Equal(string.Empty, window.StatusForTesting);

    window.LoadResultText("second");
    True(!editor.CanUndo);
    editor.Undo();
    Equal("second", editor.Text);

    window.ClearAndHide();
    Equal(string.Empty, editor.Text);
    True(!editor.CanUndo);
    editor.Undo();
    Equal(string.Empty, editor.Text);
    window.ForceClose();
});

static void TestIcons() => RunOnSta(() =>
{
    var image = AppIcon.CreateImageSource();
    True(image.IsFrozen);
    Equal(256, image.PixelWidth);
    Equal(256, image.PixelHeight);
    using var icon = AppIcon.CreateDrawingIcon();
    True(icon.Width > 0 && icon.Height > 0);
});

static void TestHotkeyDialog() => RunOnSta(() =>
{
    var window = new HotkeyWindow(HotkeySpec.Default)
    {
        Opacity = 0,
        ShowInTaskbar = false,
        Left = -10000
    };
    window.Show();
    Equal("TextGrab keyboard shortcut", window.Title);
    True(window.Icon is not null);
    var content = (DependencyObject)window.Content;
    var modifiers = FindVisualChildren<CheckBox>(content);
    Equal(3, modifiers.Count);
    True(modifiers.Single(box => Equals(box.Content, "Ctrl")).IsChecked == true);
    True(modifiers.Single(box => Equals(box.Content, "Alt")).IsChecked == true);
    True(modifiers.Single(box => Equals(box.Content, "Shift")).IsChecked == false);
    var key = FindVisualChildren<ComboBox>(content).Single();
    Equal(47, key.Items.Count);
    Equal("T", key.Text);
    window.Close();
});

static void RenderResultWindow(string outputPath)
{
    var window = new ResultWindow(new ClipboardService());
    window.LoadResultText("Invoice 1042\r\n\r\nWidget A    3 × $12.00\r\nWidget B    1 × $8.50\r\n\r\nTotal: $44.50\r\n\r\nEdited and ready to copy.");
    var content = (FrameworkElement)window.Content;
    const int width = 620;
    const int height = 390;
    content.Measure(new Size(width, height));
    content.Arrange(new Rect(0, 0, width, height));
    content.UpdateLayout();
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(content);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    using var stream = File.Create(fullPath);
    encoder.Save(stream);
    window.ForceClose();
}

static IReadOnlyList<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
{
    var found = new List<T>();
    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T match) found.Add(match);
        found.AddRange(FindVisualChildren<T>(child));
    }
    return found;
}

static void RunOnSta(Action action)
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try { action(); }
        catch (Exception ex) { failure = ex; }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null) throw new Exception("STA UI check failed", failure);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected <{expected}>; got <{actual}>");
}

static void True(bool value)
{
    if (!value) throw new Exception("Expected true");
}
