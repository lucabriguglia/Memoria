namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// An order-independent set of <see cref="Tag"/>s with unique keys.
/// </summary>
public sealed class TagSet : IEquatable<TagSet>
{
    private readonly SortedDictionary<string, string> _tags;

    public static TagSet Empty { get; } = new(new SortedDictionary<string, string>(StringComparer.Ordinal));

    private TagSet(SortedDictionary<string, string> tags)
    {
        _tags = tags;
    }

    public static TagSet Of(params Tag[] tags)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (sorted.ContainsKey(tag.Key))
            {
                throw new ArgumentException($"TagSet contains duplicate key '{tag.Key}'.", nameof(tags));
            }
            sorted.Add(tag.Key, tag.Value);
        }
        return new TagSet(sorted);
    }

    public IReadOnlyCollection<Tag> Tags =>
        _tags.Select(kvp => new Tag(kvp.Key, kvp.Value)).ToList();

    /// <summary>
    /// Returns true iff every tag in <paramref name="required"/> is present in this set with the same value.
    /// </summary>
    public bool Contains(TagSet required)
    {
        foreach (var kvp in required._tags)
        {
            if (!_tags.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public bool Equals(TagSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_tags.Count != other._tags.Count) return false;
        foreach (var kvp in _tags)
        {
            if (!other._tags.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is TagSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _tags)
        {
            hash.Add(kvp.Key, StringComparer.Ordinal);
            hash.Add(kvp.Value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}
