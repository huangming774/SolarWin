namespace SolarWin.Helpers;

/// <summary>
/// Thread-safe LRU cache for disposable GPU (or other native) resources.
/// Values are owned by the cache; eviction / replace / <see cref="Clear"/> / <see cref="Dispose"/>
/// call <see cref="IDisposable.Dispose"/> on the value once all active <see cref="Lease"/> instances
/// are released. A leased entry is never disposed while its lease count is &gt; 0, preventing
/// use-after-free when UI still holds a texture.
/// </summary>
internal sealed class LeasedLruCache<TKey, TValue> : IDisposable
    where TKey : notnull
    where TValue : class, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, Entry> _entries;
    private readonly LinkedList<Entry> _lru = [];
    private readonly long _maxBytes;
    private bool _disposed;
    private long _totalBytes;

    public LeasedLruCache(long maxBytes, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _maxBytes = maxBytes;
        _entries = new Dictionary<TKey, Entry>(comparer);
    }

    public long TotalBytes
    {
        get
        {
            lock (_gate)
            {
                return _totalBytes;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryAcquire(TKey key, out Lease? lease)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_entries.TryGetValue(key, out var entry) || entry.Value is null)
            {
                lease = null;
                return false;
            }

            Touch(entry);
            entry.LeaseCount++;
            lease = new Lease(this, entry);
            return true;
        }
    }

    public Lease? AddOrAcquire(TKey key, TValue value, long estimatedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedBytes);

        List<TValue>? disposals = null;
        Lease? lease;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_entries.TryGetValue(key, out var existing) && existing.Value is not null)
            {
                Touch(existing);
                existing.LeaseCount++;
                lease = new Lease(this, existing);
                disposals = [value];
            }
            else if (estimatedBytes > _maxBytes)
            {
                lease = null;
                disposals = [value];
            }
            else
            {
                disposals = EvictUntilFits(estimatedBytes);
                if (_totalBytes + estimatedBytes > _maxBytes)
                {
                    lease = null;
                    disposals ??= [];
                    disposals.Add(value);
                }
                else
                {
                    var entry = new Entry(key, value, estimatedBytes) { LeaseCount = 1 };
                    entry.Node = _lru.AddFirst(entry);
                    _entries.Add(key, entry);
                    _totalBytes += estimatedBytes;
                    lease = new Lease(this, entry);
                }
            }
        }

        DisposeAll(disposals);
        return lease;
    }

    public bool Remove(TKey key)
    {
        TValue? disposal = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_entries.Remove(key, out var entry))
            {
                return false;
            }

            RemoveNode(entry);
            disposal = DetachValue(entry);
        }

        disposal?.Dispose();
        return true;
    }

    public void Clear()
    {
        List<TValue> disposals;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            disposals = DetachAll();
        }

        DisposeAll(disposals);
    }

    public void Dispose()
    {
        List<TValue> disposals;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            disposals = DetachAll();
        }

        DisposeAll(disposals);
    }

    private List<TValue>? EvictUntilFits(long incomingBytes)
    {
        List<TValue>? disposals = null;
        var node = _lru.Last;
        while (_totalBytes + incomingBytes > _maxBytes && node is not null)
        {
            var previous = node.Previous;
            var entry = node.Value;
            if (entry.LeaseCount == 0)
            {
                _entries.Remove(entry.Key);
                RemoveNode(entry);
                var value = DetachValue(entry);
                if (value is not null)
                {
                    disposals ??= [];
                    disposals.Add(value);
                }
            }

            node = previous;
        }

        return disposals;
    }

    private List<TValue> DetachAll()
    {
        var disposals = new List<TValue>(_entries.Count);
        foreach (var entry in _entries.Values)
        {
            var value = DetachValue(entry);
            if (value is not null)
            {
                disposals.Add(value);
            }
        }

        _entries.Clear();
        _lru.Clear();
        _totalBytes = 0;
        return disposals;
    }

    private TValue? DetachValue(Entry entry)
    {
        var value = entry.Value;
        if (value is null)
        {
            return null;
        }

        entry.Value = null;
        _totalBytes -= entry.EstimatedBytes;
        return value;
    }

    private void Release(Entry entry)
    {
        lock (_gate)
        {
            if (entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }
        }
    }

    private bool TryGetValue(Entry entry, out TValue? value)
    {
        lock (_gate)
        {
            value = entry.Value;
            return value is not null;
        }
    }

    private void Touch(Entry entry)
    {
        RemoveNode(entry);
        entry.Node = _lru.AddFirst(entry);
    }

    private void RemoveNode(Entry entry)
    {
        if (entry.Node is not null)
        {
            _lru.Remove(entry.Node);
            entry.Node = null;
        }
    }

    private static void DisposeAll(List<TValue>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            value.Dispose();
        }
    }

    internal sealed class Entry(TKey key, TValue value, long estimatedBytes)
    {
        public TKey Key { get; } = key;

        public TValue? Value { get; set; } = value;

        public long EstimatedBytes { get; } = estimatedBytes;

        public int LeaseCount { get; set; }

        public LinkedListNode<Entry>? Node { get; set; }
    }

    public sealed class Lease : IDisposable
    {
        private LeasedLruCache<TKey, TValue>? _owner;
        private Entry? _entry;

        internal Lease(LeasedLruCache<TKey, TValue> owner, Entry entry)
        {
            _owner = owner;
            _entry = entry;
        }

        public bool TryGetValue(out TValue? value)
        {
            var owner = Volatile.Read(ref _owner);
            var entry = Volatile.Read(ref _entry);
            if (owner is null || entry is null)
            {
                value = null;
                return false;
            }

            return owner.TryGetValue(entry, out value);
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            var entry = Interlocked.Exchange(ref _entry, null);
            if (owner is not null && entry is not null)
            {
                owner.Release(entry);
            }
        }
    }
}
