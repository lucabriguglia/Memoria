using System.Diagnostics;
using System.Globalization;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Failures returned by DCB store providers.
/// </summary>
/// <remarks>
/// The <see cref="Failure.Type"/> constants are deliberately the ones
/// <see cref="EventSourcing.StoreFailures"/> already defines, not new ones. A concurrency conflict
/// means the same thing and warrants the same response in either consistency model, so a caller
/// matching on <c>memoria/concurrency-conflict</c> — or a retry policy built around it — works
/// unchanged against both. Only the tags differ, because a DCB boundary is a tag query rather than
/// a stream.
/// </remarks>
public static class DcbStoreFailures
{
    /// <summary>
    /// The boundary moved between the decision reading it and the append.
    /// </summary>
    /// <param name="query">The consistency boundary that was asserted over.</param>
    /// <param name="expectedPosition">The position the caller read.</param>
    /// <param name="latestPosition">The position the boundary is actually at.</param>
    /// <returns>The failure.</returns>
    public static Failure ConcurrencyConflict(TagQuery query, long expectedPosition, long latestPosition) =>
        new(ErrorCode.Conflict,
            Title: "Concurrency conflict",
            Description:
            $"Expected nothing matching '{query}' to have been appended since position {expectedPosition}, but the boundary is at {latestPosition}. Reload and retry.",
            Type: EventSourcing.StoreFailures.ConcurrencyConflictType,
            Tags: WithTraceId(new Dictionary<string, string>
            {
                ["tagQuery"] = query.ToString(),
                ["expectedPosition"] = expectedPosition.ToString(CultureInfo.InvariantCulture),
                ["latestPosition"] = latestPosition.ToString(CultureInfo.InvariantCulture)
            }));

    /// <summary>
    /// The store could not complete the operation.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="query">The consistency boundary involved, when there is one.</param>
    /// <returns>The failure.</returns>
    /// <remarks>
    /// Provider exception detail is deliberately excluded: it names tables, columns and constraints,
    /// and a <see cref="Failure"/> mapped onto an HTTP response would disclose it. That detail is
    /// recorded on the current <see cref="Activity"/> instead.
    /// </remarks>
    public static Failure StorageFailure(string operation, TagQuery? query = null)
    {
        var tags = new Dictionary<string, string> { ["operation"] = operation };

        if (query is not null)
        {
            tags["tagQuery"] = query.ToString();
        }

        return new Failure(ErrorCode.Error,
            Title: "Storage failure",
            Description: $"The store could not complete the '{operation}' operation.",
            Type: EventSourcing.StoreFailures.StorageFailureType,
            Tags: WithTraceId(tags));
    }

    /// <summary>
    /// The caller supplied more events than the store commits in one atomic append.
    /// </summary>
    /// <param name="operation">The operation that was refused.</param>
    /// <param name="requested">The number of events supplied.</param>
    /// <param name="maximum">The number the store accepts.</param>
    /// <returns>The failure.</returns>
    public static Failure BatchLimitExceeded(string operation, int requested, int maximum) =>
        new(ErrorCode.BadRequest,
            Title: "Batch limit exceeded",
            Description:
            $"The '{operation}' operation supplied {requested} events but the store commits at most {maximum} in one atomic append. Split the work across several calls.",
            Type: EventSourcing.StoreFailures.BatchLimitExceededType,
            Tags: WithTraceId(new Dictionary<string, string>
            {
                ["operation"] = operation,
                ["requestedEventCount"] = requested.ToString(CultureInfo.InvariantCulture),
                ["maximumEventCount"] = maximum.ToString(CultureInfo.InvariantCulture)
            }));

    private static IDictionary<string, string> WithTraceId(Dictionary<string, string> tags)
    {
        var activity = Activity.Current;

        if (activity is not null)
        {
            tags["traceId"] = activity.TraceId.ToString();
        }

        return tags;
    }
}
