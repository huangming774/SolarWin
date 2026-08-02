namespace SolarWin.Data;

/// <summary>
/// Single-writer Channel pump for local SQLite (design v1.0 Phase 2 / 7).
/// Enqueue never throws to UI; CompleteAndDrain is for account switch/remove.
/// </summary>
public interface IChatWritePump
{
    /// <summary>Non-blocking enqueue. Failures are logged only.</summary>
    void Enqueue(ChatWriteOp op);

    /// <summary>
    /// Complete the current channel, flush remaining ops, stop the consumer.
    /// Safe to call when already stopped.
    /// </summary>
    Task CompleteAndDrainAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure a consumer is running for the currently bound account.
    /// After migrate ready, enqueues a one-shot full eviction if not yet run for this bind.
    /// </summary>
    void EnsureRunning();

    // —— Phase 7 diagnostics (debug / settings) ——

    /// <summary>Ops accepted into the channel but not yet finished processing.</summary>
    int QueueDepth { get; }

    /// <summary>Total ops successfully written to the channel.</summary>
    long EnqueuedTotal { get; }

    /// <summary>Ops dropped because the bounded channel evicted them or was unavailable.</summary>
    long DroppedTotal { get; }

    /// <summary>Batches that completed <c>SaveChanges</c> without throwing.</summary>
    long BatchesSucceeded { get; }

    /// <summary>Batches that threw during processing.</summary>
    long BatchesFailed { get; }

    /// <summary>
    /// <see cref="BatchesFailed"/> / (<see cref="BatchesSucceeded"/> + <see cref="BatchesFailed"/>).
    /// 0 when no batches yet.
    /// </summary>
    double BatchFailureRate { get; }
}
