using System.Drawing;
using System.Windows.Interop;
using TextGrab.Interop;
using TextGrab.Core;
using Forms = System.Windows.Forms;

namespace TextGrab.UI;

public sealed class TrayController : IDisposable
{
    private const int CaptureHotkeyId = 0x5447;
    private const int HistoryHotkeyId = 0x5448;
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly HwndSource _messageWindow;
    private readonly Forms.ToolStripMenuItem _shortcutItem;
    private bool _disposed;
    private bool _captureHotkeyRegistered;
    private bool _historyHotkeyRegistered;
    private HotkeySpec _currentCaptureHotkey = HotkeySpec.Default;
    private HotkeySpec _currentHistoryHotkey = HotkeySpec.HistoryDefault;

    public event EventHandler? CaptureRequested;
    public event EventHandler? ClipboardOcrRequested;
    public event EventHandler? HistoryRequested;
    public event EventHandler? ShortcutChangeRequested;
    public event EventHandler? ExitRequested;
    public bool CaptureHotkeyRegistered => _captureHotkeyRegistered;
    public bool HistoryHotkeyRegistered => _historyHotkeyRegistered;
    public HotkeySpec CurrentCaptureHotkey => _currentCaptureHotkey;
    public HotkeySpec CurrentHistoryHotkey => _currentHistoryHotkey;

    public TrayController()
    {
        _ownedIcon = AppIcon.CreateDrawingIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture Region", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("OCR Clipboard Image", null, (_, _) => ClipboardOcrRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Snip History", null, (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty));
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
        _captureHotkeyRegistered = Register(CaptureHotkeyId, _currentCaptureHotkey);
        _historyHotkeyRegistered = Register(HistoryHotkeyId, _currentHistoryHotkey);
        UpdateShortcutLabels();
    }

    public bool TryChangeHotkeys(HotkeySpec requestedCapture, HotkeySpec requestedHistory, out string error)
    {
        error = string.Empty;
        if (requestedCapture == requestedHistory)
        {
            error = "Capture region and Snip history must use different shortcuts.";
            return false;
        }

        var previousCapture = _currentCaptureHotkey;
        var previousHistory = _currentHistoryHotkey;
        if (_captureHotkeyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, CaptureHotkeyId);
        if (_historyHotkeyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, HistoryHotkeyId);
        _captureHotkeyRegistered = false;
        _historyHotkeyRegistered = false;

        var captureRegistered = Register(CaptureHotkeyId, requestedCapture);
        var historyRegistered = captureRegistered && Register(HistoryHotkeyId, requestedHistory);
        if (captureRegistered && historyRegistered)
        {
            _currentCaptureHotkey = requestedCapture;
            _currentHistoryHotkey = requestedHistory;
            _captureHotkeyRegistered = true;
            _historyHotkeyRegistered = true;
            UpdateShortcutLabels();
            return true;
        }

        if (captureRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, CaptureHotkeyId);
        if (historyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, HistoryHotkeyId);
        _captureHotkeyRegistered = Register(CaptureHotkeyId, previousCapture);
        _historyHotkeyRegistered = Register(HistoryHotkeyId, previousHistory);
        UpdateShortcutLabels();
        var failed = captureRegistered ? requestedHistory : requestedCapture;
        error = $"{failed.DisplayText} is already used by Windows or another app. The previous shortcuts remain active where available; both commands are also in the tray menu.";
        return false;
    }

    private bool Register(int id, HotkeySpec shortcut)
    {
        var modifiers = NativeMethods.ModNoRepeat;
        if (shortcut.Control) modifiers |= NativeMethods.ModControl;
        if (shortcut.Alt) modifiers |= NativeMethods.ModAlt;
        if (shortcut.Shift) modifiers |= NativeMethods.ModShift;
        return NativeMethods.RegisterHotKey(_messageWindow.Handle, id, modifiers, shortcut.VirtualKey);
    }

    private void UpdateShortcutLabels()
    {
        _shortcutItem.Text = "Keyboard shortcuts…";
        _icon.Text = $"TextGrab — capture {_currentCaptureHotkey.DisplayText}; history {_currentHistoryHotkey.DisplayText}";
    }

    public void ShowHotkeyConflict(bool captureUnavailable, bool historyUnavailable)
    {
        _icon.BalloonTipTitle = "TextGrab hotkey unavailable";
        _icon.BalloonTipText = captureUnavailable && historyUnavailable
            ? "The capture and history shortcuts are already used by another app. Both commands remain available from the tray menu."
            : captureUnavailable
                ? $"{_currentCaptureHotkey.DisplayText} is already used by another app. Capture Region remains available from the tray menu."
                : $"{_currentHistoryHotkey.DisplayText} is already used by another app. Snip History remains available from the tray menu.";
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
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == CaptureHotkeyId)
        {
            handled = true;
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HistoryHotkeyId)
        {
            handled = true;
            HistoryRequested?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_captureHotkeyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, CaptureHotkeyId);
        if (_historyHotkeyRegistered) NativeMethods.UnregisterHotKey(_messageWindow.Handle, HistoryHotkeyId);
        _messageWindow.RemoveHook(WindowProc);
        _messageWindow.Dispose();
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _ownedIcon.Dispose();
    }
}
