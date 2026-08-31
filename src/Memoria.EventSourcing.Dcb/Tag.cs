namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// A label attached to an event, naming one thing the event concerns — a course, a student, an
/// order. Tags are what a <see cref="TagQuery"/> matches on, and therefore what a consistency
/// boundary is drawn around.
/// </summary>
/// <remarks>
/// A tag renders as <c>{Key}:{Value}</c>. The key may not contain a colon, so the rendered form
/// always parses back to the pair it came from; the value may. Comparison is ordinal and
/// case-sensitive, matching how every other identifier in Memoria compares in .NET — a store whose
/// column collation is case-insensitive must specify a case-sensitive one rather than expecting
/// this type to loosen.
/// </remarks>
/// <example>
/// <code>
/// var course = new Tag("course", "c1");
/// var student = new Tag("student", "s7");
/// aggregate.Add(new SeatReservedEvent(...), course, student);
/// </code>
/// </example>
public readonly record struct Tag
{
    /// <summary>
    /// The character separating a tag's key from its value in its rendered form.
    /// </summary>
    public const char Separator = ':';

    /// <summary>
    /// Initialises a tag from a key and a value.
    /// </summary>
    /// <param name="key">The kind of thing being named. May not be empty or contain a colon.</param>
    /// <param name="value">The identity of that thing. May not be empty; may contain colons.</param>
    /// <exception cref="ArgumentException">The key or value is empty, or the key contains a colon.</exception>
    public Tag(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A tag key cannot be null or whitespace.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A tag value cannot be null or whitespace.", nameof(value));
        }

        if (key.Contains(Separator))
        {
            throw new ArgumentException(
                $"A tag key cannot contain '{Separator}' because it separates the key from the value. Key was '{key}'.",
                nameof(key));
        }

        Key = key;
        Value = value;
    }

    /// <summary>
    /// Gets the kind of thing this tag names, such as <c>course</c>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the identity of the thing this tag names, such as <c>c1</c>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Parses a tag from its rendered <c>{Key}:{Value}</c> form.
    /// </summary>
    /// <param name="tag">The rendered tag.</param>
    /// <returns>The parsed tag.</returns>
    /// <exception cref="ArgumentException">The string is not a rendered tag.</exception>
    public static Tag Parse(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("A tag cannot be parsed from a null or whitespace string.", nameof(tag));
        }

        var separator = tag.IndexOf(Separator);
        if (separator <= 0 || separator == tag.Length - 1)
        {
            throw new ArgumentException(
                $"'{tag}' is not a tag. The expected form is 'key{Separator}value'.", nameof(tag));
        }

        return new Tag(tag[..separator], tag[(separator + 1)..]);
    }

    /// <summary>
    /// Renders the tag as <c>{Key}:{Value}</c>.
    /// </summary>
    /// <returns>The rendered tag.</returns>
    public override string ToString() => $"{Key}{Separator}{Value}";
}
