using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextGrab.Clipboard;
using TextGrab.Core;
using TextGrab.History;
using TextGrab.UI;

if (args is ["--render-ui", var outputPath])
{
    RunOnSta(() => RenderResultWindow(outputPath));
    Console.WriteLine($"Rendered result UI to {Path.GetFullPath(outputPath)}");
    return 0;
}

if (args is ["--render-history-ui", var historyOutputPath])
{
    RunOnSta(() => RenderHistoryWindow(historyOutputPath));
    Console.WriteLine($"Rendered history UI to {Path.GetFullPath(historyOutputPath)}");
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
    ("history is newest-first bounded editable and clearable", TestHistoryStore),
    ("history thumbnails are frozen and size capped", TestThumbnailFactory),
    ("history picker builds and releases rows", TestHistoryWindow),
    ("result editor is editable clean-only and purges undo", TestResultEditor),
    ("embedded icons load for WPF and tray", TestIcons),
    ("shortcut dialog exposes modifiers and supported keys", TestHotkeyDialog),
    ("default shortcut is Ctrl+Alt+T", () => Equal("Ctrl+Alt+T", HotkeySpec.Default.DisplayText)),
    ("default history shortcut is Ctrl+Alt+V", () => Equal("Ctrl+Alt+V", HotkeySpec.HistoryDefault.DisplayText)),
    ("history preview and age labels are compact", TestHistoryLabels),
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

static void TestHistoryStore()
{
    var store = new HistoryStore();
    var thumbnail = CreateTestImage(4, 4);
    for (var index = 0; index < HistoryStore.Capacity + 2; index++)
        store.Add($"entry {index}", thumbnail, new DateTimeOffset(2026, 1, 1, 0, index, 0, TimeSpan.Zero));

    Equal(HistoryStore.Capacity, store.Entries.Count);
    Equal("entry 26", store.Entries[0].Text);
    Equal("entry 2", store.Entries[^1].Text);
    var entry = store.Entries[5];
    True(store.UpdateText(entry.Id, "edited"));
    Equal("edited", entry.Text);
    True(store.Remove(entry.Id));
    Equal(HistoryStore.Capacity - 1, store.Entries.Count);
    store.Clear();
    Equal(0, store.Entries.Count);
}

static void TestThumbnailFactory()
{
    var wide = ThumbnailFactory.Create(CreateTestImage(400, 100));
    Equal(120, wide.PixelWidth);
    Equal(30, wide.PixelHeight);
    True(wide.IsFrozen);

    var tall = ThumbnailFactory.Create(CreateTestImage(100, 400));
    Equal(18, tall.PixelWidth);
    Equal(72, tall.PixelHeight);
    True(tall.IsFrozen);
}

static void TestHistoryWindow() => RunOnSta(() =>
{
    var store = new HistoryStore();
    var window = new HistoryWindow(store, new ClipboardService());
    window.PrepareForTesting();
    Equal(0, window.ItemCountForTesting);
    True(window.EmptyStateForTesting);

    store.Add("one", CreateTestImage(20, 10));
    store.Add("two", CreateTestImage(20, 10));
    window.PrepareForTesting();
    Equal(2, window.ItemCountForTesting);
    True(!window.EmptyStateForTesting);
    window.HidePicker();
    Equal(0, window.ItemCountForTesting);
    window.ForceClose();
});

static void TestResultEditor() => RunOnSta(() =>
{
    var history = new HistoryStore();
    var window = new ResultWindow(new ClipboardService(), history);
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

    var entry = history.Add("tracked", CreateTestImage(4, 4));
    window.SetResult(entry);
    editor.Select(editor.Text.Length, 0);
    editor.SelectedText = " edit";
    Equal("tracked edit", entry.Text);
    Equal(1, history.Entries.Count);

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
    var window = new HotkeyWindow(HotkeySpec.Default, HotkeySpec.HistoryDefault)
    {
        Opacity = 0,
        ShowInTaskbar = false,
        Left = -10000
    };
    window.Show();
    Equal("TextGrab keyboard shortcuts", window.Title);
    True(window.Icon is not null);
    var content = (DependencyObject)window.Content;
    var modifiers = FindVisualChildren<CheckBox>(content);
    Equal(6, modifiers.Count);
    Equal(2, modifiers.Count(box => Equals(box.Content, "Ctrl") && box.IsChecked == true));
    Equal(2, modifiers.Count(box => Equals(box.Content, "Alt") && box.IsChecked == true));
    Equal(2, modifiers.Count(box => Equals(box.Content, "Shift") && box.IsChecked == false));
    var keys = FindVisualChildren<ComboBox>(content);
    Equal(2, keys.Count);
    True(keys.All(key => key.Items.Count == 47));
    Equal("T", keys[0].Text);
    Equal("V", keys[1].Text);
    window.Close();
});

static void TestHistoryLabels()
{
    Equal("first\r\nsecond", HistoryWindow.CreateTextPreview("first\r\nsecond\r\nthird"));
    Equal("(empty text)", HistoryWindow.CreateTextPreview(string.Empty));
    var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    Equal("Just now", HistoryWindow.FormatAge(now.AddSeconds(-20), now));
    Equal("7 min ago", HistoryWindow.FormatAge(now.AddMinutes(-7), now));
    Equal("2 hr ago", HistoryWindow.FormatAge(now.AddHours(-2), now));
    Equal("1 day ago", HistoryWindow.FormatAge(now.AddDays(-1), now));
}

static void RenderResultWindow(string outputPath)
{
    var window = new ResultWindow(new ClipboardService(), new HistoryStore());
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

static void RenderHistoryWindow(string outputPath)
{
    var store = new HistoryStore();
    var now = DateTimeOffset.Now;
    store.Add("Support ticket 4812\r\nCustomer cannot sign in after resetting the password.", CreateColoredTestImage(320, 180, 80, 120, 210), now.AddMinutes(-18));
    store.Add("Invoice 1042\r\nWidget A    3 × $12.00\r\nTotal: $44.50", CreateColoredTestImage(320, 180, 235, 235, 235), now.AddMinutes(-5));
    store.Add("TextGrab keeps session history locally in memory.", CreateColoredTestImage(320, 180, 190, 225, 175), now.AddSeconds(-20));
    var window = new HistoryWindow(store, new ClipboardService());
    window.PrepareForTesting();
    var content = (FrameworkElement)window.Content;
    const int width = 560;
    const int height = 550;
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

static BitmapSource CreateTestImage(int width, int height)
{
    var stride = checked(width * 4);
    var pixels = new byte[checked(stride * height)];
    var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    image.Freeze();
    return image;
}

static BitmapSource CreateColoredTestImage(int width, int height, byte red, byte green, byte blue)
{
    var stride = checked(width * 4);
    var pixels = new byte[checked(stride * height)];
    for (var index = 0; index < pixels.Length; index += 4)
    {
        pixels[index] = blue;
        pixels[index + 1] = green;
        pixels[index + 2] = red;
        pixels[index + 3] = 255;
    }
    var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    image.Freeze();
    return image;
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
