using System.Diagnostics;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing;

/// <summary>
/// Records what a snapshot write folded, on the current <see cref="Activity"/>.
/// </summary>
/// <remarks>
/// <para>
/// This exists to answer one production question: an aggregate is in a state nobody expects — which
/// events produced it? The record is written at the moment of the fold, so it stays true no matter
/// how the aggregate's type or its event filter change afterwards. Reconstructing the same answer
/// later cannot offer that: only today's <c>EventTypeFilter</c> is available, and a snapshot written
/// under an earlier <see cref="AggregateType"/> version was not built with it.
/// </para>
/// <para>
/// Every value is a bounded scalar. A fold over a thousand events records exactly what a fold over
/// two does, so tracing cost does not scale with stream length. When nothing is listening, nothing
/// is allocated — the tags, and the aggregate's store id, are built only after the null check.
/// </para>
/// <para>
/// <c>appliedCount</c> counts events the fold consumed; <c>versionAfter - versionBefore</c> counts
/// those that actually changed the aggregate. The two differ when an event matches the aggregate's
/// <c>EventTypeFilter</c> but its <c>Apply</c> ignores it, and that difference is usually the
/// interesting part of the answer.
/// </para>
/// </remarks>
public static class AggregateDiagnostics
{
    /// <summary>
    /// The name of the activity event recording a fold. Store-neutral: every store emits this same
    /// event, alongside whatever transport-level events it emits of its own.
    /// </summary>
    public const string AggregateFoldedEventName = "Aggregate Folded";

    /// <summary>
    /// Records a fold on the current activity, if anything is listening.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="streamId">The stream the events were read from.</param>
    /// <param name="aggregateId">The aggregate the events were folded into.</param>
    /// <param name="appliedFromSequence">Sequence of the first event folded.</param>
    /// <param name="appliedToSequence">Sequence of the last event folded.</param>
    /// <param name="appliedCount">How many events were folded.</param>
    /// <param name="versionBefore">The aggregate's version before the fold.</param>
    /// <param name="versionAfter">The aggregate's version after the fold.</param>
    public static void AddAggregateFoldedEvent<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        int appliedFromSequence, int appliedToSequence, int appliedCount, int versionBefore, int versionAfter)
        where T : IAggregateRoot
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.AddEvent(new ActivityEvent(AggregateFoldedEventName, timestamp: default,
            tags: new ActivityTagsCollection
            {
                { "streamId", streamId.Id },
                { "aggregateId", aggregateId.ToStoreId() },
                { "appliedFromSequence", appliedFromSequence },
                { "appliedToSequence", appliedToSequence },
                { "appliedCount", appliedCount },
                { "versionBefore", versionBefore },
                { "versionAfter", versionAfter }
            }));
    }
}
