using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of <see cref="IDcbStore"/>.
/// </summary>
/// <remarks>
/// Production correctness of the conditional <see cref="Append(IReadOnlyList{EventToAppend}, AppendCondition, CancellationToken)"/>
/// path requires SERIALIZABLE transaction isolation, or an advisory lock keyed on the
/// query's tag set. The store opens a transaction when one is not already in scope;
/// callers can wrap multiple operations in their own outer transaction if needed.
/// </remarks>
public sealed class EntityFrameworkCoreDcbStore(IDcbDbContext dbContext) : IDcbStore
{
    public async Task<Result<IReadOnlyList<StoredEvent>>> Query(DcbQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Items.Count == 0)
        {
            return Result<IReadOnlyList<StoredEvent>>.Ok(Array.Empty<StoredEvent>());
        }

        var matchingIds = await MatchingEventIds(query, afterPosition: null, cancellationToken);

        var entities = await dbContext.DcbEvents
            .AsNoTracking()
            .Include(e => e.Tags)
            .Where(e => matchingIds.Contains(e.EventId))
            .OrderBy(e => e.GlobalPosition)
            .ToListAsync(cancellationToken);

        IReadOnlyList<StoredEvent> stored = entities.Select(ToStoredEvent).ToList();
        return Result<IReadOnlyList<StoredEvent>>.Ok(stored);
    }

    public Task<Result<AppendResult>> Append(IReadOnlyList<EventToAppend> events, CancellationToken cancellationToken = default) =>
        AppendCore(events, condition: null, cancellationToken);

    public Task<Result<AppendResult>> Append(IReadOnlyList<EventToAppend> events, AppendCondition condition, CancellationToken cancellationToken = default) =>
        AppendCore(events, condition, cancellationToken);

    private async Task<Result<AppendResult>> AppendCore(IReadOnlyList<EventToAppend> events, AppendCondition? condition, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return Result<AppendResult>.Ok(new AppendResult(Array.Empty<StoredEvent>(), 0L));
        }

        if (condition is { } cond)
        {
            var conflictPosition = await FirstConflictPosition(cond, cancellationToken);
            if (conflictPosition is { } observed)
            {
                return new ConcurrencyConflict(cond.Query, cond.AfterPosition, observed);
            }
        }

        var latestPosition = await dbContext.DcbEvents
            .AsNoTracking()
            .MaxAsync(e => (long?)e.GlobalPosition, cancellationToken) ?? 0L;

        var now = DateTimeOffset.UtcNow;
        var appended = new List<StoredEvent>(events.Count);

        foreach (var toAppend in events)
        {
            latestPosition++;
            var eventId = Guid.NewGuid().ToString();
            var eventTypeKey = EventSerialization.GetEventTypeKey(toAppend.Payload);

            var entity = new DcbEventEntity
            {
                EventId = eventId,
                GlobalPosition = latestPosition,
                EventType = eventTypeKey,
                Data = EventSerialization.SerializeData(toAppend.Payload),
                RecordedAt = now,
                Tags = toAppend.Tags.Tags.Select(t => new DcbEventTagEntity
                {
                    EventId = eventId,
                    TagKey = t.Key,
                    TagValue = t.Value,
                    GlobalPosition = latestPosition
                }).ToList()
            };

            dbContext.DcbEvents.Add(entity);
            appended.Add(new StoredEvent(eventId, latestPosition, toAppend.Payload, toAppend.Tags, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<AppendResult>.Ok(new AppendResult(appended, latestPosition));
    }

    private async Task<long?> FirstConflictPosition(AppendCondition condition, CancellationToken cancellationToken)
    {
        var candidateIds = await MatchingEventIds(condition.Query, afterPosition: condition.AfterPosition, cancellationToken);
        if (candidateIds.Count == 0) return null;

        return await dbContext.DcbEvents
            .AsNoTracking()
            .Where(e => candidateIds.Contains(e.EventId))
            .MinAsync(e => (long?)e.GlobalPosition, cancellationToken);
    }

    /// <summary>
    /// Returns the IDs of every event that matches at least one <see cref="QueryItem"/>,
    /// optionally restricted to events with <c>GlobalPosition &gt; afterPosition</c>.
    /// </summary>
    private async Task<List<string>> MatchingEventIds(DcbQuery query, long? afterPosition, CancellationToken cancellationToken)
    {
        var collected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in query.Items)
        {
            var ids = await EventIdsMatchingItem(item, afterPosition, cancellationToken);
            collected.UnionWith(ids);
        }

        return collected.ToList();
    }

    private async Task<List<string>> EventIdsMatchingItem(QueryItem item, long? afterPosition, CancellationToken cancellationToken)
    {
        var typeKeys = item.Types.Select(EventTypeKeyFromClr).ToList();

        if (item.Tags.Tags.Count == 0)
        {
            var eventsQuery = dbContext.DcbEvents.AsNoTracking().AsQueryable();
            if (typeKeys.Count > 0)
            {
                eventsQuery = eventsQuery.Where(e => typeKeys.Contains(e.EventType));
            }
            if (afterPosition is { } pos)
            {
                eventsQuery = eventsQuery.Where(e => e.GlobalPosition > pos);
            }
            return await eventsQuery.Select(e => e.EventId).ToListAsync(cancellationToken);
        }

        var tagPairs = item.Tags.Tags.Select(t => new { t.Key, t.Value }).ToList();
        var requiredTagCount = tagPairs.Count;

        var matchingTagIds = dbContext.DcbEventTags
            .AsNoTracking()
            .Where(t => tagPairs.Select(p => p.Key).Contains(t.TagKey)
                        && tagPairs.Select(p => p.Value).Contains(t.TagValue));

        if (afterPosition is { } position)
        {
            matchingTagIds = matchingTagIds.Where(t => t.GlobalPosition > position);
        }

        // Filter to exact (key,value) pairs; SQLite cannot translate tuple Contains.
        var perKey = tagPairs.GroupBy(p => p.Key).ToList();
        foreach (var group in perKey)
        {
            var key = group.Key;
            var values = group.Select(g => g.Value).ToList();
            matchingTagIds = matchingTagIds.Where(t => t.TagKey != key || values.Contains(t.TagValue));
        }

        var grouped = await matchingTagIds
            .GroupBy(t => t.EventId)
            .Where(g => g.Count() >= requiredTagCount)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        if (typeKeys.Count == 0)
        {
            return grouped;
        }

        return await dbContext.DcbEvents
            .AsNoTracking()
            .Where(e => grouped.Contains(e.EventId) && typeKeys.Contains(e.EventType))
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken);
    }

    private static string EventTypeKeyFromClr(Type clrType)
    {
        var attribute = (Domain.EventType?)Attribute.GetCustomAttribute(clrType, typeof(Domain.EventType));
        if (attribute is null)
        {
            throw new InvalidOperationException($"Type '{clrType.FullName}' is missing the [EventType] attribute.");
        }
        return Domain.TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version);
    }

    private static StoredEvent ToStoredEvent(DcbEventEntity entity)
    {
        var payload = EventSerialization.DeserializeData(entity.EventType, entity.Data);
        var tags = entity.Tags.Count == 0
            ? TagSet.Empty
            : TagSet.Of(entity.Tags.Select(t => new Tag(t.TagKey, t.TagValue)).ToArray());
        return new StoredEvent(entity.EventId, entity.GlobalPosition, payload, tags, entity.RecordedAt);
    }
}
