using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// The most events one append commits atomically, unless overridden.
    /// </summary>
    /// <remarks>
    /// There is no hard relational limit the way Cosmos DB caps a transactional batch at 100
    /// operations. This is a guard against an unbounded append rather than a provider constraint, so
    /// it is set high enough that no reasonable decision meets it.
    /// </remarks>
    public const int DefaultMaxEventsPerAppend = 1000;

    /// <summary>
    /// Appends events, refusing if the condition's boundary has moved since it was read.
    /// </summary>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="events">The events, with the tags they are appended under.</param>
    /// <param name="condition">
    /// The concurrency check, or null to append unconditionally. An unconditional append never
    /// fails on concurrency — it still makes conditioned appends over the same tags fail, but has
    /// nothing of its own to be invalidated.
    /// </param>
    /// <param name="maxEventsPerAppend">The batch limit. Defaults to <see cref="DefaultMaxEventsPerAppend"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The outcome. <c>memoria/concurrency-conflict</c> when the boundary moved,
    /// <c>memoria/batch-limit-exceeded</c> when too many events were supplied, and
    /// <c>memoria/storage-failure</c> for anything else.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The check has two halves. Reading <c>MAX(Position)</c> inside the boundary is the cheap one,
    /// and gives the caller a good error before any work is done. The load-bearing half is the tag
    /// head rows: every append updates the row for every tag it writes under <em>and</em> every tag
    /// its condition names, so two appends contend exactly when their boundaries overlap and not
    /// otherwise.
    /// </para>
    /// <para>
    /// Needs a relational provider. The in-memory provider models neither the transaction this opens
    /// nor the concurrency token the head rows carry, so an append against it would report success
    /// it cannot deliver.
    /// </para>
    /// </remarks>
    public static async Task<Result> SaveEvents(this IDcbDbContext dcbDbContext, TaggedEvent[] events,
        AppendCondition? condition, int maxEventsPerAppend = DefaultMaxEventsPerAppend,
        CancellationToken cancellationToken = default)
    {
        const string operation = "Append Events";

        ArgumentNullException.ThrowIfNull(events);

        // Nothing to write is success, matching what every store has done since 1.5.0. A condition
        // guards events; with no events there is nothing for it to guard.
        if (events.Length == 0)
        {
            return Result.Ok();
        }

        if (events.Length > maxEventsPerAppend)
        {
            return DcbStoreFailures.BatchLimitExceeded(operation, events.Length, maxEventsPerAppend);
        }

        // Ordinal, so every transaction takes the head rows in the same order. Two appends over
        // overlapping tag sets would otherwise be free to acquire them in opposite orders and
        // deadlock rather than conflict.
        var affectedTags = events
            .SelectMany(taggedEvent => taggedEvent.Tags)
            .Concat(condition?.Query.Tags ?? [])
            .Select(tag => tag.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        try
        {
            await dcbDbContext.EnsureTagHeads(affectedTags, cancellationToken);

            await using var transaction = await dcbDbContext.Database.BeginTransactionAsync(cancellationToken);

            if (condition is not null)
            {
                // Tracked, so replacing each Token below emits `WHERE Tag = @t AND Token = @old`.
                // That is the check: an overlapping append committing first replaces the token and
                // this update matches nothing.
                var heads = await dcbDbContext.DcbTagHeads
                    .Where(head => affectedTags.Contains(head.Tag))
                    .ToListAsync(cancellationToken);

                // Read after the heads are loaded, so the tokens captured above belong to the same
                // observation as this position.
                var latestPosition = await dcbDbContext.GetLatestPosition(condition.Query,
                    cancellationToken: cancellationToken);

                if (latestPosition != condition.AfterPosition)
                {
                    DcbDiagnostics.AddConcurrencyConflictEvent(condition.Query, condition.AfterPosition,
                        latestPosition);

                    return DcbStoreFailures.ConcurrencyConflict(condition.Query, condition.AfterPosition,
                        latestPosition);
                }

                // Ordered so the updates are emitted in a consistent order across transactions.
                foreach (var head in heads.OrderBy(head => head.Tag, StringComparer.Ordinal))
                {
                    head.Token = Guid.NewGuid();
                }
            }
            else
            {
                // An unconditional append must still invalidate every conditioned append over these
                // tags, but has nothing of its own to be invalidated — so it replaces the tokens
                // without the guard. Doing it through the change tracker instead would make two
                // unconditional appends over the same tag conflict with each other, which is a
                // failure neither of them asked for.
                var token = Guid.NewGuid();

                await dcbDbContext.DcbTagHeads
                    .Where(head => affectedTags.Contains(head.Tag))
                    .ExecuteUpdateAsync(head => head.SetProperty(row => row.Token, token), cancellationToken);
            }

            foreach (var taggedEvent in events)
            {
                dcbDbContext.DcbEvents.Add(new DcbEventEntity
                {
                    EventType = TypeBindings.GetEventBindingKey(taggedEvent.Event.GetType()),
                    Data = DomainSerializer.Current.Serialize(taggedEvent.Event),
                    // Added through the navigation so Entity Framework Core fills in the foreign key
                    // from the position the database assigns.
                    Tags = taggedEvent.Tags
                        .Select(tag => new DcbEventTagEntity { Tag = tag.ToString() })
                        .ToList()
                });
            }

            await dcbDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            dcbDbContext.ChangeTracker.Clear();

            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A head row this append captured was replaced by an overlapping append that committed
            // first. Only a conditioned append can get here: an unconditional one has no token to
            // be invalidated, because EnsureTagHeads leaves it nothing to guard.
            dcbDbContext.ChangeTracker.Clear();

            var query = condition?.Query
                        ?? TagQuery.AnyOf(affectedTags.Select(Tag.Parse).ToArray());
            var expectedPosition = condition?.AfterPosition ?? AppendCondition.NoEvents;

            DcbDiagnostics.AddException(exception, operation, query);

            // Re-read so the caller can retry from the failure without issuing another query. Only
            // on the failure path, so the happy path pays nothing for it.
            var latestPosition = await dcbDbContext.GetLatestPosition(query, cancellationToken: cancellationToken);

            DcbDiagnostics.AddConcurrencyConflictEvent(query, expectedPosition, latestPosition);

            return DcbStoreFailures.ConcurrencyConflict(query, expectedPosition, latestPosition);
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, condition?.Query);

            return DcbStoreFailures.StorageFailure(operation, condition?.Query);
        }
    }

    /// <summary>
    /// Creates any tag head rows the append will contend on, before the append's own transaction.
    /// </summary>
    /// <remarks>
    /// A head row carries no domain meaning until an event references its tag, so creating one early
    /// is harmless and creating one twice is not a conflict. Doing it here keeps the race between
    /// two appends that both introduce a tag out of the critical path, where it would otherwise
    /// surface as a primary key violation indistinguishable from a real storage failure.
    /// </remarks>
    private static async Task EnsureTagHeads(this IDcbDbContext dcbDbContext, List<string> tags,
        CancellationToken cancellationToken)
    {
        var existing = await dcbDbContext.DcbTagHeads.AsNoTracking()
            .Where(head => tags.Contains(head.Tag))
            .Select(head => head.Tag)
            .ToListAsync(cancellationToken);

        var missing = tags.Except(existing, StringComparer.Ordinal).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var tag in missing)
        {
            dcbDbContext.DcbTagHeads.Add(new DcbTagHeadEntity { Tag = tag, Token = Guid.NewGuid() });
        }

        try
        {
            await dcbDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another append introduced the same tag first. The row now exists, which is all this
            // step wanted.
        }
        finally
        {
            dcbDbContext.ChangeTracker.Clear();
        }
    }
}
