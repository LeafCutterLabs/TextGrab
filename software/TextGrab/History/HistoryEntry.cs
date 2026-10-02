using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace TextGrab.History;

public sealed class HistoryEntry : INotifyPropertyChanged
{
    private string _text;

    internal HistoryEntry(Guid id, DateTimeOffset createdAt, string text, BitmapSource thumbnail)
    {
        Id = id;
        CreatedAt = createdAt;
        _text = text;
        Thumbnail = thumbnail;
    }

    public Guid Id { get; }
    public DateTimeOffset CreatedAt { get; }
    public BitmapSource Thumbnail { get; }
    public string Text
    {
        get => _text;
        internal set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
