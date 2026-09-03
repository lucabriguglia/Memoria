using System.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Records DCB store diagnostics on the current <see cref="Activity"/>.
/// </summary>
public static class DcbDiagnostics
{
    /// <summary>
    /// The activity event emitted when an append is refused because its boundary moved.
    /// </summary>
    private const string ConcurrencyConflictEventName = "Concurrency Conflict";

    /// <summary>
    /// Records that an append was refused because its boundary moved.
    /// </summary>
    /// <param name="query">The boundary asserted over.</param>
    /// <param name="expectedPosition">The position the caller read.</param>
    /// <param name="latestPosition">The position the boundary is actually at.</param>
    public static void AddConcurrencyConflictEvent(TagQuery query, long expectedPosition, long latestPosition) =>
        Activity.Current?.AddEvent(new ActivityEvent(ConcurrencyConflictEventName, timestamp: default,
            tags: new ActivityTagsCollection
            {
                { "tagQuery", query.ToString() },
                { "expectedPosition", expectedPosition },
                { "latestPosition", latestPosition }
            }));

    /// <summary>
    /// Records what a snapshot write folded, on the current activity.
    /// </summary>
    /// <remarks>
    /// Emitted under <see cref="AggregateDiagnostics.AggregateFoldedEventName"/> — the same name the
    /// streamed stores use, and the constant itself rather than a copy of the string, so an operator
    /// querying for folds gets both consistency models and the two cannot drift apart. The tags
    /// differ where the concepts do: a boundary instead of a stream, and positions instead of
    /// sequences.
    /// <para>
    /// <c>appliedCount</c> counts events the fold consumed; <c>versionAfter - versionBefore</c>
    /// counts those that changed the model. The gap is events the type filter admitted and
    /// <c>Apply</c> ignored, which is usually the interesting part.
    /// </para>
    /// </remarks>
    /// <param name="query">The boundary the events were read from.</param>
    /// <param name="storeId">The aggregate the events were folded into.</param>
    /// <param name="appliedFromPosition">Position of the first event folded.</param>
    /// <param name="appliedToPosition">Position of the last event folded.</param>
    /// <param name="appliedCount">How many events were folded.</param>
    /// <param name="versionBefore">The model's version before the fold.</param>
    /// <param name="versionAfter">The model's version after the fold.</param>
    public static void AddAggregateFoldedEvent(TagQuery query, string storeId, long appliedFromPosition,
        long appliedToPosition, int appliedCount, int versionBefore, int versionAfter) =>
        AddFoldedEvent(AggregateDiagnostics.AggregateFoldedEventName, "aggregateId", query, storeId,
            appliedFromPosition, appliedToPosition, appliedCount, versionBefore, versionAfter);

    /// <summary>
    /// Records what a projection snapshot write folded, on the current activity.
    /// </summary>
    /// <remarks>
    /// Emitted under <see cref="ProjectionDiagnostics.ProjectionFoldedEventName"/>, for the reason
    /// that name exists at all: a projection fold is not an aggregate fold, and reporting it as one
    /// would make the name wrong about half the time.
    /// </remarks>
    /// <param name="query">The boundary the events were read from.</param>
    /// <param name="storeId">The projection the events were folded into.</param>
    /// <param name="appliedFromPosition">Position of the first event folded.</param>
    /// <param name="appliedToPosition">Position of the last event folded.</param>
    /// <param name="appliedCount">How many events were folded.</param>
    /// <param name="versionBefore">The model's version before the fold.</param>
    /// <param name="versionAfter">The model's version after the fold.</param>
    public static void AddProjectionFoldedEvent(TagQuery query, string storeId, long appliedFromPosition,
        long appliedToPosition, int appliedCount, int versionBefore, int versionAfter) =>
        AddFoldedEvent(ProjectionDiagnostics.ProjectionFoldedEventName, "projectionId", query, storeId,
            appliedFromPosition, appliedToPosition, appliedCount, versionBefore, versionAfter);

    private static void AddFoldedEvent(string eventName, string idTagName, TagQuery query, string storeId,
        long appliedFromPosition, long appliedToPosition, int appliedCount, int versionBefore, int versionAfter)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.AddEvent(new ActivityEvent(eventName, timestamp: default,
            tags: new ActivityTagsCollection
            {
                { "tagQuery", query.ToString() },
                { idTagName, storeId },
                { "appliedFromPosition", appliedFromPosition },
                { "appliedToPosition", appliedToPosition },
                { "appliedCount", appliedCount },
                { "versionBefore", versionBefore },
                { "versionAfter", versionAfter }
            }));
    }

    /// <summary>
    /// Records an exception against the current activity, tagged with the operation and boundary.
    /// </summary>
    /// <remarks>
    /// Provider exception detail names tables, columns and constraints. It belongs here, on the
    /// trace, and never on the <see cref="Memoria.Results.Failure"/> a caller might map onto an HTTP
    /// response.
    /// </remarks>
    /// <param name="exception">The exception to record.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="query">The boundary involved, when there is one.</param>
    public static void AddException(Exception exception, string operation, TagQuery? query = null)
    {
        var tags = new TagList { { "operation", operation } };

        if (query is not null)
        {
            tags.Add("tagQuery", query.ToString());
        }

        Activity.Current?.AddException(exception, tags);
    }
}
