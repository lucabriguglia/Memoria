using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The <see cref="IDcbDomainService"/> registered by <c>AddMemoriaDcb</c> when no store provider has
/// replaced it. Every member throws, so a missing store registration fails with a message naming the
/// cause rather than a dependency-injection resolution error.
/// </summary>
public class DefaultDcbDomainService : IDcbDomainService
{
    private static string NotImplementedMessage =>
        "No DCB store provider has been configured. Please register one, such as Entity Framework Core via AddMemoriaDcbEntityFrameworkCore.";

    /// <inheritdoc />
    public Task<Result<T?>> GetAggregate<T>(IDcbAggregateId<T> aggregateId, ReadMode readMode,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEvents(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsFromPosition(TagQuery query, long fromPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsUpToPosition(TagQuery query, long upToPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsBetweenPositions(TagQuery query, long fromPosition, long toPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsFromDate(TagQuery query, DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsUpToDate(TagQuery query, DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<List<IEvent>>> GetEventsBetweenDates(TagQuery query, DateTimeOffset fromDate,
        DateTimeOffset toDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<long>> GetLatestPosition(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        long upToPosition, CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T?>> GetProjection<T>(IDcbProjectionId<T> projectionId, ReadMode readMode,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result> SaveProjection<T>(IDcbProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IDcbProjection =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result> SaveAggregate<T>(IDcbAggregateId<T> aggregateId, T aggregate,
        AppendCondition? condition, CancellationToken cancellationToken = default) where T : IDcbAggregateRoot =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result> SaveEvents(TaggedEvent[] events, AppendCondition? condition,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T?>> UpdateAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task<Result<T?>> UpdateProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        throw new NotImplementedException(NotImplementedMessage);

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);
}
