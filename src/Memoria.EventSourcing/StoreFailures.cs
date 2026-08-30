using System.Diagnostics;
using System.Globalization;
using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing;

/// <summary>
/// Builds the failures every event store provider returns, so that a caller can tell a concurrency
/// conflict from a storage fault without knowing which provider is behind <see cref="IDomainService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Tags carry only the caller's own context — the stream they addressed and the sequences they
/// supplied — plus the current trace id where there is one. Provider exception detail is deliberately
/// absent: it names tables, columns and constraints, varies by engine and locale, and a consumer
/// mapping a <see cref="Failure"/> onto an HTTP response would disclose it without meaning to. That
/// detail is recorded on the current <see cref="Activity"/> for operators instead.
/// </para>
/// </remarks>
public static class StoreFailures
{
    /// <summary>Stable identifier for a concurrency conflict, safe to branch on.</summary>
    public const string ConcurrencyConflictType = "memoria/concurrency-conflict";

    /// <summary>Stable identifier for a storage-level fault, safe to branch on.</summary>
    public const string StorageFailureType = "memoria/storage-failure";

    /// <summary>
    /// The stream moved on between reading its latest sequence and appending to it. Retryable:
    /// reload the aggregate and reapply the decision against <c>latestEventSequence</c>.
    /// </summary>
    public static Failure ConcurrencyConflict(IStreamId streamId, int expectedEventSequence, int latestEventSequence) =>
        new(ErrorCode.Conflict,
            Title: "Concurrency conflict",
            Description:
            $"Expected stream '{streamId.Id}' to be at sequence {expectedEventSequence} but it is at {latestEventSequence}. Reload and retry.",
            Type: ConcurrencyConflictType,
            Tags: WithTraceId(new Dictionary<string, string>
            {
                ["streamId"] = streamId.Id,
                ["expectedEventSequence"] = expectedEventSequence.ToString(CultureInfo.InvariantCulture),
                ["latestEventSequence"] = latestEventSequence.ToString(CultureInfo.InvariantCulture)
            }));

    /// <summary>
    /// The store could not complete the operation. Not retryable without addressing the cause; the
    /// provider's own exception is on the current <see cref="Activity"/>.
    /// </summary>
    /// <summary>
    /// The write was larger than the store can commit in one atomic unit.
    /// </summary>
    public const string BatchLimitExceededType = "memoria/batch-limit-exceeded";

    /// <summary>
    /// Creates a failure for a write the store cannot commit atomically because it exceeds a
    /// provider limit.
    /// </summary>
    /// <param name="operation">The operation that was rejected.</param>
    /// <param name="streamId">The stream the write targeted.</param>
    /// <param name="requested">How many events the caller supplied.</param>
    /// <param name="maximum">The most the provider can accept in one write.</param>
    /// <returns>A failure describing the limit.</returns>
    /// <remarks>
    /// <see cref="ErrorCode.BadRequest"/> rather than <see cref="ErrorCode.Error"/>: the store is
    /// healthy and retrying the same call cannot help. The caller has to split the work, so this
    /// must be distinguishable from a storage failure.
    /// </remarks>
    public static Failure BatchLimitExceeded(string operation, IStreamId streamId, int requested, int maximum) =>
        new(ErrorCode.BadRequest,
            Title: "Batch limit exceeded",
            Description:
            $"The '{operation}' operation supplied {requested} events for stream '{streamId.Id}' but the store commits at most {maximum} in one atomic write. Split the work across several calls.",
            Type: BatchLimitExceededType,
            Tags: WithTraceId(new Dictionary<string, string>
            {
                ["operation"] = operation,
                ["streamId"] = streamId.Id,
                ["requestedEventCount"] = requested.ToString(CultureInfo.InvariantCulture),
                ["maximumEventCount"] = maximum.ToString(CultureInfo.InvariantCulture)
            }));

    public static Failure StorageFailure(string operation, IStreamId? streamId = null)
    {
        var tags = new Dictionary<string, string> { ["operation"] = operation };

        if (streamId is not null)
        {
            tags["streamId"] = streamId.Id;
        }

        return new Failure(ErrorCode.Error,
            Title: "Storage failure",
            Description: $"The store could not complete the '{operation}' operation.",
            Type: StorageFailureType,
            Tags: WithTraceId(tags));
    }

    /// <summary>
    /// Adds the current trace id when there is one, so a reported failure leads an operator to the
    /// recorded exception without carrying any of its detail.
    /// </summary>
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
