namespace TextGrab.Core;

public sealed record HotkeySpec(bool Control, bool Alt, bool Shift, uint VirtualKey, string KeyName)
{
    public static HotkeySpec Default { get; } = new(true, true, false, 'T', "T");
    public static HotkeySpec HistoryDefault { get; } = new(true, true, false, 'V', "V");

    public string DisplayText
    {
        get
        {
            var parts = new List<string>(4);
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(KeyName);
            return string.Join("+", parts);
        }
    }

    public bool HasModifier => Control || Alt || Shift;
}
