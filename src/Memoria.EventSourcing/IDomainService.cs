using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing;

public interface IDomainService : IDisposable
{
    /// <summary>
    /// Retrieves an aggregate of the specified type with the provided identifiers and read mode.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream associated with the aggregate.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to retrieve.</param>
    /// <param name="readMode">The mode in which the aggregate should be read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the aggregate wrapped in a <see cref="Result{TValue}"/>.</returns>
    Task<Result<T?>> GetAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves the domain events associated with the specified stream.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream for which domain events are to be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the domain events. If null, no filtering is applied.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events. If null, no filtering is applied.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a result wrapping a list of domain events.</returns>
    /// <example>
    /// var result = await domainService.GetEvents(streamId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEvents(IStreamId streamId, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of domain events that occurred between the specified sequence numbers.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream from which to retrieve domain events.</param>
    /// <param name="fromSequence">The starting sequence number (inclusive).</param>
    /// <param name="toSequence">The ending sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the retrieved domain events.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsBetweenSequences(streamId, fromSequence, toSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsBetweenSequences(
        IStreamId streamId,
        int fromSequence,
        int toSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves domain events starting from a specified sequence number, optionally filtered by event types.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream to which the domain events belong.</param>
    /// <param name="fromSequence">The sequence number from which to start retrieving domain events.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the retrieved domain events. If null, no filtering is applied.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsFromSequence(streamId, fromSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsFromSequence(IStreamId streamId, int fromSequence,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of domain events up to a specified sequence number from a given stream,
    /// optionally filtered by event types.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream containing the domain events.</param>
    /// <param name="upToSequence">The sequence number up to which events should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the domain events.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsUpToSequence(streamId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsUpToSequence(IStreamId streamId, int upToSequence,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of domain events up to a specified date from a given stream,
    /// optionally filtered by event types.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream containing the domain events.</param>
    /// <param name="upToDate">The date up to which events should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the domain events.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsUpToDate(streamId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsUpToDate(
        IStreamId streamId,
        DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of domain events from a specified date from a given stream,
    /// optionally filtered by event types.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream containing the domain events.</param>
    /// <param name="fromDate">The date from which events should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the domain events.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsFromDate(streamId, fromDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsFromDate(
        IStreamId streamId,
        DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of domain events between two specified dates from a given stream,
    /// optionally filtered by event types.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream containing the domain events.</param>
    /// <param name="fromDate">The starting date from which events should be retrieved.</param>
    /// <param name="toDate">The ending date up to which events should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the domain events.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the domain events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of domain events wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetEventsBetweenDates(streamId, fromDate, toDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </example>
    Task<Result<List<IEvent>>> GetEventsBetweenDates(
        IStreamId streamId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an in-memory aggregate of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the aggregate wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryAggregate&lt;T&gt;(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves an in-memory aggregate of the specified type up to a specified sequence.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to retrieve.</param>
    /// <param name="upToSequence">The sequence number up to which the aggregate should be built.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the aggregate wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryAggregate&lt;T&gt;(streamId, aggregateId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        int upToSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves an in-memory aggregate of the specified type up to a specified date.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to retrieve.</param>
    /// <param name="upToDate">The date up to which the aggregate should be built.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the aggregate wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryAggregate&lt;T&gt;(streamId, aggregateId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves an in-memory projection of the specified type by folding all matching events from
    /// the stream. The rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projection wrapped in a <see cref="Result{TValue}"/>. When no matching events are stored, a projection with <c>Version = 0</c> is returned.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryProjection&lt;T&gt;(streamId, projectionId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IProjection, new();

    /// <summary>
    /// Retrieves an in-memory projection of the specified type by folding matching events from the
    /// stream up to a specified sequence. The rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection to retrieve.</param>
    /// <param name="upToSequence">The sequence number up to which the projection should be built.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projection wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryProjection&lt;T&gt;(streamId, projectionId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        int upToSequence, CancellationToken cancellationToken = default) where T : IProjection, new();

    /// <summary>
    /// Retrieves an in-memory projection of the specified type by folding matching events from the
    /// stream up to a specified date. The rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection to retrieve.</param>
    /// <param name="upToDate">The date up to which the projection should be built.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projection wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetInMemoryProjection&lt;T&gt;(streamId, projectionId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </example>
    Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IProjection, new();

    /// <summary>
    /// Retrieves a projection (read-model) for the specified projection identifier, using the
    /// selected read mode to control how the snapshot and subsequent events are combined.
    /// </summary>
    /// <typeparam name="T">The type of the projection to retrieve.</typeparam>
    /// <param name="streamId">The unique identifier of the stream the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection to retrieve.</param>
    /// <param name="readMode">The mode in which the projection should be read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the projection
    /// wrapped in a <see cref="Result{TValue}"/>, or a null value when no snapshot has been saved
    /// and, for the <see cref="ReadMode"/> variants that reconstruct, no events could be applied.
    /// </returns>
    /// <remarks>
    /// Projections follow the same <see cref="ReadMode"/> semantics as aggregates. When a snapshot
    /// does not exist and the read mode allows reconstruction (<see cref="ReadMode.SnapshotOrCreate"/>
    /// or <see cref="ReadMode.SnapshotWithNewEventsOrCreate"/>), the projection is built by applying
    /// events from the stream and the resulting snapshot is persisted so subsequent reads are cheap.
    /// </remarks>
    /// <example>
    /// var result = await domainService.GetProjection&lt;T&gt;(streamId, projectionId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </example>
    Task<Result<T?>> GetProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IProjection, new();

    /// <summary>
    /// Saves a projection (read-model) snapshot for the specified projection identifier.
    /// </summary>
    /// <typeparam name="T">The type of the projection to save.</typeparam>
    /// <param name="streamId">The unique identifier of the stream the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection to save.</param>
    /// <param name="projection">The projection instance whose state is persisted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains a
    /// <see cref="Result"/> indicating the outcome of the operation.</returns>
    /// <remarks>
    /// A projection produces no events, so saving it upserts only the snapshot (no event stream is
    /// written). The snapshot is stored in the same underlying table/container as aggregates for the
    /// time being.
    /// </remarks>
    /// <example>
    /// var result = await domainService.SaveProjection&lt;T&gt;(streamId, projectionId, projection);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// </example>
    Task<Result> SaveProjection<T>(IStreamId streamId, IProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IProjection;

    /// <summary>
    /// Retrieves the latest event sequence number for a specific stream.
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream for which the latest event sequence is being retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the events to be considered when determining the latest sequence number.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the latest event sequence number wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.GetLatestEventSequence(streamId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var sequence = result.Value;
    /// </example>
    Task<Result<int>> GetLatestEventSequence(IStreamId streamId, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the specified aggregate to the event stream.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to be saved.</typeparam>
    /// <param name="streamId">The unique identifier of the event stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to save.</param>
    /// <param name="aggregate">The aggregate instance containing the state to persist.</param>
    /// <param name="expectedEventSequence">The sequence number of the last known event, used for optimistic concurrency control.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains a <see cref="Result"/> indicating the outcome of the operation.</returns>
    /// <example>
    /// var result = await domainService.SaveAggregate&lt;T&gt;(streamId, aggregateId, aggregate, expectedEventSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// </example>
    Task<Result> SaveAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        T aggregate, int expectedEventSequence, CancellationToken cancellationToken = default)
        where T : IAggregateRoot, new();

    /// <summary>
    /// Saves the specified domain events to the underlying event store.
    /// </summary>
    /// <param name="streamId">The unique identifier representing the stream to which the domain events belong.</param>
    /// <param name="events">An array of domain events to save.</param>
    /// <param name="expectedEventSequence">The expected sequence of events in the stream, ensuring concurrency control.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result is a <see cref="Result"/> indicating success or failure of the operation.</returns>
    /// <example>
    /// var result = await domainService.SaveEvents(streamId, events, expectedEventSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// </example>
    Task<Result> SaveEvents(IStreamId streamId, IEvent[] events, int expectedEventSequence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an aggregate of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate to update.</typeparam>
    /// <param name="streamId">The unique identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the updated aggregate wrapped in a <see cref="Result{TValue}"/>.</returns>
    /// <example>
    /// var result = await domainService.UpdateAggregate&lt;T&gt;(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </example>
    Task<Result<T?>> UpdateAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new();
}