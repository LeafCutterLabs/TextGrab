namespace TextGrab.Core;

public sealed class OperationGate
{
    private int _busy;

    public IDisposable? TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0
        ? new Releaser(this)
        : null;

    public bool IsBusy => Volatile.Read(ref _busy) != 0;

    private sealed class Releaser(OperationGate owner) : IDisposable
    {
        private OperationGate? _owner = owner;
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null) Volatile.Write(ref owner._busy, 0);
        }
    }
}
