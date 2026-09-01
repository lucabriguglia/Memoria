using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Folds every event inside the identifier's boundary into a fresh aggregate, without persisting
    /// a snapshot.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        IDcbAggregateId<T> aggregateId, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        var aggregate = NewAggregate(aggregateId);

        var eventEntities = await dcbDbContext.GetEventEntities(aggregateId.Boundary, aggregate.EventTypeFilter,
            cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside the boundary up to a position into a fresh aggregate.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        IDcbAggregateId<T> aggregateId, long upToPosition, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        var aggregate = NewAggregate(aggregateId);

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToPosition(aggregateId.Boundary, upToPosition,
            aggregate.EventTypeFilter, cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside the boundary up to a date into a fresh aggregate.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDcbDbContext dcbDbContext,
        IDcbAggregateId<T> aggregateId, DateTimeOffset upToDate, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        var aggregate = NewAggregate(aggregateId);

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToDate(aggregateId.Boundary, upToDate,
            aggregate.EventTypeFilter, cancellationToken);

        return Fold(aggregate, aggregateId, eventEntities);
    }

    /// <summary>
    /// Creates the model with its boundary already set, so <c>Apply</c> can read it.
    /// </summary>
    /// <remarks>
    /// This is how a model spanning more than one entity learns which ones it is about. It also makes
    /// <c>Add</c> without explicit tags append under the boundary the model was folded from, rather
    /// than under whatever the caller remembered to assign.
    /// </remarks>
    private static T NewAggregate<T>(IDcbAggregateId<T> aggregateId) where T : IDcbAggregateRoot, new() =>
        new() { Tags = aggregateId.Boundary.Tags };

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
