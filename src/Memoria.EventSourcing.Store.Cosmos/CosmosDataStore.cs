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
/// Provides data access operations for the Cosmos DB Event Sourcing store.
/// This class handles the storage and retrieval of aggregates, events, and aggregate event documents in Cosmos DB.
/// </summary>
public class CosmosDataStore : ICosmosDataStore
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Container _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataStore"/> class.
    /// </summary>
    /// <param name="clientProvider">Provides the container backed by the shared Cosmos DB client.</param>
    /// <param name="timeProvider">The time provider for timestamp operations.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving user information.</param>
    public CosmosDataStore(CosmosClientProvider clientProvider, TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
        _container = clientProvider.Container;
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
            DiagnosticsExtensions.AddActivityEvent(response, streamId, aggregateId, operation: "Get Aggregate Document");

            // A point read returns whatever holds that id in that partition, and events,
            // aggregates and projections share both. Reported rather than treated as absent:
            // rebuilding from events and upserting the snapshot would overwrite the colliding
            // document.
            if (response.Resource.DocumentType != DocumentType.Aggregate)
            {
                return CosmosStoreFailures.DocumentIdCollision("Get Aggregate Document", streamId,
                    aggregateDocumentId, DocumentType.Aggregate, response.Resource.DocumentType);
            }

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (AggregateDocument?)null;
        }
        catch (Exception ex)
        {
            const string operation = "Get Aggregate Document";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Retrieves all event documents from a stream, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional array of event properties to filter the results. If null, all event will be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocuments(IStreamId streamId, Type[]? eventTypeFilter,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream between specific sequence numbers, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromSequence">The minimum sequence number to start retrieving events from (inclusive).</param>
    /// <param name="toSequence">The maximum sequence number to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents between the specified sequences, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsBetweenSequences(IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.sequence <= @toSequence AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@fromSequence", fromSequence)
            .WithParameter("@toSequence", toSequence)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents Between Sequences", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream starting from a specific sequence number, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromSequence">The minimum sequence number to start retrieving events from (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents from the specified sequence, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsFromSequence(IStreamId streamId, int fromSequence, Type[]? eventTypeFilter,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence >= @fromSequence AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@fromSequence", fromSequence)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents from Sequence", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream up to a specific sequence number, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="upToSequence">The maximum sequence number to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents up to the specified sequence, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsUpToSequence(IStreamId streamId, int upToSequence, Type[]? eventTypeFilter,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.sequence <= @upToSequence AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@upToSequence", upToSequence)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents up to Sequence", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream up to a specific date, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="upToDate">The maximum date to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents up to the specified date, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsUpToDate(IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate <= @upToDate AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@upToDate", upToDate)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents up to Date", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream starting from a specific date, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromDate">The minimum date to start retrieving events from (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents from the specified date, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsFromDate(IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@fromDate", fromDate)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents from Date", cancellationToken);
    }

    /// <summary>
    /// Retrieves event documents from a stream between specific dates, optionally filtered by event types.
    /// The results are ordered by sequence number.
    /// </summary>
    /// <param name="streamId">The stream identifier to retrieve events from.</param>
    /// <param name="fromDate">The minimum date to start retrieving events from (inclusive).</param>
    /// <param name="toDate">The maximum date to retrieve events up to (inclusive).</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter by. If null or empty, all events are returned.</param>
    /// <param name="eventPropertyFilter">An optional dictionary of event properties to filter the results. If null, no property filtering is applied.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents between the specified dates, or a failure if an error occurred.</returns>
    public async Task<Result<List<EventDocument>>> GetEventDocumentsBetweenDates(IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, IDictionary<string, string>? eventPropertyFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE c.streamId = @streamId AND c.createdDate >= @fromDate AND c.createdDate <= @toDate AND c.documentType = @documentType")
            .AppendEventFilters(eventTypeFilter, eventPropertyFilter)
            .Append(" ORDER BY c.sequence");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@streamId", streamId.Id)
            .WithParameter("@fromDate", fromDate)
            .WithParameter("@toDate", toDate)
            .WithParameter("@documentType", DocumentType.Event)
            .BindEventFilterParameters(eventTypeFilter, eventPropertyFilter);

        return await _container.QueryListAsync<EventDocument>(queryDefinition, streamId,
            operation: "Get Event Documents between Dates", cancellationToken);
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

        var newEventDocumentsResult = await GetEventDocumentsFromSequence(streamId, fromSequence: aggregate.LatestEventSequence + 1, aggregate.EventTypeFilter, cancellationToken: cancellationToken);
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

        AggregateDiagnostics.AddAggregateFoldedEvent(streamId, aggregateId,
            appliedFromSequence: newEventDocuments[0].Sequence,
            appliedToSequence: newEventDocuments[^1].Sequence, appliedCount: newEventDocuments.Count,
            versionBefore: currentAggregateVersion, versionAfter: aggregate.Version);

        if (aggregate.Version == currentAggregateVersion)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newLatestEventSequenceForAggregate = newEventDocuments[^1].Sequence;
        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        var aggregateDocumentToUpdate = aggregate.ToAggregateDocument(streamId, aggregateId, newLatestEventSequenceForAggregate);
        aggregateDocumentToUpdate.CreatedDate = aggregateDocument?.CreatedDate ?? timeStamp;
        aggregateDocumentToUpdate.CreatedBy = aggregateDocument?.CreatedBy ?? currentUserNameIdentifier;
        aggregateDocumentToUpdate.UpdatedDate = timeStamp;
        aggregateDocumentToUpdate.UpdatedBy = currentUserNameIdentifier;

        var writeResult = await _container.WriteAggregateSnapshot(streamId, aggregateId, aggregateDocumentToUpdate,
            operation: "Update Aggregate Document", cancellationToken);

        return writeResult.IsSuccess ? aggregate : writeResult.Failure!;
    }

    /// <summary>
    /// Retrieves a projection document from Cosmos DB for the specified stream and projection.
    /// </summary>
    /// <typeparam name="T">The type of projection to retrieve.</typeparam>
    /// <param name="streamId">The stream identifier containing the projection.</param>
    /// <param name="projectionId">The unique identifier of the projection.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing the projection document if found, null if not found, or a failure if an error occurred.</returns>
    public async Task<Result<ProjectionDocument?>> GetProjectionDocument<T>(IStreamId streamId,
        IProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IProjection, new()
    {
        try
        {
            var projectionDocumentId = projectionId.ToStoreId();

            var response = await _container.ReadItemAsync<ProjectionDocument>(projectionDocumentId,
                new PartitionKey(streamId.Id), cancellationToken: cancellationToken);
            DiagnosticsExtensions.AddActivityEvent(response, streamId, operation: "Get Projection Document");

            if (response.Resource.DocumentType != DocumentType.Projection)
            {
                return CosmosStoreFailures.DocumentIdCollision("Get Projection Document", streamId,
                    projectionDocumentId, DocumentType.Projection, response.Resource.DocumentType);
            }

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (ProjectionDocument?)null;
        }
        catch (Exception ex)
        {
            const string operation = "Get Projection Document";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Applies new events beyond the projection's <c>LatestEventSequence</c> and upserts the updated snapshot.
    /// </summary>
    /// <typeparam name="T">The type of projection to update.</typeparam>
    /// <param name="streamId">The stream identifier containing the projection.</param>
    /// <param name="projectionId">The unique identifier of the projection.</param>
    /// <param name="projectionDocument">The current projection document, or null when no snapshot exists.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result containing the updated projection, null when no events could be applied, or a failure.</returns>
    public async Task<Result<T?>> UpdateProjectionDocument<T>(IStreamId streamId,
        IProjectionId<T> projectionId, ProjectionDocument? projectionDocument,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = projectionDocument is null ? new T() : projectionDocument.ToProjection<T>();

        var currentProjectionVersion = projection.Version;

        var newEventDocumentsResult = await GetEventDocumentsFromSequence(streamId,
            fromSequence: projection.LatestEventSequence + 1, projection.EventTypeFilter, projectionId.EventPropertyFilter,
            cancellationToken: cancellationToken);
        if (newEventDocumentsResult.IsNotSuccess)
        {
            return newEventDocumentsResult.Failure!;
        }

        var newEventDocuments = newEventDocumentsResult.Value!;
        if (newEventDocuments.Count == 0)
        {
            return projection.Version > 0 ? projection : default;
        }

        var newEvents = newEventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        projection.Apply(newEvents);

        ProjectionDiagnostics.AddProjectionFoldedEvent(streamId, projectionId,
            appliedFromSequence: newEventDocuments[0].Sequence,
            appliedToSequence: newEventDocuments[^1].Sequence,
            appliedCount: newEventDocuments.Count, versionBefore: currentProjectionVersion,
            versionAfter: projection.Version);

        if (projection.Version == currentProjectionVersion)
        {
            return projection.Version > 0 ? projection : default;
        }

        projection.LatestEventSequence = newEventDocuments[^1].Sequence;

        var timeStamp = _timeProvider.GetUtcNow();
        var currentUserNameIdentifier = _httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var projectionDocumentToUpsert = projection.ToProjectionDocument(streamId, projectionId);
            projectionDocumentToUpsert.CreatedDate = projectionDocument?.CreatedDate ?? timeStamp;
            projectionDocumentToUpsert.CreatedBy = projectionDocument?.CreatedBy ?? currentUserNameIdentifier;
            projectionDocumentToUpsert.UpdatedDate = timeStamp;
            projectionDocumentToUpsert.UpdatedBy = currentUserNameIdentifier;

            var response = await _container.UpsertItemAsync(projectionDocumentToUpsert,
                new PartitionKey(streamId.Id), WriteRequestOptions.Item, cancellationToken);
            DiagnosticsExtensions.AddActivityEvent(response, streamId, operation: "Update Projection Document");
            return response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
                ? projection
                : StoreFailures.StorageFailure("Update Projection Document", streamId);
        }
        catch (Exception ex)
        {
            const string operation = "Update Projection Document";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }

    /// <summary>
    /// Does nothing. The Cosmos DB client is shared across the application and owned by
    /// <see cref="CosmosClientProvider"/>; disposing it here would tear down connections still in
    /// use by other scopes. <see cref="ICosmosDataStore"/> declares <see cref="IDisposable"/>, so
    /// the method remains.
    /// </summary>
    public void Dispose()
    {
    }
}
