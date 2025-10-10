using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

/// <summary>
/// Provides extension methods for IDomainDbContext.
/// </summary>
public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves an in-memory aggregate for a given stream and aggregate ID.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the aggregate.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryAggregate(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await domainDbContext.GetEventEntities(streamId, aggregate.EventTypeFilter, cancellationToken);
        if (eventEntities.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    /// <summary>
    /// Retrieves an in-memory aggregate for a given stream and aggregate ID up to a specified sequence.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToSequence">The maximum sequence number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the aggregate.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryAggregate(streamId, aggregateId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, int upToSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await domainDbContext.GetEventEntitiesUpToSequence(streamId, upToSequence, aggregate.EventTypeFilter, cancellationToken);
        if (eventEntities.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    /// <summary>
    /// Retrieves an in-memory aggregate for a given stream and aggregate ID up to a specified date.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToDate">The maximum date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the aggregate.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryAggregate(streamId, aggregateId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventEntities = await domainDbContext.GetEventEntitiesUpToDate(streamId, upToDate, aggregate.EventTypeFilter, cancellationToken);
        if (eventEntities.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }
}
