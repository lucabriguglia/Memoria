using Memoria.EventSourcing.Domain;
using Memoria.Extensions;
using Memoria.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using OpenCqrs.EventSourcing.Store.Cosmos.Configuration;
using OpenCqrs.EventSourcing.Store.Cosmos.Documents;
using OpenCqrs.EventSourcing.Store.Cosmos.Extensions;

namespace OpenCqrs.EventSourcing.Store.Cosmos;

/// <summary>
/// Provides data access operations for the Cosmos DB Event Sourcing store.
/// This class handles the storage and retrieval of aggregates, events, and aggregate event documents in Cosmos DB.
/// </summary>
public class CosmosDataStore : ICosmosDataStore
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataStore"/> class.
    /// </summary>
    /// <param name="options">The Cosmos DB configuration options.</param>
    /// <param name="timeProvider">The time provider for timestamp operations.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving user information.</param>
    public CosmosDataStore(IOptions<CosmosOptions> options, TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
        _cosmosClient = new CosmosClient(options.Value.Endpoint, options.Value.AuthKey, options.Value.ClientOptions);
        _container = _cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.ContainerName);
    }

    /// <summary>
    /// Retrieves an aggregate document from Cosmos DB for the specified stream and aggregate.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier containing the aggregate.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing the aggregate document if found, null if not found, or a failure if an error occurred.</returns>
    /// <exception cref="Exception">Thrown when the aggregate type does not have an AggregateType attribute.</exception>
    public async Task<Result<AggregateDocument?>> GetAggregateDocument<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateDocumentId = aggregateId.ToStoreId();

        try
        {
            var response = await _container.ReadItemAsync<AggregateDocument>(aggregateDocumentId, new PartitionKey(streamId.Id), cancellationToken: cancellationToken);
            response.AddActivityEvent(streamId, aggregateId, operation: "Get Aggregate Document");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (AggregateDocument?)null;
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Aggregate Document");
            return ErrorHandling.DefaultFailure;
        }
    }

    /// <summary>
    /// Retrieves all aggregate event documents for a specific aggregate from Cosmos DB.
    /// The results are ordered by applied date.
    /// </summary>
    /// <typeparam name="T">The type of aggregate whose events to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier containing the aggregate.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of aggregate event documents, or a failure if an error occurred.</returns>
    /// <exception cref="Exception">Thrown when the aggregate type does not have an AggregateType attribute.</exception>
    public async Task<Result<List<AggregateEventDocument>>> GetAggregateEventDocuments<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.aggregateId = @aggregateId AND c.documentType = @documentType ORDER BY c.appliedDate";
        var queryDefinition = new QueryDefinition(sql)
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@aggregateId", aggregateId.ToStoreId())
            .WithParameter("@documentType", DocumentType.AggregateEvent);

        var aggregateEventDocuments = new List<AggregateEventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<AggregateEventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                aggregateEventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Aggregate Event Documents");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Aggregate Event Documents");
            return ErrorHandling.DefaultFailure;
        }

        return aggregateEventDocuments;
    }

    /// <summary>
    /// Retrieves all event documents from a stream, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocuments(IStreamId streamId, Type[]? eventTypeFilter, CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves specific event documents from a stream by their identifiers.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="eventIds">An array of event identifiers to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents matching the specified IDs, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocuments(IStreamId streamId, string[] eventIds, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventIds, c.id) ORDER BY c.sequence";
        var queryDefinition = new QueryDefinition(sql)
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@documentType", DocumentType.Event)
            .WithParameter("@eventIds", eventIds);

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents by IDs");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents by IDs");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream between specific sequence numbers, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromSequence">The minimum sequence number to start retrieving events from (inclusive).</param>
    /// <param name="toSequence">The maximum sequence number to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents between the specified sequences, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsBetweenSequences(IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter, CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.sequence <= @toSequence AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromSequence", fromSequence)
                .WithParameter("@toSequence", toSequence)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.sequence <= @toSequence AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromSequence", fromSequence)
                .WithParameter("@toSequence", toSequence)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents Between Sequences");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents from Sequence");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream starting from a specific sequence number, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromSequence">The minimum sequence number to start retrieving events from (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents from the specified sequence, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsFromSequence(IStreamId streamId, int fromSequence, Type[]? eventTypeFilter, CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromSequence", fromSequence)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromSequence", fromSequence)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents from Sequence");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents from Sequence");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream up to a specific sequence number, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="upToSequence">The maximum sequence number to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents up to the specified sequence, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsUpToSequence(IStreamId streamId, int upToSequence, Type[]? eventTypeFilter, CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence <= @upToSequence AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@upToSequence", upToSequence)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence <= @upToSequence AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@upToSequence", upToSequence)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents up to Sequence");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents up to Sequence");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream up to a specific date, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="upToDate">The maximum date to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents up to the specified date, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsUpToDate(IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate <= @upToDate AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@upToDate", upToDate)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate <= @upToDate AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@upToDate", upToDate)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents up to Date");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents up to Sequence");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream starting from a specific date, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromDate">The minimum date to start retrieving events from (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents from the specified date, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsFromDate(IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromDate", fromDate)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromDate", fromDate)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents from Date");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents up to Sequence");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Retrieves event documents from a stream between specific dates, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromDate">The minimum date to start retrieving events from (inclusive).</param>
    /// <param name="toDate">The maximum date to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents between the specified dates, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsBetweenDates(IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        QueryDefinition queryDefinition;

        var filterEventTypes = eventTypeFilter is not null && eventTypeFilter.Length > 0;
        if (!filterEventTypes)
        {
            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.createdDate <= @toDate AND c.documentType = @documentType ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromDate", fromDate)
                .WithParameter("@toDate", toDate)
                .WithParameter("@documentType", DocumentType.Event);
        }
        else
        {
            var eventTypes = eventTypeFilter!
                .Select(eventType => TypeBindings.EventTypeBindings.FirstOrDefault(b => b.Value == eventType))
                .Select(b => b.Key).ToList();

            const string sql = "SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.createdDate <= @toDate AND c.documentType = @documentType AND ARRAY_CONTAINS(@eventTypes, c.eventType) ORDER BY c.sequence";
            queryDefinition = new QueryDefinition(sql)
                .WithParameter("@streamId", streamId.Id)
                .WithParameter("@fromDate", fromDate)
                .WithParameter("@toDate", toDate)
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@eventTypes", eventTypes);
        }

        var eventDocuments = new List<EventDocument>();

        try
        {
            using var iterator = _container.GetItemQueryIterator<EventDocument>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                eventDocuments.AddRange(response);
                response.AddActivityEvent(streamId, operation: "Get Event Documents between Dates");
            }
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Event Documents between Dates");
            return ErrorHandling.DefaultFailure;
        }

        return eventDocuments;
    }

    /// <summary>
    /// Updates an aggregate document by applying new events and storing the updated state in Cosmos DB.
    /// This method retrieves new events since the aggregate's last update, applies them to the aggregate, 
    /// and creates aggregate event documents to track the relationship between the aggregate and events.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to update.</typeparam>
    /// <param name="streamId">The stream identifier containing the aggregate.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="aggregateDocument">The current aggregate document to update.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing the updated aggregate, or a failure if an error occurred.</returns>
    /// <exception cref="Exception">Thrown when the aggregate type does not have an AggregateType attribute.</exception>
    public async Task<Result<T?>> UpdateAggregateDocument<T>(IStreamId streamId, IAggregateId<T> aggregateId, AggregateDocument? aggregateDocument, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = aggregateDocument is null ? new T() : aggregateDocument.ToAggregate<T>();

        var currentAggregateVersion = aggregate.Version;

        var newEventDocumentsResult = await GetEventDocumentsFromSequence(streamId, fromSequence: aggregate.LatestEventSequence + 1, aggregate.EventTypeFilter, cancellationToken);
        if (newEventDocumentsResult.IsNotSuccess)
        {
            return newEventDocumentsResult.Failure!;
        }
        var newEventDocuments = newEventDocumentsResult.Value!;
        if (newEventDocuments.Count == 0)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newEvents = newEventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        aggregate.Apply(newEvents);
        if (aggregate.Version == currentAggregateVersion)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newLatestEventSequenceForAggregate = newEventDocuments.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var batch = _container.CreateTransactionalBatch(new PartitionKey(streamId.Id));

            var aggregateDocumentToUpdate = aggregate.ToAggregateDocument(streamId, aggregateId, newLatestEventSequenceForAggregate);
            aggregateDocumentToUpdate.CreatedDate = aggregateDocument?.CreatedDate ?? timeStamp;
            aggregateDocumentToUpdate.CreatedBy = aggregateDocument?.CreatedBy ?? currentUserNameIdentifier;
            aggregateDocumentToUpdate.UpdatedDate = timeStamp;
            aggregateDocumentToUpdate.UpdatedBy = currentUserNameIdentifier;
            batch.UpsertItem(aggregateDocumentToUpdate);

            foreach (var eventDocument in newEventDocuments)
            {
                var aggregateEventDocument = new AggregateEventDocument
                {
                    Id = $"{aggregateId.ToStoreId()}|{eventDocument.Id}",
                    StreamId = streamId.Id,
                    AggregateId = aggregateId.ToStoreId(),
                    EventId = eventDocument.Id,
                    AppliedDate = timeStamp
                };
                batch.CreateItem(aggregateEventDocument);
            }

            var batchResponse = await batch.ExecuteAsync(cancellationToken);
            batchResponse.AddActivityEvent(streamId, aggregateId, operation: "Update Aggregate Document");
            return batchResponse.IsSuccessStatusCode ? aggregate : ErrorHandling.DefaultFailure;
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Update Aggregate Document");
            return ErrorHandling.DefaultFailure;
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the CosmosDataStore and optionally releases the managed resources.
    /// This method disposes of the Cosmos client connection.
    /// </summary>
    public void Dispose()
    {
        _cosmosClient.Dispose();
    }
}
