using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.Cosmos;

public interface ICosmosDataStore : IDisposable
{
    /// <summary>
    /// Retrieves an aggregate document from the Cosmos data store.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="streamId">The identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing the retrieved aggregate document or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetAggregateDocument&lt;T&gt;(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var document = result.Value;
    /// </example>
    Task<Result<AggregateDocument?>> GetAggregateDocument<T>(IStreamId streamId,
        IAggregateId<T> aggregateId, CancellationToken cancellationToken = default)
        where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves the event documents associated with an aggregate from the Cosmos data store.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="streamId">The identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing the retrieved list of aggregate event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetAggregateEventDocuments&lt;T&gt;(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<AggregateEventDocument>>> GetAggregateEventDocuments<T>(IStreamId streamId,
        IAggregateId<T> aggregateId, CancellationToken cancellationToken = default)
        where T : IAggregateRoot, new();

    /// <summary>
    /// Retrieves a list of event documents from the Cosmos data store for a specific stream.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the events are to be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results. If null, all event types will be retrieved.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocuments(streamId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocuments(IStreamId streamId, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store based on a stream and a set of event identifiers.
    /// </summary>
    /// <param name="streamId">The identifier of the stream to which the events belong.</param>
    /// <param name="eventIds">An array of event identifiers to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of retrieved event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocuments(streamId, eventIds);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocuments(IStreamId streamId, string[] eventIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store between specified sequence numbers.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the event documents are retrieved.</param>
    /// <param name="fromSequence">The starting sequence number.</param>
    /// <param name="toSequence">The ending sequence number.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsBetweenSequences(streamId, fromSequence, toSequence, eventTypeFilter);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsBetweenSequences(IStreamId streamId, int fromSequence,
        int toSequence, Type[]? eventTypeFilter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of event documents from the specified sequence in the Cosmos data store.
    /// </summary>
    /// <param name="streamId">The identifier of the stream for which to retrieve event documents.</param>
    /// <param name="fromSequence">The sequence number from which to start retrieving event documents.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results. If null, all event types are included.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation of the operation.</param>
    /// <returns>A result containing a list of retrieved event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsFromSequence(streamId, fromSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsFromSequence(IStreamId streamId, int fromSequence,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store up to a specified sequence number.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the event documents are retrieved.</param>
    /// <param name="upToSequence">The maximum sequence number up to which the event documents should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the retrieved documents.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents retrieved or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsUpToSequence(streamId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsUpToSequence(IStreamId streamId, int upToSequence,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store up to a specified date.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the event documents are retrieved.</param>
    /// <param name="upToDate">The date up to which the event documents should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsUpToDate(streamId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsUpToDate(
        IStreamId streamId,
        DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store from a specified date.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the event documents are retrieved.</param>
    /// <param name="fromDate">The date from which the event documents should be retrieved.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsFromDate(streamId, fromDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsFromDate(
        IStreamId streamId,
        DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves event documents from the Cosmos data store between specified dates.
    /// </summary>
    /// <param name="streamId">The identifier of the stream from which the event documents are retrieved.</param>
    /// <param name="fromDate">The starting date.</param>
    /// <param name="toDate">The ending date.</param>
    /// <param name="eventTypeFilter">An optional array of event types to filter the results.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing a list of event documents or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.GetEventDocumentsBetweenDates(streamId, fromDate, toDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var documents = result.Value;
    /// </example>
    Task<Result<List<EventDocument>>> GetEventDocumentsBetweenDates(
        IStreamId streamId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing aggregate document in the Cosmos data store.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="streamId">The identifier of the stream to which the aggregate belongs.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="aggregateDocument">The aggregate document containing updated data.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A result containing the updated aggregate or a failure.</returns>
    /// <example>
    /// var result = await cosmosDataStore.UpdateAggregateDocument&lt;T&gt;(streamId, aggregateId, aggregateDocument);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </example>
    Task<Result<T?>> UpdateAggregateDocument<T>(IStreamId streamId,
        IAggregateId<T> aggregateId, AggregateDocument? aggregateDocument,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new();
}
