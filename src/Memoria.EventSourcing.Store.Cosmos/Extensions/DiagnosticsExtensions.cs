using System.Diagnostics;
using Memoria.EventSourcing.Domain;
using Microsoft.Azure.Cosmos;
using OpenCqrs.EventSourcing.Store.Cosmos.Documents;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Provides extension methods for adding diagnostic information to activities.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Adds an activity event for a CosmosDB batch response with aggregate information.
    /// </summary>
    /// <param name="batchResponse">The transactional batch response from CosmosDB.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="operation">The operation being performed.</param>
    public static void AddActivityEvent<T>(this TransactionalBatchResponse batchResponse, IStreamId streamId, IAggregateId<T> aggregateId, string operation) where T : IAggregateRoot
    {
        Activity.Current?.AddEvent(new ActivityEvent("Cosmos Transactional Batch", default, new ActivityTagsCollection
        {
            { "operation", operation },
            { "streamId", streamId.Id },
            { "aggregateId", aggregateId.ToStoreId() },
            { "cosmos.activityId", batchResponse.ActivityId },
            { "cosmos.statusCode", batchResponse.StatusCode },
            { "cosmos.errorMessage", batchResponse.ErrorMessage },
            { "cosmos.requestCharge", batchResponse.RequestCharge },
            { "cosmos.count", batchResponse.Count }
        }));
    }

    /// <summary>
    /// Adds an activity event for a CosmosDB batch response with event document information.
    /// </summary>
    /// <param name="batchResponse">The transactional batch response from CosmosDB.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="eventDocuments">The collection of event documents processed in the batch.</param>
    /// <param name="operation">The operation being performed.</param>
    public static void AddActivityEvent(this TransactionalBatchResponse batchResponse, IStreamId streamId, IEnumerable<EventDocument> eventDocuments, string operation)
    {
        Activity.Current?.AddEvent(new ActivityEvent(name: "Cosmos Transactional Batch", timestamp: default, new ActivityTagsCollection
        {
            { "operation", operation },
            { "streamId", streamId.Id },
            { "eventDocumentIds", string.Join(", ", eventDocuments.Select(document => document.Id))},
            { "cosmos.activityId", batchResponse.ActivityId },
            { "cosmos.statusCode", batchResponse.StatusCode },
            { "cosmos.errorMessage", batchResponse.ErrorMessage },
            { "cosmos.requestCharge", batchResponse.RequestCharge },
            { "cosmos.count", batchResponse.Count }
        }));
    }

    /// <summary>
    /// Adds an activity event for a CosmosDB item response with aggregate information.
    /// </summary>
    /// <param name="itemResponse">The item response from CosmosDB.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="operation">The operation being performed.</param>
    public static void AddActivityEvent<T>(this ItemResponse<AggregateDocument> itemResponse, IStreamId streamId, IAggregateId<T> aggregateId, string operation) where T : IAggregateRoot
    {
        Activity.Current?.AddEvent(new ActivityEvent("Cosmos Read Item", default, new ActivityTagsCollection
        {
            { "operation", operation },
            { "streamId", streamId.Id },
            { "aggregateId", aggregateId.ToStoreId() },
            { "cosmos.activityId", itemResponse.ActivityId },
            { "cosmos.statusCode", itemResponse.StatusCode },
            { "cosmos.requestCharge", itemResponse.RequestCharge }
        }));
    }

    /// <summary>
    /// Adds an activity event for a CosmosDB feed response with stream information.
    /// </summary>
    /// <param name="feedResponse">The feed response from CosmosDB.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="operation">The operation being performed.</param>
    public static void AddActivityEvent<T>(this FeedResponse<T> feedResponse, IStreamId streamId, string operation)
    {
        Activity.Current?.AddEvent(new ActivityEvent(name: "Cosmos Feed Iterator", timestamp: default, new ActivityTagsCollection
        {
            { "operation", operation },
            { "streamId", streamId.Id },
            { "cosmos.activityId", feedResponse.ActivityId },
            { "cosmos.statusCode", feedResponse.StatusCode },
            { "cosmos.requestCharge", feedResponse.RequestCharge },
            { "cosmos.count", feedResponse.Count }
        }));
    }

    /// <summary>
    /// Adds an activity event for concurrency exceptions with sequence information.
    /// </summary>
    /// <param name="streamId">The stream identifier where the concurrency exception occurred.</param>
    /// <param name="expectedEventSequence">The expected event sequence number.</param>
    /// <param name="latestEventSequence">The actual latest event sequence number.</param>
    public static void AddActivityEvent(IStreamId streamId, int expectedEventSequence, int latestEventSequence)
    {
        Activity.Current?.AddEvent(new ActivityEvent(name: "Concurrency Exception", timestamp: default, tags: new ActivityTagsCollection
        {
            { "streamId", streamId.Id },
            { "expectedEventSequence", expectedEventSequence },
            { "latestEventSequence", latestEventSequence }
        }));
    }

    /// <summary>
    /// Adds exception information to the current activity with stream and operation context.
    /// </summary>
    /// <param name="exception">The exception to add to the activity.</param>
    /// <param name="streamId">The stream identifier associated with the operation.</param>
    /// <param name="operation">A description of the operation that caused the exception.</param>
    public static void AddException(this Exception exception, IStreamId streamId, string operation)
    {
        Activity.Current?.AddException(exception, tags: new TagList
        {
            { "operation", operation },
            { "streamId", streamId.Id }
        });
    }
}
