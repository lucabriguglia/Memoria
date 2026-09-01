using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Appends an event from another context in the gap between an operation's first and second query.
/// </summary>
/// <remarks>
/// <para>
/// That gap is where a read-decide-append cycle reads the boundary's position and folds it, and which
/// of those comes first decides what an event arriving here costs. Read the position first and the
/// fold sees more than the condition admits, so the append is refused. Read it second and it admits
/// more than the fold saw, so the append is accepted on a decision that never read the event — the
/// lost update the condition exists to prevent.
/// </para>
/// <para>
/// It injects as the second query is about to execute rather than when the first one finishes: at
/// that moment the first reader has been consumed and closed, so the append can reuse the same SQLite
/// connection. Firing while a reader is still open would fail on the connection rather than reproduce
/// anything.
/// </para>
/// </remarks>
public class AppendsBetweenReadsInterceptor(Func<Task> appendFromAnotherContext) : DbCommandInterceptor
{
    private int _queries;
    private bool _fired;

    /// <summary>
    /// Gets whether a second query was reached and the append happened before it.
    /// </summary>
    public bool Fired => _fired;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await InjectBeforeTheSecondQuery();
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await InjectBeforeTheSecondQuery();
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task InjectBeforeTheSecondQuery()
    {
        _queries++;

        if (_fired || _queries != 2)
        {
            return;
        }

        _fired = true;
        await appendFromAnotherContext();
    }
}
