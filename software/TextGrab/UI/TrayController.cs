using System.Drawing;
using System.Windows.Interop;
using TextGrab.Interop;
using TextGrab.Core;
using Forms = System.Windows.Forms;

namespace TextGrab.UI;

public sealed class TrayController : IDisposable
{
    private const int HotkeyId = 0x5447;
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly HwndSource _messageWindow;
    private readonly Forms.ToolStripMenuItem _shortcutItem;
    private bool _disposed;
    private bool _hotkeyRegistered;
    private HotkeySpec _currentHotkey = HotkeySpec.Default;

    public event EventHandler? CaptureRequested;
    public event EventHandler? ClipboardOcrRequested;
    public event EventHandler? ShortcutChangeRequested;
    public event EventHandler? ExitRequested;
    public bool HotkeyRegistered => _hotkeyRegistered;
    public HotkeySpec CurrentHotkey => _currentHotkey;

    public TrayController()
    {
        _ownedIcon = AppIcon.CreateDrawingIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture Region", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("OCR Clipboard Image", null, (_, _) => ClipboardOcrRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _shortcutItem = new Forms.ToolStripMenuItem();
        _shortcutItem.Click += (_, _) => ShortcutChangeRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_shortcutItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _icon = new Forms.NotifyIcon
        {
            Icon = _ownedIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);

        _messageWindow = new HwndSource(new HwndSourceParameters("TextGrabHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        _messageWindow.AddHook(WindowProc);
        _hotkeyRegistered = Register(_currentHotkey);
        UpdateShortcutLabels();
    }

    public bool TryChangeHotkey(HotkeySpec requested, out string error)
    {
        error = string.Empty;
        if (requested == _currentHotkey && _hotkeyRegistered) return true;

        var previous = _currentHotkey;
        var previousWasRegistered = _hotkeyRegistered;
        if (previousWasRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _hotkeyRegistered = false;

        if (Register(requested))
        {
            _currentHotkey = requested;
            _hotkeyRegistered = true;
            UpdateShortcutLabels();
            return true;
        }

        if (previousWasRegistered) _hotkeyRegistered = Register(previous);
        UpdateShortcutLabels();
        error = previousWasRegistered && _hotkeyRegistered
            ? $"{requested.DisplayText} is already used by Windows or another app. The shortcut remains {previous.DisplayText}."
            : $"{requested.DisplayText} is already used by Windows or another app. Region capture remains available from the tray menu.";
        return false;
    }

    private bool Register(HotkeySpec shortcut)
    {
        var modifiers = NativeMethods.ModNoRepeat;
        if (shortcut.Control) modifiers |= NativeMethods.ModControl;
        if (shortcut.Alt) modifiers |= NativeMethods.ModAlt;
        if (shortcut.Shift) modifiers |= NativeMethods.ModShift;
        return NativeMethods.RegisterHotKey(_messageWindow.Handle, HotkeyId, modifiers, shortcut.VirtualKey);
    }

    private void UpdateShortcutLabels()
    {
        var shortcut = _currentHotkey.DisplayText;
        _shortcutItem.Text = $"Keyboard shortcut: {shortcut}…";
        _icon.Text = $"TextGrab — {shortcut}";
    }

    public void ShowHotkeyConflict()
    {
        _icon.BalloonTipTitle = "TextGrab hotkey unavailable";
        _icon.BalloonTipText = "Ctrl+Alt+T is already used by another app. Use the TextGrab tray menu to capture.";
        _icon.ShowBalloonTip(6000);
    }

    public void ShowMessage(string title, string message, Forms.ToolTipIcon kind = Forms.ToolTipIcon.Info)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = kind;
        _icon.ShowBalloonTip(5000);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hotkeyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _messageWindow.RemoveHook(WindowProc);
        _messageWindow.Dispose();
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _ownedIcon.Dispose();
    }
}
