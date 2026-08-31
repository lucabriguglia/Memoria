using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Folds every event inside a boundary into a fresh aggregate, without persisting a snapshot.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbAggregateId<T> aggregateId, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await dcbDbContext.GetEventEntities(query, aggregate.EventTypeFilter, cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a position into a fresh aggregate.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbAggregateId<T> aggregateId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToPosition(query, upToPosition,
            aggregate.EventTypeFilter, cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a date into a fresh aggregate.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbAggregateId<T> aggregateId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToDate(query, upToDate,
            aggregate.EventTypeFilter, cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    private static T Fold<T>(T aggregate, IDcbAggregateId<T> aggregateId, List<DcbEventEntity> eventEntities)
        where T : IDcbAggregateRoot
    {
        if (eventEntities.Count == 0)
        {
            return aggregate;
        }

        aggregate.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        // Nothing was applied, so there is no identity or position worth claiming — the same
        // decision the streamed store makes, so an aggregate that ignored every event it was given
        // is indistinguishable from one that was given none.
        if (aggregate.Version == 0)
        {
            return aggregate;
        }

        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestPosition = eventEntities[^1].Position;

        return aggregate;
    }
}
