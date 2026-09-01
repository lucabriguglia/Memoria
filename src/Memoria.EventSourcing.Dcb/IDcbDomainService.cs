using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The entry point for reading and appending events under dynamic consistency boundaries — the DCB
/// counterpart of <see cref="IDomainService"/>.
/// </summary>
/// <remarks>
/// The shape mirrors <see cref="IDomainService"/> deliberately, with two substitutions: a
/// <see cref="TagQuery"/> selects events where an <see cref="IStreamId"/> did, and a global
/// <c>position</c> orders them where a per-stream <c>sequence</c> did. Result types, read modes and
/// failure classification are shared, so a caller moving between the two models is changing what a
/// boundary <em>is</em>, not how the API is used.
/// <para>
/// There is no <c>eventPropertyFilter</c>. It existed to pick one aggregate's events out of a
/// shared stream, which is what tags do directly.
/// </para>
/// </remarks>
public interface IDcbDomainService : IDisposable
{
    /// <summary>
    /// Retrieves an aggregate built from the events inside a boundary.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="readMode">How the snapshot and any newer events should be combined.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The aggregate, or null when it does not exist and the read mode does not create it.</returns>
    Task<Result<T?>> GetAggregate<T>(IDcbAggregateId<T> aggregateId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new();

    /// <summary>
    /// Retrieves the events inside a boundary, in position order.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEvents(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary from a position onwards.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="fromPosition">The inclusive lower bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsFromPosition(TagQuery query, long fromPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary up to a position.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="upToPosition">The inclusive upper bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsUpToPosition(TagQuery query, long upToPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary between two positions.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="fromPosition">The inclusive lower bound.</param>
    /// <param name="toPosition">The inclusive upper bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsBetweenPositions(TagQuery query, long fromPosition, long toPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary from a date onwards.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="fromDate">The inclusive lower bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsFromDate(TagQuery query, DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary up to a date.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="upToDate">The inclusive upper bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsUpToDate(TagQuery query, DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the events inside a boundary between two dates.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="fromDate">The inclusive lower bound.</param>
    /// <param name="toDate">The inclusive upper bound.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The events.</returns>
    Task<Result<List<IEvent>>> GetEventsBetweenDates(TagQuery query, DateTimeOffset fromDate, DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest position inside a boundary — the value an
    /// <see cref="AppendCondition"/> is built from.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The latest position, or <see cref="AppendCondition.NoEvents"/> when the boundary is empty.
    /// </returns>
    Task<Result<long>> GetLatestPosition(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Folds the events inside a boundary into a fresh aggregate without persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The aggregate.</returns>
    Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new();

    /// <summary>
    /// Folds the events inside a boundary up to a position into a fresh aggregate, without
    /// persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="upToPosition">The inclusive upper bound.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The aggregate.</returns>
    Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new();

    /// <summary>
    /// Folds the events inside a boundary up to a date into a fresh aggregate, without persisting a
    /// snapshot.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="upToDate">The inclusive upper bound.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The aggregate.</returns>
    Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new();

    /// <summary>
    /// Folds the events inside a boundary into a fresh projection without persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projection.</returns>
    Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new();

    /// <summary>
    /// Folds the events inside a boundary up to a position into a fresh projection, without
    /// persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="upToPosition">The inclusive upper bound.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projection.</returns>
    Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new();

    /// <summary>
    /// Folds the events inside a boundary up to a date into a fresh projection, without persisting a
    /// snapshot.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="upToDate">The inclusive upper bound.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projection.</returns>
    Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new();

    /// <summary>
    /// Retrieves a projection built from the events inside a boundary.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="readMode">How the snapshot and any newer events should be combined.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projection, or null when it does not exist and the read mode does not create it.</returns>
    Task<Result<T?>> GetProjection<T>(IDcbProjectionId<T> projectionId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new();

    /// <summary>
    /// Persists a projection snapshot.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="projection">The projection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The outcome.</returns>
    Task<Result> SaveProjection<T>(IDcbProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IDcbProjection;

    /// <summary>
    /// Appends an aggregate's uncommitted events and persists its snapshot.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="aggregate">The aggregate.</param>
    /// <param name="condition">
    /// The concurrency check. Pass null to append unconditionally, which is only correct when the
    /// decision depended on nothing it read.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The outcome. A boundary that moved since the condition was read fails with
    /// <c>memoria/concurrency-conflict</c>.
    /// </returns>
    Task<Result> SaveAggregate<T>(IDcbAggregateId<T> aggregateId, T aggregate,
        AppendCondition? condition, CancellationToken cancellationToken = default) where T : IDcbAggregateRoot;

    /// <summary>
    /// Appends events directly.
    /// </summary>
    /// <param name="events">The events, with the tags they are appended under.</param>
    /// <param name="condition">
    /// The concurrency check. Pass null to append unconditionally.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The outcome. A boundary that moved since the condition was read fails with
    /// <c>memoria/concurrency-conflict</c>.
    /// </returns>
    Task<Result> SaveEvents(TaggedEvent[] events, AppendCondition? condition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings an aggregate's snapshot up to date with the events appended inside its boundary since
    /// it was written, and persists the result.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The refreshed aggregate, or null when there is nothing to refresh — no snapshot and no events
    /// inside the boundary that this aggregate applies.
    /// </returns>
    /// <remarks>
    /// The same shape as <see cref="IDomainService.UpdateAggregate{T}"/>: read the latest snapshot,
    /// fold what arrived after it, write it back. It appends nothing and takes no
    /// <see cref="AppendCondition"/> — a decision that produces events reads the boundary with
    /// <see cref="GetLatestPosition"/>, folds it, and calls <see cref="SaveAggregate{T}"/> or
    /// <see cref="SaveEvents"/> with a condition built from that position.
    /// </remarks>
    Task<Result<T?>> UpdateAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new();

    /// <summary>
    /// Brings a projection.s snapshot up to date with the events appended inside its boundary since
    /// it was written, and persists the result.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The refreshed projection, or null when there is nothing to refresh.
    /// </returns>
    /// <remarks>
    /// The counterpart of <see cref="UpdateAggregate{T}"/>. A read model differs from a write model
    /// only in never producing events, so every other operation is offered on both.
    /// </remarks>
    Task<Result<T?>> UpdateProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new();
}
