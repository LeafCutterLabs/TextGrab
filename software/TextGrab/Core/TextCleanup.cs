namespace TextGrab.Core;

public static class TextCleanup
{
    public static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
                            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd(' ', '\t');

        return string.Join("\r\n", lines.Where(line => line.Length > 0));
    }
}
