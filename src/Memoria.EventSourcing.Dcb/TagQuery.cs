namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The consistency boundary of a decision, expressed as a query over event tags — the events a
/// model folds, and the events an append condition watches for.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a boundary <em>dynamic</em>: it is chosen per decision and evaluated at
/// append time, rather than fixed at design time the way a stream is.
/// </para>
/// <para>
/// A boundary comes in two shapes. <see cref="AnyOf(Tag[])"/> is a <em>union</em> — every event
/// carrying at least one of its tags — and is what a rule spanning several things needs, such as a
/// course's capacity <em>and</em> a student's course count. <see cref="AllOf(Tag[])"/> is an
/// <em>intersection</em> — only the events carrying every one of its tags — and is what a fact about
/// a combination needs, such as whether this student is already on this course.
/// </para>
/// <para>
/// Internally both are the same thing: a set of tag groups, matched as any group whose tags are all
/// present. A union is one group per tag; an intersection is one group holding them all. Keeping the
/// structure rather than a flag is what allows a query mixing the two to be added later without
/// changing the type again.
/// </para>
/// <para>
/// Equality and <see cref="ToString"/> are order-independent, so two spellings of the same boundary
/// are one boundary — which is what lets a snapshot be keyed on the query that produced it. A union
/// and an intersection over the same tags are <em>not</em> the same boundary, because they select
/// different events, and they render differently so that a snapshot of one is never read back as the
/// other.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Everything about the course, plus everything about the student.
/// var union = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));
/// var state = await dcb.GetInMemoryProjection(union, projectionId);
///
/// // Only the events concerning both — this student's dealings with this course.
/// var intersection = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));
/// </code>
/// </example>
public sealed class TagQuery : IEquatable<TagQuery>
{
    /// <summary>
    /// The character joining the tags within one group in the canonical form. Read as "and".
    /// </summary>
    private const char TagSeparator = '&';

    /// <summary>
    /// The character joining the groups in the canonical form. Read as "or".
    /// </summary>
    private const char GroupSeparator = ',';

    /// <summary>
    /// The character escaping a separator that appears inside a tag.
    /// </summary>
    private const char EscapeCharacter = '\\';

    private readonly HashSet<Tag>[] _groups;
    private readonly string _canonical;

    private TagQuery(IEnumerable<HashSet<Tag>> groups)
    {
        // Rendered first, then deduplicated and ordered by that rendering, so a group's identity and
        // its place in the canonical form are decided by the same thing.
        var rendered = groups
            .Select(group => (Group: group, Canonical: Render(group)))
            .DistinctBy(group => group.Canonical, StringComparer.Ordinal)
            .OrderBy(group => group.Canonical, StringComparer.Ordinal)
            .ToArray();

        _groups = rendered.Select(group => group.Group).ToArray();
        _canonical = string.Join(GroupSeparator, rendered.Select(group => group.Canonical));

        Tags = rendered.SelectMany(group => group.Group).ToHashSet();
    }

    /// <summary>
    /// Creates a union boundary: every event carrying at least one of the given tags.
    /// </summary>
    /// <param name="tags">The tags forming the boundary. Duplicates collapse.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">No tags were supplied.</exception>
    public static TagQuery AnyOf(params Tag[] tags)
    {
        Validate(tags);

        return new TagQuery(tags.Select(tag => new HashSet<Tag> { tag }));
    }

    /// <summary>
    /// Creates a union boundary: every event carrying at least one of the given tags.
    /// </summary>
    /// <param name="tags">The tags forming the boundary. Duplicates collapse.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">No tags were supplied.</exception>
    public static TagQuery AnyOf(IEnumerable<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return AnyOf(tags.ToArray());
    }

    /// <summary>
    /// Creates an intersection boundary: only the events carrying every one of the given tags.
    /// </summary>
    /// <param name="tags">The tags forming the boundary. Duplicates collapse.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">No tags were supplied.</exception>
    /// <remarks>
    /// An intersection narrows what a decision <em>reads</em>. It does not narrow what the decision
    /// may condition on: an append must be conditioned on the boundary it was folded from, or a
    /// wider one. Conditioning on an intersection a decision did not fold would accept an append
    /// resting on facts that have since moved.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Has this student already subscribed to this course? One event answers it, and this is the
    /// // boundary that reads only that event rather than the whole course and the whole student.
    /// var boundary = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));
    /// </code>
    /// </example>
    public static TagQuery AllOf(params Tag[] tags)
    {
        Validate(tags);

        return new TagQuery([[..tags]]);
    }

