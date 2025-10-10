using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Saves an array of domain events directly to the event store with optimistic concurrency control.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="events">An array of domain events to save.</param>
    /// <param name="expectedEventSequence">The expected sequence number for concurrency control.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating the success or failure of the save operation.</returns>
    /// <example>
    /// <code>
    /// var result = await context.SaveEvents(streamId, events, expectedSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// // Save successful
    /// </code>
    /// </example>
    public static async Task<Result> SaveEvents(this IDomainDbContext domainDbContext, IStreamId streamId, IEvent[] events, int expectedEventSequence, CancellationToken cancellationToken = default)
    {
        try
        {
            var trackResult = await domainDbContext.TrackEvents(streamId, events, expectedEventSequence, cancellationToken);
            if (trackResult.IsNotSuccess)
            {
                return trackResult.Failure!;
            }

            await domainDbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Save Domain Events");
            return ErrorHandling.DefaultFailure;
        }
    }
}
