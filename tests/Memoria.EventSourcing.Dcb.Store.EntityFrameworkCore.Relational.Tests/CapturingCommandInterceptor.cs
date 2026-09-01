using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Records the SQL a context sends, so a test can assert on the statement Entity Framework Core
/// generated rather than on the model metadata that shaped it.
/// </summary>
/// <remarks>
/// Needed because the tag head check has no representation in code: it is a WHERE clause Entity
/// Framework Core adds to an UPDATE, from a concurrency token declaration in one file and a tracked
/// query in another. Neither reads as load-bearing at the point someone would remove it.
/// </remarks>
public class CapturingCommandInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];

    /// <summary>
    /// Gets the SQL sent so far, in order.
    /// </summary>
    public IReadOnlyList<string> Commands => _commands;

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// The statements that updated the tag head table.
    /// </summary>
    public IReadOnlyList<string> TagHeadUpdates =>
    [
        .. _commands.Where(command =>
            command.Contains("UPDATE", StringComparison.Ordinal)
            && command.Contains("DcbTagHeads", StringComparison.Ordinal))
    ];
}
