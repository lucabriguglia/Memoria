namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The optimistic concurrency check for an append: "nothing matching <see cref="Query"/> has been
/// appended since <see cref="AfterPosition"/>".
/// </summary>
/// <remarks>
/// This is what makes a consistency boundary dynamic. A streamed append asserts against one
/// stream's latest sequence, fixed when the aggregate was designed; a DCB append asserts against
/// whatever the decision actually read, so two commands contend only when their boundaries overlap.
/// <para>
/// Appending with no condition at all is expressed by passing no condition, not by an empty one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var boundary = TagQuery.AnyOf(new Tag("seat", "a1"), new Tag("student", "s7"));
/// var position = (await dcb.GetLatestPosition(boundary)).Value;
/// // ... decide ...
/// await dcb.SaveEvents(events, new AppendCondition(boundary, position));
/// </code>
/// </example>
public sealed record AppendCondition
{
    /// <summary>
    /// The position meaning "nothing has ever been appended inside this boundary". Positions start
    /// at 1, so 0 is below every real one.
    /// </summary>
    public const long NoEvents = 0;

    /// <summary>
    /// Initialises an append condition.
    /// </summary>
    /// <param name="query">The consistency boundary being asserted over.</param>
    /// <param name="afterPosition">The latest position the decision read inside that boundary.</param>
    /// <exception cref="ArgumentNullException">The query is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The position is negative.</exception>
    public AppendCondition(TagQuery query, long afterPosition)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(afterPosition);

        Query = query;
        AfterPosition = afterPosition;
    }

    /// <summary>
    /// Gets the consistency boundary being asserted over.
    /// </summary>
    public TagQuery Query { get; }

    /// <summary>
    /// Gets the latest position the decision read inside that boundary. The append fails if the
    /// boundary has moved past it.
    /// </summary>
    public long AfterPosition { get; }

    /// <summary>
    /// Creates a condition asserting that nothing has ever been appended inside a boundary — the
    /// check for a decision that may only happen once.
    /// </summary>
    /// <param name="query">The consistency boundary.</param>
    /// <returns>The condition.</returns>
    public static AppendCondition NothingAppendedFor(TagQuery query) => new(query, NoEvents);
}
