using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Removes every tag head row in the instant before a conditioned append reads them, so the read
/// comes back empty.
/// </summary>
/// <remarks>
/// <para>
/// <c>EnsureTagHeads</c> creates a row for every affected tag immediately before the append, so the
/// read that follows cannot normally come back empty. It can if something removed the rows in
/// between — a restore, a truncate, surgery on the wrong database — and the append must not then
/// read the boundary as empty, because that is the answer that lets a decision which may only happen
/// once happen twice.
/// </para>
/// <para>
/// Fires before <em>every</em> such read, not once. An append that finds no head rows creates them
/// and reads again, so removing them once only exercises that recovery; the case this reproduces is
/// the one where the rows are not there for either read, and the append has to answer its condition
/// without them.
/// </para>
/// <para>
/// Targets the probe by the two tables it names, rather than by counting reads, so it keeps working
/// if another statement is added ahead of it. On the same connection, which SQLite allows and Npgsql
/// does not — the same constraint <see cref="StaleTagHeadInterceptor"/> carries.
/// </para>
/// </remarks>
public class VanishingTagHeadInterceptor : DbCommandInterceptor
{
    private bool _fired;

    /// <summary>Gets whether the rows were removed, so a test can assert the race really happened.</summary>
    public bool Fired => _fired;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var isTheProbe = command.CommandText.Contains("DcbTagHeads", StringComparison.Ordinal)
                         && command.CommandText.Contains("DcbEventTags", StringComparison.Ordinal);

        if (isTheProbe)
        {
            _fired = true;

            await using var delete = command.Connection!.CreateCommand();
            delete.Transaction = command.Transaction;
            delete.CommandText = "DELETE FROM DcbTagHeads";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
