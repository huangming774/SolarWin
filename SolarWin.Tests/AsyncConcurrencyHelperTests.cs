using SolarWin.Helpers;

namespace SolarWin.Tests;

public sealed class AsyncConcurrencyHelperTests
{
    [Fact]
    public async Task RunAsync_NeverExceedsConfiguredConcurrency()
    {
        var active = 0;
        var peak = 0;
        var completed = 0;
        var operations = Enumerable.Range(0, 12)
            .Select(_ => (Func<Task>)(async () =>
            {
                var now = Interlocked.Increment(ref active);
                UpdatePeak(ref peak, now);
                await Task.Delay(15);
                Interlocked.Decrement(ref active);
                Interlocked.Increment(ref completed);
            }))
            .ToArray();

        await AsyncConcurrencyHelper.RunAsync(4, operations);

        Assert.Equal(12, completed);
        Assert.InRange(peak, 1, 4);
    }

    private static void UpdatePeak(ref int peak, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref peak);
            if (candidate <= current
                || Interlocked.CompareExchange(ref peak, candidate, current) == current)
            {
                return;
            }
        }
    }
}
