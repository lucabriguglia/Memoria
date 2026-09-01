using System.Diagnostics;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing;

/// <summary>
/// Records what a projection snapshot write folded, on the current <see cref="Activity"/>.
/// </summary>
/// <remarks>
/// <para>
/// The read-model counterpart of <see cref="AggregateDiagnostics"/>, and it exists for the same
/// reason: a projection in a state nobody expects raises exactly the question an aggregate does —
/// which events produced it? — and the answer is only true if it is recorded at the moment of the
/// fold. Reconstructing it later cannot work, because only today's <c>EventTypeFilter</c> is
/// available and a snapshot written under an earlier <see cref="ProjectionType"/> version was not
/// built with it.
/// </para>
/// <para>
/// A separate event name from <see cref="AggregateDiagnostics.AggregateFoldedEventName"/> rather
/// than a shared one: the two carry different identifiers, and calling a projection fold
/// "Aggregate Folded" would make that name a lie about half its occurrences. The tag shapes are
/// otherwise identical, so a query over both is a two-name filter and nothing more.
/// </para>
/// <para>
/// Every value is a bounded scalar, and nothing is allocated when nothing is listening.
/// </para>
/// </remarks>
public static class ProjectionDiagnostics
{
    /// <summary>
    /// The name of the activity event recording a projection fold. Store-neutral: every store emits
    /// this same event.
    /// </summary>
    public const string ProjectionFoldedEventName = "Projection Folded";

    /// <summary>
    /// Records a fold on the current activity, if anything is listening.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="streamId">The stream the events were read from.</param>
    /// <param name="projectionId">The projection the events were folded into.</param>
    /// <param name="appliedFromSequence">Sequence of the first event folded.</param>
    /// <param name="appliedToSequence">Sequence of the last event folded.</param>
    /// <param name="appliedCount">How many events were folded.</param>
    /// <param name="versionBefore">The projection's version before the fold.</param>
    /// <param name="versionAfter">The projection's version after the fold.</param>
    public static void AddProjectionFoldedEvent<T>(IStreamId streamId, IProjectionId<T> projectionId,
        int appliedFromSequence, int appliedToSequence, int appliedCount, int versionBefore, int versionAfter)
        where T : IProjection
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.AddEvent(new ActivityEvent(ProjectionFoldedEventName, timestamp: default,
            tags: new ActivityTagsCollection
            {
                { "streamId", streamId.Id },
                { "projectionId", projectionId.ToStoreId() },
                { "appliedFromSequence", appliedFromSequence },
                { "appliedToSequence", appliedToSequence },
                { "appliedCount", appliedCount },
                { "versionBefore", versionBefore },
                { "versionAfter", versionAfter }
            }));
    }
}
