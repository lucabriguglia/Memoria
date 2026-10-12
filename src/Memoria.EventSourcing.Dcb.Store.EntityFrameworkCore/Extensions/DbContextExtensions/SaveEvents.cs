using System.Data.Common;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

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

        if (dcbDbContext.AppendsThroughRoutine(condition))
        {
            return await dcbDbContext.AppendThroughRoutine(events, condition, affectedTags, operation,
                cancellationToken);
        }

        try
        {
            // Outside the catch below, so the strategy is the one that decides what is worth trying
            // again: a classifier inside it would turn every transient failure into a storage
            // failure before the strategy ever saw it.
            return await dcbDbContext.Appending(async token =>
            {
                var heads = await dcbDbContext.ClaimTagHeads(affectedTags, condition, token);

                await using var transaction = await dcbDbContext.Database.BeginTransactionAsync(token);

                var appendResult = await dcbDbContext.AppendCore(events, condition, affectedTags, heads, token);
                if (appendResult.IsNotSuccess)
                {
                    return appendResult.Failure!;
                }

                await transaction.CommitAsync(token);

                return Result.Ok();
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            return await dcbDbContext.AppendFailure(exception, operation, condition, affectedTags,
                cancellationToken);
        }
    }

    /// <summary>
    /// Runs one append through the store's execution strategy, so a deployment that retries
    /// transient failures appends rather than failing.
    /// </summary>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="append">The whole append, from reading the boundary to committing it.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// <para>
    /// EF Core refuses to open a caller's own transaction under a strategy that retries, unless the
    /// whole unit is run through that strategy: a transaction it did not start is one it cannot
    /// start again. An append opens one, so without this every append against a deployment
    /// configured with <c>EnableRetryOnFailure</c> failed — and failed as
    /// <c>memoria/storage-failure</c>, since the refusal is an <c>InvalidOperationException</c> like
    /// any other the append did not expect.
    /// </para>
    /// <para>
    /// The unit starts at the boundary read and the tag head claim, not at the transaction. The
    /// tokens read there are what the append's update is guarded on, and an attempt made again on
    /// tokens a rolled-back attempt read would be guarded on values the store no longer holds — it
    /// would report a conflict with an append nobody made.
    /// </para>
    /// <para>
    /// A boundary that moved leaves as a value rather than an exception, so it is answered once and
    /// never tried again: the caller has to read the boundary and decide afresh, and asking the same
    /// stale question three more times would only charge them for it.
    /// </para>
    /// </remarks>
    private static Task<Result> Appending(this IDcbDbContext dcbDbContext,
        Func<CancellationToken, Task<Result>> append, CancellationToken cancellationToken)
    {
        var attempt = 0;

        return dcbDbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            // EF Core hands the same context to every attempt without resetting it, so what an
            // attempt attached is still tracked when the next one starts and would collide with the
            // rows it attaches. Only between attempts: clearing before the first would throw away
            // whatever the caller had staged on the context, which has always been saved with the
            // append.
            if (attempt++ > 0)
            {
                dcbDbContext.ChangeTracker.Clear();
            }

            return await append(token);
        }, cancellationToken);
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
        AppendCondition? condition, List<string> affectedTags, List<TagHeadProbe> heads,
        CancellationToken cancellationToken)
    {
        if (condition is not null)
        {
            // Every affected tag had a head row when they were claimed, so this reads the boundary
            // again only if they were removed underneath it. Taking the empty case as an empty
            // boundary instead would let an append conditioned on NoEvents through against a
            // boundary full of events.
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
    /// Reads the tag head rows the append will contend on, creating any that do not exist yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One read, not two. Finding out whether a row exists and reading the token it carries are the
    /// same question asked of the same row, so the read that returns the tokens also says which tags
    /// it found — and only the tags it did not are created. The existence check is not skipped or
    /// remembered between appends: a head row that has gone missing is still detected here on every
    /// append, which is what stops an append proceeding with no guard at all. See
    /// <c>TagHeadTests.An_append_restores_a_tag_head_row_that_has_gone_missing</c>.
    /// </para>
    /// <para>
    /// Runs before the append's transaction opens, for two reasons. Creating a row has to happen
    /// outside it — two appends introducing the same tag collide on the primary key, and on
    /// PostgreSQL a failed statement aborts the entire transaction it ran in, so tolerating that race
    /// from inside would poison the append. And the transaction then opens with its answer already in
    /// hand, so the head rows every overlapping append contends on are held for less time.
    /// </para>
    /// <para>
    /// Reading the boundary outside the transaction is safe because it is the token, not the
    /// transaction, that makes the position trustworthy. Any append that could move this boundary
    /// must write under one of its tags, so it must replace a token captured here, so the guarded
    /// update inside the transaction matches nothing and the append is refused. Widening that window
    /// changes which of two already-correct paths reports the conflict, not whether it is reported.
    /// </para>
    /// </remarks>
    private static async Task<List<TagHeadProbe>> ClaimTagHeads(this IDcbDbContext dcbDbContext,
        List<string> affectedTags, AppendCondition? condition, CancellationToken cancellationToken)
    {
        var heads = await dcbDbContext.ReadTagHeads(affectedTags, condition, cancellationToken);

        var missing = affectedTags
            .Except(heads.Select(head => head.Tag), StringComparer.Ordinal)
            .ToList();

        if (missing.Count == 0)
        {
            return heads;
        }

        var creationFailure = await dcbDbContext.CreateTagHeads(missing, cancellationToken);

        // Read again rather than assuming what was just written: the tokens have to be the stored
        // ones, and another append introducing the same tag may have won the race to create it.
        var claimed = await dcbDbContext.ReadTagHeads(affectedTags, condition, cancellationToken);

        // A failed insert is benign only if the heads exist now, because another append created
        // them. Otherwise the append would go ahead with no guard over a tag, so it fails instead.
        if (creationFailure is not null && claimed.Count < affectedTags.Count)
        {
            throw creationFailure;
        }

        return claimed;
    }

    /// <summary>
    /// Reads the stored tag head rows among the given tags, with the boundary position when there is
    /// a condition to check against it.
    /// </summary>
    /// <remarks>
    /// The tokens and the position come back together, so they belong to the same observation by
    /// construction rather than by being ordered. The boundary read rides along as a subquery over
    /// nothing in the head row, so the engine evaluates it once rather than once per tag. An
    /// unconditional append has no boundary to check and asks for no position.
    /// </remarks>
    private static Task<List<TagHeadProbe>> ReadTagHeads(this IDcbDbContext dcbDbContext,
        List<string> affectedTags, AppendCondition? condition, CancellationToken cancellationToken)
    {
        var heads = dcbDbContext.DcbTagHeads.AsNoTracking()
            .Where(head => affectedTags.Contains(head.Tag));

        if (condition is null)
        {
            return heads
                .Select(head => new TagHeadProbe(head.Tag, head.Token, null))
                .ToListAsync(cancellationToken);
        }

        var boundaryPositions = dcbDbContext.PositionsInside(condition.Query);

        return heads
            .Select(head => new TagHeadProbe(head.Tag, head.Token,
                boundaryPositions.Max(position => (long?)position)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates head rows for tags that have none.
    /// </summary>
    /// <returns>The first insert that failed, or null; whether it matters is decided by the re-read.</returns>
    /// <remarks>
    /// <para>
    /// A head row carries no domain meaning until an event references its tag, so creating one early
    /// is harmless and creating one twice is not a conflict.
    /// </para>
    /// <para>
    /// A statement per head rather than a save of the context. A save commits everything tracked,
    /// so changes the caller staged on the context used to be committed here, outside the append's
    /// transaction, whatever the append then decided; and a failure of theirs was taken for
    /// another append having created the head, and swallowed. The statement is built from the
    /// model's own table and column names, so it writes where the context maps the heads.
    /// </para>
    /// </remarks>
    private static async Task<Exception?> CreateTagHeads(this IDcbDbContext dcbDbContext, List<string> missing,
        CancellationToken cancellationToken)
    {
        var heads = dcbDbContext.DcbTagHeads.EntityType;
        var table = StoreObjectIdentifier.Table(heads.GetTableName()!, heads.GetSchema());
        var sql = dcbDbContext.Database.GetService<ISqlGenerationHelper>();

        var tableName = sql.DelimitIdentifier(table.Name, table.Schema);
        var tagColumn = sql.DelimitIdentifier(heads.FindProperty(nameof(DcbTagHeadEntity.Tag))!.GetColumnName(table)!);
        var tokenColumn = sql.DelimitIdentifier(heads.FindProperty(nameof(DcbTagHeadEntity.Token))!.GetColumnName(table)!);

        var insert = $"INSERT INTO {tableName} ({tagColumn}, {tokenColumn}) SELECT {{0}}, {{1}} " +
                     $"WHERE NOT EXISTS (SELECT 1 FROM {tableName} WHERE {tagColumn} = {{0}})";

        Exception? firstFailure = null;

        foreach (var tag in missing)
        {
            try
            {
                await dcbDbContext.Database.ExecuteSqlRawAsync(insert, [tag, Guid.NewGuid()], cancellationToken);
            }
            catch (DbException exception)
            {
                // Most likely another append introduced the same tag between the check and the
                // insert. The re-read that follows decides.
                firstFailure ??= exception;
            }
        }

        return firstFailure;
    }
}