    /// <summary>
    /// Creates an intersection boundary: only the events carrying every one of the given tags.
    /// </summary>
    /// <param name="tags">The tags forming the boundary. Duplicates collapse.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">No tags were supplied.</exception>
    public static TagQuery AllOf(IEnumerable<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return AllOf(tags.ToArray());
    }

    /// <summary>
    /// Gets every tag this boundary names, however those tags combine.
    /// </summary>
    /// <remarks>
    /// The flat union, not the structure. It is what an append contends on — a conditioned append
    /// claims a head row for every tag its boundary names — and what an aggregate's events are
    /// appended under when <c>Add</c> is given no tags of its own.
    /// </remarks>
    public IReadOnlyCollection<Tag> Tags { get; }

    /// <summary>
    /// Gets the groups the tags combine in: an event is inside the boundary when it carries every
    /// tag of at least one group.
    /// </summary>
    /// <remarks>
    /// A union is one group per tag; an intersection is a single group. A store needs this rather
    /// than <see cref="Tags"/>, because the tags alone do not say how they combine.
    /// </remarks>
    public IReadOnlyCollection<IReadOnlyCollection<Tag>> TagGroups => _groups;

    /// <summary>
    /// Determines whether an event carrying the given tags falls inside this boundary.
    /// </summary>
    /// <param name="tags">The tags carried by the event.</param>
    /// <returns><c>true</c> if the event carries every tag of at least one group.</returns>
    /// <remarks>
    /// An event may carry more tags than the boundary asks for and still be inside it. A boundary
    /// asks that its tags are among the event's, not that they are all of them.
    /// </remarks>
    public bool Matches(IEnumerable<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        // Materialised once: this runs per event wherever a boundary is applied in memory.
        var carried = tags as IReadOnlySet<Tag> ?? tags.ToHashSet();

        return _groups.Any(group => group.All(carried.Contains));
    }

    /// <inheritdoc />
    public bool Equals(TagQuery? other) =>
        other is not null && (ReferenceEquals(this, other) ||
                              string.Equals(_canonical, other._canonical, StringComparison.Ordinal));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as TagQuery);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_canonical);

    /// <summary>
    /// Renders the query in a stable order, so the result is usable as a persisted key.
    /// </summary>
    /// <returns>The canonical form.</returns>
    /// <remarks>
    /// Tags within a group are joined by <c>&amp;</c> and groups by <c>,</c>, both ordered ordinally,
    /// so a union over two tags renders <c>course:c1,student:s7</c> and an intersection over the same
    /// two renders <c>course:c1&amp;student:s7</c>. A tag whose value contains a separator has it
    /// escaped, so two different boundaries can never render the same string — which matters because
    /// this is what keys a snapshot. The form is not meant to be parsed back.
    /// </remarks>
    public override string ToString() => _canonical;

    /// <summary>
    /// Determines whether two queries describe the same boundary.
    /// </summary>
    public static bool operator ==(TagQuery? left, TagQuery? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>
    /// Determines whether two queries describe different boundaries.
    /// </summary>
    public static bool operator !=(TagQuery? left, TagQuery? right) => !(left == right);

    private static void Validate(Tag[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Length == 0)
        {
            throw new ArgumentException(
                "A tag query needs at least one tag. To append without a condition, supply no condition rather than an empty query.",
                nameof(tags));
        }
    }

    private static string Render(HashSet<Tag> group) =>
        string.Join(TagSeparator, group.Select(tag => Escape(tag.ToString())).Order(StringComparer.Ordinal));

    private static string Escape(string tag) => tag
        .Replace($"{EscapeCharacter}", $"{EscapeCharacter}{EscapeCharacter}", StringComparison.Ordinal)
        .Replace($"{TagSeparator}", $"{EscapeCharacter}{TagSeparator}", StringComparison.Ordinal)
        .Replace($"{GroupSeparator}", $"{EscapeCharacter}{GroupSeparator}", StringComparison.Ordinal);
}
