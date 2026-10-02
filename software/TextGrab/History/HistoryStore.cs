using System.Windows.Media.Imaging;

namespace TextGrab.History;

public sealed class HistoryStore
{
    public const int Capacity = 25;
    private readonly List<HistoryEntry> _entries = [];

    public IReadOnlyList<HistoryEntry> Entries => _entries;
    public event EventHandler? Changed;

    public HistoryEntry Add(string text, BitmapSource thumbnail, DateTimeOffset? createdAt = null)
    {
        if (!thumbnail.IsFrozen)
            throw new ArgumentException("History thumbnails must be frozen.", nameof(thumbnail));

        var entry = new HistoryEntry(Guid.NewGuid(), createdAt ?? DateTimeOffset.Now, text, thumbnail);
        _entries.Insert(0, entry);
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    public bool UpdateText(Guid id, string text)
    {
        var entry = _entries.FirstOrDefault(candidate => candidate.Id == id);
        if (entry is null || entry.Text == text) return false;
        entry.Text = text;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Remove(Guid id)
    {
        var index = _entries.FindIndex(candidate => candidate.Id == id);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_entries.Count == 0) return;
        _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
