using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.AppendRoutine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// Installs the routine the DCB store appends through on engines that have one.
/// </summary>
public static class DcbAppendRoutineDatabaseExtensions
{
    /// <summary>
    /// Creates or replaces the DCB append routine, so appends take one round trip. Safe to run more
    /// than once. Does nothing on engines whose appends go through Entity Framework Core.
    /// </summary>
    /// <param name="database">The DCB context's database.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// <para>
    /// For databases created with <c>EnsureCreated</c>, such as tests and examples: call it right
    /// after. <c>EnsureCreated</c> creates the tables from the model, and a routine is not part of
    /// the model.
    /// </para>
    /// <para>
    /// The routine is generated from this context's model, so it fits however the context maps the
    /// store's tables. Call it from each context that maps them differently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await dbContext.Database.EnsureCreatedAsync();
    /// await dbContext.Database.EnsureDcbAppendRoutineAsync();
    /// </code>
    /// </example>
    public static async Task EnsureDcbAppendRoutineAsync(this DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        if (DcbAppendRoutine.For(database.GetService<ICurrentDbContext>().Context) is { } routine)
        {
            await database.ExecuteSqlRawAsync(routine.Definition, cancellationToken);
        }
    }
}
