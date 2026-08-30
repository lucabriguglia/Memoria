using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.EventSourcing.Store.Cosmos.Extensions;
using Memoria.Extensions;
using Memoria.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Cosmos DB implementation of the domain service for event sourcing operations.
/// </summary>
public class CosmosDomainService : IDomainService
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Container _container;
    private readonly ICosmosDataStore _cosmosDataStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDomainService"/> class.
    /// </summary>
    /// <param name="clientProvider">Provides the container backed by the shared Cosmos DB client.</param>
    /// <param name="timeProvider">The time provider for timestamps.</param>
    /// <param name="httpContextAccessor">HTTP context accessor for user information.</param>
    /// <param name="cosmosDataStore">The Cosmos data store for document operations.</param>
    public CosmosDomainService(CosmosClientProvider clientProvider, TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor, ICosmosDataStore cosmosDataStore)
    {
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
        _container = clientProvider.Container;
        _cosmosDataStore = cosmosDataStore;
    }

    /// <summary>
    /// Retrieves the aggregate of a specified type associated with a stream and aggregate ID.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate, which must implement <see cref="IAggregateRoot"/> and have a parameterless constructor.</typeparam>
    /// <param name="streamId">The identifier of the event stream containing the aggregate.</param>
    /// <param name="aggregateId">The identifier of the aggregate to retrieve.</param>
    /// <param name="readMode">The mode specifying how the aggregate should be read from the store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/> representing the operation outcome.</returns>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public async Task<Result<T?>> GetAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        ReadMode readMode = ReadMode.SnapshotOnly,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateDocumentResult =
            await _cosmosDataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
        if (aggregateDocumentResult.IsNotSuccess)
        {
            return aggregateDocumentResult.Failure!;
        }

        if (aggregateDocumentResult.Value != null)
        {
            var currentAggregateDocument = aggregateDocumentResult.Value;
            switch (readMode)
            {
                case ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate:
                    return currentAggregateDocument.ToAggregate<T>();
                case ReadMode.SnapshotWithNewEvents or ReadMode.SnapshotWithNewEventsOrCreate:
                    return await _cosmosDataStore.UpdateAggregateDocument(streamId, aggregateId,
                        currentAggregateDocument, cancellationToken);
            }
        }

        if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
        {
            return default(T);
        }

        var aggregate = new T();

        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocuments(streamId, aggregate.EventTypeFilter, aggregateId.EventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return default(T);
        }

        var events = eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        aggregate.Apply(events);
        if (aggregate.Version == 0)
        {
            return default(T);
        }

        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        var latestEventSequenceForAggregate = eventDocuments[^1].Sequence;
        var aggregateDocument =
            aggregate.ToAggregateDocument(streamId, aggregateId, latestEventSequenceForAggregate);
        aggregateDocument.CreatedDate = timeStamp;
        aggregateDocument.CreatedBy = currentUserNameIdentifier;
        aggregateDocument.UpdatedDate = timeStamp;
        aggregateDocument.UpdatedBy = currentUserNameIdentifier;

        var writeResult = await _container.WriteAggregateSnapshot(streamId, aggregateId, aggregateDocument,
            eventDocuments, timeStamp, operation: "Get Aggregate", cancellationToken);

        return writeResult.IsSuccess ? aggregate : writeResult.Failure!;
    }

    /// <summary>
    /// Gets all domain events from a stream with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEvents(IStreamId streamId, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _cosmosDataStore.GetEventDocuments(streamId, eventTypeFilter, eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events that have been applied to a specific aggregate.
    /// </summary>
    /// <typeparam name="T">The type of aggregate.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events applied to the aggregate or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsAppliedToAggregate<T>(IStreamId streamId,
        IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateEventDocumentsResult =
            await _cosmosDataStore.GetAggregateEventDocuments(streamId, aggregateId, cancellationToken);
        if (aggregateEventDocumentsResult.IsNotSuccess)
        {
            return aggregateEventDocumentsResult.Failure!;
        }

        var aggregateEventDocuments = aggregateEventDocumentsResult.Value!;
        if (aggregateEventDocuments.Count == 0)
        {
            return new List<IEvent>();
        }

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocuments(streamId,
            aggregateEventDocuments.Select(ae => ae.EventId).ToArray(), cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!;
        return eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events between two specific sequence numbers with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The starting sequence number (inclusive).</param>
    /// <param name="toSequence">The ending sequence number (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the retrieved domain events.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsBetweenSequences(
        IStreamId streamId,
        int fromSequence,
        int toSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _cosmosDataStore.GetEventDocumentsBetweenSequences(streamId, fromSequence,
            toSequence, eventTypeFilter, eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events from a specific sequence number onwards with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromSequence">The sequence number to start from.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsFromSequence(IStreamId streamId, int fromSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsFromSequence(streamId, fromSequence, eventTypeFilter,
                eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events up to a specific sequence number with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="upToSequence">The sequence number to stop at.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsUpToSequence(IStreamId streamId, int upToSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsUpToSequence(streamId, upToSequence, eventTypeFilter,
                eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events up to a specific date with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="upToDate">The date to stop at.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsUpToDate(IStreamId streamId, DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsUpToDate(streamId, upToDate, eventTypeFilter, eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events from a specific date onwards with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The date to start from.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsFromDate(IStreamId streamId, DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsFromDate(streamId, fromDate, eventTypeFilter, eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets domain events between two specific dates with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="fromDate">The starting date (inclusive).</param>
    /// <param name="toDate">The ending date (inclusive).</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the list of domain events or failure information.</returns>
    public async Task<Result<List<IEvent>>> GetEventsBetweenDates(IStreamId streamId, DateTimeOffset fromDate,
        DateTimeOffset toDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsBetweenDates(streamId, fromDate, toDate, eventTypeFilter,
                eventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    /// <summary>
    /// Gets an in-memory aggregate by applying all relevant domain events from the stream.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate or failure information.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocuments(streamId, aggregate.EventTypeFilter, aggregateId.EventPropertyFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments[^1].Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    /// <summary>
    /// Gets an in-memory aggregate by applying domain events up to a specific sequence number.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToSequence">The sequence number to stop at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate or failure information.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        int upToSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocumentsUpToSequence(streamId, upToSequence,
            aggregate.EventTypeFilter, cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments[^1].Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    /// <summary>
    /// Gets an in-memory aggregate by applying domain events up to a specific date.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="upToDate">The date to stop at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory aggregate or failure information.</returns>
    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocumentsUpToDate(streamId, upToDate, aggregate.EventTypeFilter,
                cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments[^1].Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    /// <summary>
    /// Gets an in-memory projection by folding all matching events from the stream, without
    /// persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of projection to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory projection or failure information.</returns>
    public async Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = new T();

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocuments(streamId, projection.EventTypeFilter,
            cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventDocuments[^1].Sequence;

        return projection;
    }

    /// <summary>
    /// Gets an in-memory projection by folding matching events from the stream up to a specific
    /// sequence, without persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of projection to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="upToSequence">The sequence number to stop at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory projection or failure information.</returns>
    public async Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        int upToSequence, CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = new T();

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocumentsUpToSequence(streamId, upToSequence,
            projection.EventTypeFilter, cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventDocuments[^1].Sequence;

        return projection;
    }

    /// <summary>
    /// Gets an in-memory projection by folding matching events from the stream up to a specific
    /// date, without persisting a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of projection to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="upToDate">The date to stop at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the in-memory projection or failure information.</returns>
    public async Task<Result<T>> GetInMemoryProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = new T();

        var eventDocumentsResult = await _cosmosDataStore.GetEventDocumentsUpToDate(streamId, upToDate,
            projection.EventTypeFilter, cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventDocuments[^1].Sequence;

        return projection;
    }

    /// <summary>
    /// Retrieves a projection for the specified projection identifier, using the selected read mode.
    /// </summary>
    /// <typeparam name="T">The type of projection to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="readMode">The mode in which the projection should be read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection, a null value when no snapshot exists (or, for reconstruction modes, no events could be applied), or failure information.</returns>
    public async Task<Result<T?>> GetProjection<T>(IStreamId streamId, IProjectionId<T> projectionId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IProjection, new()
    {
        var projectionDocumentResult =
            await _cosmosDataStore.GetProjectionDocument(streamId, projectionId, cancellationToken);
        if (projectionDocumentResult.IsNotSuccess)
        {
            return projectionDocumentResult.Failure!;
        }

        if (projectionDocumentResult.Value != null)
        {
            var currentProjectionDocument = projectionDocumentResult.Value;
            switch (readMode)
            {
                case ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate:
                    return currentProjectionDocument.ToProjection<T>();
                case ReadMode.SnapshotWithNewEvents or ReadMode.SnapshotWithNewEventsOrCreate:
                    return await _cosmosDataStore.UpdateProjectionDocument(streamId, projectionId,
                        currentProjectionDocument, cancellationToken);
            }
        }

        if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
        {
            return default(T);
        }

        var projection = new T();

        var eventDocumentsResult =
            await _cosmosDataStore.GetEventDocuments(streamId, projection.EventTypeFilter, cancellationToken: cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }

        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return default(T);
        }

        var events = eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        projection.Apply(events);
        if (projection.Version == 0)
        {
            return default(T);
        }

        projection.LatestEventSequence = eventDocuments[^1].Sequence;

        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var projectionDocument = projection.ToProjectionDocument(streamId, projectionId);
            projectionDocument.CreatedDate = timeStamp;
            projectionDocument.CreatedBy = currentUserNameIdentifier;
            projectionDocument.UpdatedDate = timeStamp;
            projectionDocument.UpdatedBy = currentUserNameIdentifier;

            var response = await _container.UpsertItemAsync(projectionDocument, new PartitionKey(streamId.Id),
                WriteRequestOptions.Item, cancellationToken);
            response.AddActivityEvent(streamId, operation: "Get Projection");
            return response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
                ? projection
                : StoreFailures.StorageFailure("Get Projection", streamId);
        }
        catch (Exception ex)
        {
            const string operation = "Get Projection";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Saves a projection snapshot for the specified projection identifier.
    /// </summary>
    /// <typeparam name="T">The type of projection to save.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="projection">The projection instance to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> SaveProjection<T>(IStreamId streamId, IProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IProjection
    {
        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var projectionDocument = projection.ToProjectionDocument(streamId, projectionId);

            try
            {
                var existing = await _container.ReadItemAsync<ProjectionDocument>(projectionId.ToStoreId(),
                    new PartitionKey(streamId.Id), cancellationToken: cancellationToken);
                projectionDocument.CreatedDate = existing.Resource.CreatedDate;
                projectionDocument.CreatedBy = existing.Resource.CreatedBy;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                projectionDocument.CreatedDate = timeStamp;
                projectionDocument.CreatedBy = currentUserNameIdentifier;
            }

            projectionDocument.UpdatedDate = timeStamp;
            projectionDocument.UpdatedBy = currentUserNameIdentifier;

            var response = await _container.UpsertItemAsync(projectionDocument, new PartitionKey(streamId.Id),
                WriteRequestOptions.Item, cancellationToken);
            response.AddActivityEvent(streamId, operation: "Save Projection");
            return response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
                ? Result.Ok()
                : StoreFailures.StorageFailure("Save Projection", streamId);
        }
        catch (Exception ex)
        {
            const string operation = "Save Projection";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Gets the latest event sequence number for a stream with optional event type filtering.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="eventTypeFilter">Optional filter for specific event types.</param>
    /// <param name="eventPropertyFilter">Optional filter for specific event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the latest event sequence number or failure information.</returns>
    public async Task<Result<int>> GetLatestEventSequence(IStreamId streamId, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT VALUE MAX(c.sequence) FROM c WHERE c.streamId = @streamId AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter);

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        try
        {
            using var iterator = _container.GetItemQueryIterator<int?>(queryDefinition,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(streamId.Id)
                });

            if (!iterator.HasMoreResults)
            {
                return 0;
            }

            var response = await iterator.ReadNextAsync(cancellationToken);
            response.AddActivityEvent(streamId, operation: "Get Latest Event Sequence");
            var result = response.FirstOrDefault();
            return result ?? 0;
        }
        catch (Exception ex)
        {
            const string operation = "Get Latest Event Sequence";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Saves an aggregate with its uncommitted events to the event store.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to save.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="aggregate">The aggregate to save.</param>
    /// <param name="expectedEventSequence">The expected current event sequence for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> SaveAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, T aggregate,
        int expectedEventSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        if (!aggregate.UncommittedEvents.Any())
        {
            return Result.Ok();
        }

        // Rejected here rather than by Cosmos DB, so the caller can tell an oversized save from the
        // store being unreachable. The batch cannot be split: it has to commit atomically with the
        // sequence check below.
        var uncommittedEventCount = aggregate.UncommittedEvents.Count();
        if (uncommittedEventCount > CosmosLimits.MaxUncommittedEventsPerAggregateSave)
        {
            return StoreFailures.BatchLimitExceeded("Save Aggregate", streamId, uncommittedEventCount,
                CosmosLimits.MaxUncommittedEventsPerAggregateSave);
        }

        var latestEventSequenceResult = await GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequenceResult.IsNotSuccess)
        {
            return latestEventSequenceResult.Failure!;
        }

        var latestEventSequence = latestEventSequenceResult.Value;
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return StoreFailures.ConcurrencyConflict(streamId, expectedEventSequence, latestEventSequence);
        }

        var newLatestEventSequenceForAggregate = latestEventSequence + aggregate.UncommittedEvents.Count();
        var currentAggregateVersion = aggregate.Version - aggregate.UncommittedEvents.Count();
        var aggregateIsNew = currentAggregateVersion == 0;

        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var batch = _container.CreateTransactionalBatch(new PartitionKey(streamId.Id));

            var aggregateDocument =
                aggregate.ToAggregateDocument(streamId, aggregateId, newLatestEventSequenceForAggregate);
            aggregateDocument.UpdatedDate = timeStamp;
            aggregateDocument.UpdatedBy = currentUserNameIdentifier;
            if (aggregateIsNew)
            {
                aggregateDocument.CreatedDate = timeStamp;
                aggregateDocument.CreatedBy = currentUserNameIdentifier;
            }
            else
            {
                var existingAggregateDocumentResult =
                    await _cosmosDataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
                if (existingAggregateDocumentResult.IsNotSuccess)
                {
                    return existingAggregateDocumentResult.Failure!;
                }

                var existingAggregateDocument = existingAggregateDocumentResult.Value;
                if (existingAggregateDocument != null)
                {
                    aggregateDocument.CreatedDate = existingAggregateDocument.CreatedDate;
                    aggregateDocument.CreatedBy = existingAggregateDocument.CreatedBy;
                }
                else
                {
                    aggregateDocument.CreatedDate = timeStamp;
                    aggregateDocument.CreatedBy = currentUserNameIdentifier;
                }
            }

            batch.UpsertItem(aggregateDocument, WriteRequestOptions.BatchItem);

            foreach (var @event in aggregate.UncommittedEvents)
            {
                var eventDocument = @event.ToEventDocument(streamId, sequence: ++latestEventSequence);
                eventDocument.CreatedDate = timeStamp;
                eventDocument.CreatedBy = currentUserNameIdentifier;
                batch.CreateItem(eventDocument, WriteRequestOptions.BatchItem);

                var aggregateEventDocument = new AggregateEventDocument
                {
                    Id = $"{aggregateId.ToStoreId()}|{eventDocument.Id}",
                    StreamId = streamId.Id,
                    AggregateId = aggregateId.ToStoreId(),
                    EventId = eventDocument.Id,
                    AppliedDate = timeStamp
                };
                batch.CreateItem(aggregateEventDocument, WriteRequestOptions.BatchItem);
            }

            var batchResponse = await batch.ExecuteAsync(cancellationToken);
            batchResponse.AddActivityEvent(streamId, aggregateId, "Save Aggregate");
            return batchResponse.IsSuccessStatusCode ? Result.Ok() : StoreFailures.StorageFailure("Save Aggregate", streamId);
        }
        catch (Exception ex)
        {
            const string operation = "Save Aggregate";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Saves domain events to the event store.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="events">The domain events to save.</param>
    /// <param name="expectedEventSequence">The expected current event sequence for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> SaveEvents(IStreamId streamId, IEvent[] events, int expectedEventSequence,
        CancellationToken cancellationToken = default)
    {
        if (events.Length == 0)
        {
            return Result.Ok();
        }

        // As in SaveAggregate: this batch commits atomically with the sequence check, so an
        // oversized append is refused up front instead of failing as a storage error.
        if (events.Length > CosmosLimits.MaxEventsPerSave)
        {
            return StoreFailures.BatchLimitExceeded("Save Domain Events", streamId, events.Length,
                CosmosLimits.MaxEventsPerSave);
        }

        var latestEventSequenceResult = await GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequenceResult.IsNotSuccess)
        {
            return latestEventSequenceResult.Failure!;
        }

        var latestEventSequence = latestEventSequenceResult.Value;
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return StoreFailures.ConcurrencyConflict(streamId, expectedEventSequence, latestEventSequence);
        }

        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var batch = _container.CreateTransactionalBatch(new PartitionKey(streamId.Id));
            var eventDocuments = new List<EventDocument>();
            foreach (var @event in events)
            {
                var eventDocument = @event.ToEventDocument(streamId, sequence: ++latestEventSequence);
                eventDocument.CreatedDate = timeStamp;
                eventDocument.CreatedBy = currentUserNameIdentifier;
                eventDocuments.Add(eventDocument);
                batch.CreateItem(eventDocument, WriteRequestOptions.BatchItem);
            }

            var batchResponse = await batch.ExecuteAsync(cancellationToken);
            batchResponse.AddActivityEvent(streamId, eventDocuments, "Save Domain Events");
            return batchResponse.IsSuccessStatusCode ? Result.Ok() : StoreFailures.StorageFailure("Save Domain Events", streamId);
        }
        catch (Exception ex)
        {
            const string operation = "Save Domain Events";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Updates an aggregate by applying new events since its last snapshot.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to update.</typeparam>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the updated aggregate or failure information.</returns>
    public async Task<Result<T?>> UpdateAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateDocumentResult =
            await _cosmosDataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
        if (aggregateDocumentResult.IsNotSuccess)
        {
            return aggregateDocumentResult.Failure!;
        }

        var aggregateDocument = aggregateDocumentResult.Value;
        return await _cosmosDataStore.UpdateAggregateDocument(streamId, aggregateId, aggregateDocument,
            cancellationToken);
    }

    /// <summary>
    /// Does nothing. The Cosmos DB client is shared across the application and owned by
    /// <see cref="CosmosClientProvider"/>; disposing it here would tear down connections still in
    /// use by other scopes. <see cref="IDomainService"/> declares <see cref="IDisposable"/>, so the
    /// method remains.
    /// </summary>
    public void Dispose()
    {
    }
}