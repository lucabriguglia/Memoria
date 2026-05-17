using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql;

/// <summary>
/// Postgres-flavoured DCB store: serialises conflicting writers with
/// <c>pg_advisory_xact_lock</c> keyed on the query's tags, then runs the same
/// conflict check + insert as the base store inside that lock.
/// </summary>
/// <remarks>
/// Locks are auto-released at transaction end (commit or rollback). Acquired in sorted
/// order across all writers to avoid deadlocks. Tag-less conditions hold no advisory
/// locks and fall through to the base behaviour — document and avoid in production.
/// </remarks>
public sealed class NpgsqlDcbStore(IDcbDbContext dbContext) : EntityFrameworkCoreDcbStore(dbContext)
{
    public override async Task<Result<AppendResult>> Append(IReadOnlyList<EventToAppend> events, AppendCondition condition, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Result<AppendResult>.Ok(new AppendResult(Array.Empty<StoredEvent>(), 0L));
        }

        var lockKeys = AdvisoryLockKey.ForCondition(condition);

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var key in lockKeys)
        {
            await DbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", [key], cancellationToken);
        }

        var result = await AppendCore(events, condition, cancellationToken);

        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        return result;
    }
}
