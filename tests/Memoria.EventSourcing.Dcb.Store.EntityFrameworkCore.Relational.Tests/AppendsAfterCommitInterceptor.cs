using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Appends an event from another connection the moment a transaction commits, reproducing a writer
/// that gets in immediately after an append.
/// </summary>
/// <remarks>
/// <para>
/// This exists to catch a snapshot being stamped with a position it did not fold. Reading
/// <c>MAX(Position)</c> after the append's transaction commits can pick up somebody else's event, and
/// a snapshot recording that position claims to have consumed an event it never applied — a later
/// <c>SnapshotWithNewEvents</c> then starts past it and skips it silently. Without a writer landing
/// in exactly that window the re-read and the correct value agree, so the bug is invisible.
/// </para>
/// <para>
/// It waits for the first commit that leaves events behind. The tag head rows are created in a
/// transaction of their own before the append, and injecting there would land the event before the
/// append rather than after it.
/// </para>
/// </remarks>
public class AppendsAfterCommitInterceptor(Func<Task> appendFromAnotherConnection, Func<Task<int>> countEvents)
    : DbTransactionInterceptor
{
    private bool _fired;

    /// <summary>
    /// Gets whether the injected append happened.
    /// </summary>
    public bool Fired => _fired;

    /// <inheritdoc />
    public override async Task TransactionCommittedAsync(DbTransaction transaction,
        TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!_fired && await countEvents() > 0)
        {
            _fired = true;
            await appendFromAnotherConnection();
        }

        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
