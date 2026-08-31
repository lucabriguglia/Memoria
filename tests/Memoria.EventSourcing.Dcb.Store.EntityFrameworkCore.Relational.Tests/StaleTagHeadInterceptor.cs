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
/// <em>How</em> the tag head is moved is left to the caller, because no single technique works
/// everywhere. SQLite tolerates a second statement on the connection the append already holds;
/// Npgsql rejects it outright, so the real engines move the row from a connection of their own —
/// which is safe here only because the conditioned path holds no lock on that row until it writes.
/// </para>
/// </remarks>
/// <param name="moveTagHead">Moves the tag head. Runs once, during the save that writes the events.</param>
public class StaleTagHeadInterceptor(Func<DbContext, CancellationToken, Task> moveTagHead) : SaveChangesInterceptor
{
    private bool _fired;

    /// <summary>
    /// Gets whether the interceptor found the append's write and moved the tag head under it.
    /// </summary>
    public bool Fired => _fired;

    /// <summary>
    /// Gets the exception the simulation itself threw, if any.
    /// </summary>
    /// <remarks>
    /// Surfaced so a test can assert the race was really reproduced. A simulation that throws would
    /// otherwise be swallowed by the append's own exception handling and reported as a storage
    /// failure, which looks like the production code misbehaving.
    /// </remarks>
    public Exception? SimulationFailure { get; private set; }

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

            try
            {
                await moveTagHead(context!, cancellationToken);
            }
            catch (Exception exception)
            {
                SimulationFailure = exception;
                throw;
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Moves the tag head through the context the append is already using.
    /// </summary>
    /// <remarks>
    /// Correct on SQLite, which allows the extra statement on the open connection. Npgsql does not.
    /// </remarks>
    public static StaleTagHeadInterceptor OnSameConnection(string tag) =>
        new((context, cancellationToken) => context.Database.ExecuteSqlRawAsync(
            "UPDATE DcbTagHeads SET Token = {0} WHERE Tag = {1}", [Guid.NewGuid(), tag], cancellationToken));
}
