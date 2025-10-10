using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of the domain service for managing aggregates and domain events.
/// </summary>
public class EntityFrameworkCoreDomainService(IDomainDbContext domainDbContext) : IDomainService
{
    /// <summary>
    /// Retrieves an aggregate from the specified stream, applying the selected read mode.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve, which must implement IAggregateRoot.</typeparam>
    /// <param name="streamId">The identifier of the stream the aggregate is associated with.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate to retrieve.</param>
    /// <param name="readMode">The read mode determining how the aggregate is populated (e.g., from snapshots or events).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing a result with the retrieved aggregate.</returns>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public async Task<Result<T?>> GetAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, ReadMode readMode = ReadMode.SnapshotOnly,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.GetAggregate(streamId, aggregateId, readMode, cancellationToken);
    }

    /// <summary>
    /// Gets domain events from the specified stream with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events.</returns>
    public async Task<Result<List<IEvent>>> GetEvents(IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEvents(streamId, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events that have been applied to a specific aggregate.
    /// </summary>
    /// <typeparam name="T">The type of aggregate.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events applied to the aggregate.</returns>
    public async Task<Result<List<IEvent>>> GetEventsAppliedToAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.GetEventsAppliedToAggregate(aggregateId, cancellationToken);
    }

    /// <summary>
    /// Gets domain events between two specific sequence numbers with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The starting sequence number (inclusive).</param>
    /// <param name="toSequence">The ending sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the retrieved domain events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events between the specified sequences.</returns>
    public async Task<Result<List<IEvent>>> GetEventsBetweenSequences(IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsBetweenSequences(streamId, fromSequence, toSequence, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events from a specific sequence number onwards with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The sequence number to start from.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events from the specified sequence.</returns>
    public async Task<Result<List<IEvent>>> GetEventsFromSequence(IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsFromSequence(streamId, fromSequence, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events up to a specific sequence number with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="upToSequence">The sequence number to read up to.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events up to the specified sequence.</returns>
    public async Task<Result<List<IEvent>>> GetEventsUpToSequence(IStreamId streamId, int upToSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsUpToSequence(streamId, upToSequence, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events up to a specific date with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="upToDate">The date to read up to.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events up to the specified date.</returns>
    public async Task<Result<List<IEvent>>> GetEventsUpToDate(IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsUpToDate(streamId, upToDate, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events from a specific date onwards with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The date to start from.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events from the specified date.</returns>
    public async Task<Result<List<IEvent>>> GetEventsFromDate(IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsFromDate(streamId, fromDate, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets domain events between two specific dates with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The starting date (inclusive).</param>
    /// <param name="toDate">The ending date (inclusive).</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events between the specified dates.</returns>
    public async Task<Result<List<IEvent>>> GetEventsBetweenDates(IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetEventsBetweenDates(streamId, fromDate, toDate, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Gets an in-memory aggregate from the specified stream.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.GetInMemoryAggregate(streamId, aggregateId, cancellationToken);
    }

    /// <summary>
    /// Gets an in-memory aggregate from the specified stream up to a specific sequence number.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToSequence">The sequence number to read up to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate up to the specified sequence.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, int upToSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.GetInMemoryAggregate(streamId, aggregateId, upToSequence, cancellationToken);
    }

    /// <summary>
    /// Gets an in-memory aggregate from the specified stream up to a specific date.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToDate">The date to read up to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate up to the specified date.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.GetInMemoryAggregate(streamId, aggregateId, upToDate, cancellationToken);
    }

    /// <summary>
    /// Gets the latest event sequence number from the specified stream with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="eventTypeFilter">Optional array of event types to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the latest event sequence number.</returns>
    public async Task<Result<int>> GetLatestEventSequence(IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.GetLatestEventSequence(streamId, eventTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Saves an aggregate to the specified stream with expected event sequence validation.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to save.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="aggregate">The aggregate to save.</param>
    /// <param name="expectedEventSequence">The expected event sequence for optimistic concurrency control.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public async Task<Result> SaveAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, int expectedEventSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence, cancellationToken);
    }

    /// <summary>
    /// Saves domain events to the specified stream with expected event sequence validation.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="events">The domain events to save.</param>
    /// <param name="expectedEventSequence">The expected event sequence for optimistic concurrency control.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public async Task<Result> SaveEvents(IStreamId streamId, IEvent[] events, int expectedEventSequence, CancellationToken cancellationToken = default)
    {
        return await domainDbContext.SaveEvents(streamId, events, expectedEventSequence, cancellationToken);
    }

    /// <summary>
    /// Updates an aggregate from the specified stream.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to update.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the updated aggregate.</returns>
    public async Task<Result<T?>> UpdateAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        return await domainDbContext.UpdateAggregate(streamId, aggregateId, cancellationToken);
    }

    /// <summary>
    /// Disposes the domain service and its underlying database context.
    /// </summary>
    public void Dispose() => domainDbContext.Dispose();
}
