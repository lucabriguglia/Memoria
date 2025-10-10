using System.Diagnostics;
using Memoria.EventSourcing.Domain;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for diagnostics in the Entity Framework Core store.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Adds an activity event with specified stream and aggregate information.
    /// </summary>
    /// <param name="streamId">The identifier of the stream associated with the activity event.</param>
    /// <param name="aggregateId">The identifier of the aggregate associated with the activity event.</param>
    /// <param name="name">The name of the activity event to add.</param>
    public static void AddActivityEvent(IStreamId streamId, IAggregateId aggregateId, string name)
    {
        Activity.Current?.AddEvent(new ActivityEvent(name, timestamp: default, tags: new ActivityTagsCollection
        {
            { "streamId", streamId.Id },
            { "aggregateId", aggregateId.Id }
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
    /// Adds an exception to the current activity with stream ID and operation description.
    /// </summary>
    /// <param name="exception">The exception to add.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="operation">The description of the operation.</param>
    public static void AddException(this Exception exception, IStreamId streamId, string operation)
    {
        Activity.Current?.AddException(exception, tags: new TagList
        {
            { "operation", operation },
            { "streamId", streamId.Id }
        });
    }

    /// <summary>
    /// Adds an exception to the current activity with the operation description.
    /// </summary>
    /// <param name="exception">The exception to add.</param>
    /// <param name="operation">The description of the operation.</param>
    public static void AddException(this Exception exception, string operation)
    {
        Activity.Current?.AddException(exception, tags: new TagList
        {
            { "operation", operation }
        });
    }
}
