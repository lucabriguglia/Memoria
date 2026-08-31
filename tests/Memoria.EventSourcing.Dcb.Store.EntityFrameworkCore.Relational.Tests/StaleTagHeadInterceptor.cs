using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Simulates an overlapping append committing in the window between a conditioned append loading a
/// tag head row and writing it back.
/// </summary>
/// <remarks>
/// <para>
/// That window is the only place the tag head token does any work, and it cannot be hit by two
/// sequential appends: the second one's <c>MAX(Position)</c> pre-check already sees the first one's
/// event and refuses before the token is consulted. Without this, deleting
/// <c>IsConcurrencyToken()</c> from the model changes no test result — verified by doing exactly
/// that.
/// </para>
/// <para>
/// It replaces the token through the same context, so the statement runs on the connection and
/// transaction the append already holds. Racing it from a second connection would deadlock against
/// those locks rather than reproduce the interleaving.
/// </para>
/// </remarks>
public class StaleTagHeadInterceptor(string tag) : SaveChangesInterceptor
{
    private bool _fired;

    /// <summary>
    /// Gets whether the interceptor found the append's write and moved the tag head under it.
    /// </summary>
    public bool Fired => _fired;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;

        // Only the save that writes the events, not the earlier one that creates missing head rows.
        var isTheAppend = context is not null
                          && context.ChangeTracker.Entries<DcbEventEntity>()
                              .Any(entry => entry.State == EntityState.Added);

        if (!_fired && isTheAppend)
        {
            _fired = true;

            await context!.Database.ExecuteSqlRawAsync(
                "UPDATE DcbTagHeads SET Token = {0} WHERE Tag = {1}",
                [Guid.NewGuid(), tag], cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
