using Memoria.EventSourcing.Domain;
using Memoria.Results;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Tracks an array of domain events in the Entity Framework change tracker without persisting to the database.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="events">An array of domain events to track.</param>
    /// <param name="expectedEventSequence">The expected sequence number for concurrency control.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the list of tracked event entities or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.TrackEvents(streamId, events, expectedSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var entities = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<List<EventEntity>>> TrackEvents(this IDomainDbContext domainDbContext, IStreamId streamId, IEvent[] events, int expectedEventSequence, CancellationToken cancellationToken = default)
    {
        if (events.Length == 0)
        {
            return new List<EventEntity>();
        }

        var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return ErrorHandling.DefaultFailure;
        }

        var trackedEntities = domainDbContext.TrackEventEntities(streamId, events, startingEventSequence: latestEventSequence + 1);

        return trackedEntities;
    }
}
