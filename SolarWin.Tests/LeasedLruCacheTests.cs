using SolarWin.Helpers;

namespace SolarWin.Tests;

public sealed class LeasedLruCacheTests
{
    [Fact]
    public void EvictionDisposesLeastRecentlyUsedUnleasedValue()
    {
        using var cache = new LeasedLruCache<string, TrackedDisposable>(20);
        var first = new TrackedDisposable();
        var second = new TrackedDisposable();
        var third = new TrackedDisposable();

        cache.AddOrAcquire("first", first, 10)!.Dispose();
        cache.AddOrAcquire("second", second, 10)!.Dispose();
        Assert.True(cache.TryAcquire("first", out var firstLease));
        firstLease!.Dispose();
        cache.AddOrAcquire("third", third, 10)!.Dispose();

        Assert.False(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.False(third.IsDisposed);
    }

    [Fact]
    public void ActiveLeasePreventsEvictionAndRejectsOverflow()
    {
        using var cache = new LeasedLruCache<string, TrackedDisposable>(10);
        var pinned = new TrackedDisposable();
        var rejected = new TrackedDisposable();
        using var lease = cache.AddOrAcquire("pinned", pinned, 10)!;

        var rejectedLease = cache.AddOrAcquire("rejected", rejected, 10);

        Assert.Null(rejectedLease);
        Assert.False(pinned.IsDisposed);
        Assert.True(rejected.IsDisposed);
        Assert.Equal(10, cache.TotalBytes);
    }

    [Fact]
    public void ClearInvalidatesLeaseAndDisposesImmediately()
    {
        using var cache = new LeasedLruCache<string, TrackedDisposable>(10);
        var value = new TrackedDisposable();
        using var lease = cache.AddOrAcquire("value", value, 10)!;

        cache.Clear();

        Assert.True(value.IsDisposed);
        Assert.False(lease.TryGetValue(out _));
        Assert.Equal(0, cache.TotalBytes);
    }

    [Fact]
    public void DuplicateAddUsesCachedValueAndDisposesReplacement()
    {
        using var cache = new LeasedLruCache<string, TrackedDisposable>(20);
        var cached = new TrackedDisposable();
        var replacement = new TrackedDisposable();
        cache.AddOrAcquire("same", cached, 10)!.Dispose();

        using var lease = cache.AddOrAcquire("same", replacement, 10)!;

        Assert.True(replacement.IsDisposed);
        Assert.True(lease.TryGetValue(out var actual));
        Assert.Same(cached, actual);
    }

    private sealed class TrackedDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
