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

        var affectedTags = AffectedTags(events, condition);

        try
        {
            await dcbDbContext.EnsureTagHeads(affectedTags, cancellationToken);

            await using var transaction = await dcbDbContext.Database.BeginTransactionAsync(cancellationToken);

            var appendResult = await dcbDbContext.AppendCore(events, condition, affectedTags, cancellationToken);
            if (appendResult.IsNotSuccess)
            {
                return appendResult.Failure!;
            }

            await transaction.CommitAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception)
        {
            return await dcbDbContext.AppendFailure(exception, operation, condition, affectedTags,
                cancellationToken);
        }
    }

    /// <summary>
    /// One tag head row as a conditioned append reads it, with the boundary position that was true
    /// at the same instant.
    /// </summary>
    /// <param name="Tag">The tag.</param>
    /// <param name="Token">The token to guard the update on.</param>
    /// <param name="LatestPosition">
    /// The highest position inside the condition's boundary, or null when it is empty. The same value
    /// on every row — it is the boundary's, not the tag's.
    /// </param>
    private sealed record TagHeadProbe(string Tag, Guid Token, long? LatestPosition);

    /// <summary>
    /// The tags an append contends on: every tag it writes under, and every tag its condition names.
    /// </summary>
    /// <remarks>
    /// Ordered ordinally, so every transaction takes the head rows in the same order. Two appends
    /// over overlapping tag sets would otherwise be free to acquire them in opposite orders and
    /// deadlock rather than conflict.
    /// </remarks>
    private static List<string> AffectedTags(TaggedEvent[] events, AppendCondition? condition) =>
        events
            .SelectMany(taggedEvent => taggedEvent.Tags)
            .Concat(condition?.Query.Tags ?? [])
            .Select(tag => tag.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Checks the condition, claims the tag heads and writes the events, inside a transaction the
    /// caller owns — so an append can be committed together with a snapshot written from it.
    /// </summary>
    /// <returns>
    /// The position of the last event written, or a failure. Taken from the entities the database
    /// populated rather than by re-reading <c>MAX(Position)</c>: a re-read after this transaction
    /// commits could pick up a position belonging to somebody else's append, and a snapshot stamped
    /// with it would claim to have consumed an event it never applied.
    /// </returns>
    private static async Task<Result<long>> AppendCore(this IDcbDbContext dcbDbContext, TaggedEvent[] events,
        AppendCondition? condition, List<string> affectedTags, CancellationToken cancellationToken)
    {
        if (condition is not null)
        {
            // The tokens and the position the condition is checked against come back together, so
            // they belong to the same observation by construction rather than by being ordered. The
            // boundary read rides along as a subquery over nothing in the head row, so the engine
            // evaluates it once rather than once per tag.
            var boundaryPositions = dcbDbContext.PositionsInside(condition.Query);

            var heads = await dcbDbContext.DcbTagHeads.AsNoTracking()
                .Where(head => affectedTags.Contains(head.Tag))
                .Select(head => new TagHeadProbe(head.Tag, head.Token,
                    boundaryPositions.Max(position => (long?)position)))
                .ToListAsync(cancellationToken);

            // Every affected tag has a head row by now, because EnsureTagHeads just created the
            // missing ones — so this reads the boundary again only if they were removed underneath
            // it. Taking the empty case as an empty boundary instead would let an append conditioned
            // on NoEvents through against a boundary full of events.
            var latestPosition = heads.Count > 0
                ? heads[0].LatestPosition ?? AppendCondition.NoEvents
                : await dcbDbContext.GetLatestPosition(condition.Query, cancellationToken: cancellationToken);

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
                // Attached carrying the token that was read, then changed. Attach takes the original
                // values from the instance, so replacing Token emits
                // `WHERE Tag = @t AND Token = @old` — that is the check: an overlapping append
                // committing first replaces the token and this update matches nothing. Attaching a
                // row built here rather than loading a tracked one is what lets the read above be a
                // projection, and so lets it carry the position too.
                var tracked = new DcbTagHeadEntity { Tag = head.Tag, Token = head.Token };

                dcbDbContext.DcbTagHeads.Attach(tracked);

                tracked.Token = Guid.NewGuid();
            }
        }
        else
        {
            // An unconditional append must still invalidate every conditioned append over these
            // tags, but has nothing of its own to be invalidated — so it replaces the tokens without
            // the guard. Doing it through the change tracker instead would make two unconditional
            // appends over the same tag conflict with each other, which is a failure neither of them
            // asked for.
            var token = Guid.NewGuid();

            await dcbDbContext.DcbTagHeads
                .Where(head => affectedTags.Contains(head.Tag))
                .ExecuteUpdateAsync(head => head.SetProperty(row => row.Token, token), cancellationToken);
        }

        var written = events.Select(taggedEvent => new DcbEventEntity
        {
            EventType = TypeBindings.GetEventBindingKey(taggedEvent.Event.GetType()),
            Data = DomainSerializer.Current.Serialize(taggedEvent.Event),
            // Added through the navigation so Entity Framework Core fills in the foreign key
            // from the position the database assigns.
            Tags = taggedEvent.Tags
                .Select(tag => new DcbEventTagEntity { Tag = tag.ToString() })
                .ToList()
        }).ToList();

        dcbDbContext.DcbEvents.AddRange(written);

        await dcbDbContext.SaveChangesAsync(cancellationToken);

        // Read before the tracker is cleared: this is where the generated positions live.
        var lastPosition = written.Max(eventEntity => eventEntity.Position);

        dcbDbContext.ChangeTracker.Clear();

        return lastPosition;
    }

    /// <summary>
    /// Classifies whatever an append threw.
    /// </summary>
    private static async Task<Failure> AppendFailure(this IDcbDbContext dcbDbContext, Exception exception,
        string operation, AppendCondition? condition, List<string> affectedTags,
        CancellationToken cancellationToken, TagQuery? boundary = null)
    {
        dcbDbContext.ChangeTracker.Clear();

        if (exception is not DbUpdateConcurrencyException)
        {
            var context = condition?.Query ?? boundary;
            DcbDiagnostics.AddException(exception, operation, context);
            return DcbStoreFailures.StorageFailure(operation, context);
        }

        // A head row this append captured was replaced by an overlapping append that committed
        // first. Only a conditioned append can get here: an unconditional one replaces the tokens
        // without the guard, so it has nothing to be invalidated.
        var query = condition?.Query ?? TagQuery.AnyOf(affectedTags.Select(Tag.Parse).ToArray());
        var expectedPosition = condition?.AfterPosition ?? AppendCondition.NoEvents;

        DcbDiagnostics.AddException(exception, operation, query);

        // Re-read so the caller can retry from the failure without issuing another query. Only on
        // the failure path, so the happy path pays nothing for it.
        var latestPosition = await dcbDbContext.GetLatestPosition(query, cancellationToken: cancellationToken);

        DcbDiagnostics.AddConcurrencyConflictEvent(query, expectedPosition, latestPosition);

        return DcbStoreFailures.ConcurrencyConflict(query, expectedPosition, latestPosition);
    }

    /// <summary>
    /// Creates any tag head rows the append will contend on, before the append's own transaction.
    /// </summary>
    /// <remarks>
    /// A head row carries no domain meaning until an event references its tag, so creating one early
    /// is harmless and creating one twice is not a conflict. It has to happen outside the append's
    /// transaction: two appends introducing the same tag collide on the primary key, and on
    /// PostgreSQL a failed statement aborts the entire transaction it ran in — so tolerating that
    /// race from inside the append would poison the append.
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
