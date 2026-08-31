namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The consistency boundary of a decision, expressed as a query over event tags. A query selects
/// every event carrying at least one of its tags — the events a model folds, and the events an
/// append condition watches for.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a boundary <em>dynamic</em>: it is chosen per decision and evaluated at
/// append time, rather than fixed at design time the way a stream is.
/// </para>
/// <para>
/// 1.8.0 supports disjunction only — <see cref="AnyOf(Tag[])"/>. Conjunction ("events carrying
/// <em>both</em> <c>course:c1</c> and <c>student:s7</c>") is deliberately out of scope: it needs a
/// different query shape in the store and changes which tag heads an append must lock. The type is
/// shaped so it can be added without a breaking change.
/// </para>
/// <para>
/// Equality and <see cref="ToString"/> are order-independent, so two spellings of the same boundary
/// are one boundary — which is what lets a snapshot be keyed on the query that produced it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var boundary = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));
/// var state = await dcb.GetInMemoryProjection(boundary, projectionId);
/// </code>
/// </example>
public sealed class TagQuery : IEquatable<TagQuery>
{
    private readonly HashSet<Tag> _tags;
    private readonly string _canonical;

    private TagQuery(HashSet<Tag> tags)
    {
        _tags = tags;
        _canonical = string.Join(",", tags.Select(tag => tag.ToString()).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Creates a query matching any event that carries at least one of the given tags.
    /// </summary>
    /// <param name="tags">The tags forming the boundary. Duplicates collapse.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">No tags were supplied.</exception>
    public static TagQuery AnyOf(params Tag[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Length == 0)
        {
            throw new ArgumentException(
                "A tag query needs at least one tag. To append without a condition, supply no condition rather than an empty query.",
                nameof(tags));
        }

        return new TagQuery([..tags]);
    }

    /// <summary>
    /// Creates a query matching any event that carries at least one of the given tags.
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
    /// Gets the tags forming this boundary.
    /// </summary>
    public IReadOnlyCollection<Tag> Tags => _tags;

    /// <summary>
    /// Determines whether an event carrying the given tags falls inside this boundary.
    /// </summary>
    /// <param name="tags">The tags carried by the event.</param>
    /// <returns><c>true</c> if the event carries at least one of this query's tags.</returns>
    public bool Matches(IEnumerable<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return tags.Any(_tags.Contains);
    }

    /// <inheritdoc />
    public bool Equals(TagQuery? other) =>
        other is not null && (ReferenceEquals(this, other) || _tags.SetEquals(other._tags));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as TagQuery);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_canonical);

    /// <summary>
    /// Renders the query as its tags in a stable order, so the result is usable as a persisted key.
    /// </summary>
    /// <returns>The canonical form.</returns>
    public override string ToString() => _canonical;

    /// <summary>
    /// Determines whether two queries describe the same boundary.
    /// </summary>
    public static bool operator ==(TagQuery? left, TagQuery? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Determines whether two queries describe different boundaries.
    /// </summary>
    public static bool operator !=(TagQuery? left, TagQuery? right) => !(left == right);
}
