namespace SolarWin.Helpers;

/// <summary>Runs independent async UI workflows with bounded concurrency.</summary>
internal static class AsyncConcurrencyHelper
{
    public static async Task RunAsync(int maxConcurrency, params Func<Task>[] operations)
    {
        if (operations.Length == 0)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        async Task RunOneAsync(Func<Task> operation)
        {
            await gate.WaitAsync().ConfigureAwait(true);
            try
            {
                await operation().ConfigureAwait(true);
            }
            finally
            {
                gate.Release();
            }
        }

        await Task.WhenAll(operations.Select(RunOneAsync)).ConfigureAwait(true);
    }
}
