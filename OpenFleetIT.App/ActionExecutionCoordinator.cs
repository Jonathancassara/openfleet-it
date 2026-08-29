using System.Collections.Concurrent;

namespace OpenFleetIT.App;

public sealed class ActionExecutionCoordinator
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ActiveKeys => _active.Keys.ToArray();

    public bool TryBegin(string key, CancellationToken cancellationToken, out ActionExecutionLease? lease)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An action key is required.", nameof(key));
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_active.TryAdd(key, source))
        {
            source.Dispose();
            lease = null;
            return false;
        }

        lease = new ActionExecutionLease(key, source, Complete);
        return true;
    }

    public bool Cancel(string key)
    {
        if (!_active.TryGetValue(key, out var source)) return false;
        source.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var source in _active.Values) source.Cancel();
    }

    private void Complete(string key, CancellationTokenSource source)
    {
        _active.TryRemove(new KeyValuePair<string, CancellationTokenSource>(key, source));
        source.Dispose();
    }
}

public sealed class ActionExecutionLease : IDisposable
{
    private readonly string _key;
    private readonly CancellationTokenSource _source;
    private readonly Action<string, CancellationTokenSource> _complete;
    private int _disposed;

    internal ActionExecutionLease(string key, CancellationTokenSource source,
        Action<string, CancellationTokenSource> complete)
    {
        _key = key;
        _source = source;
        _complete = complete;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _complete(_key, _source);
    }
}
