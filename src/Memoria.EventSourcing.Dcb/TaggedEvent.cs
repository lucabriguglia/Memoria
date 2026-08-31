using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// A domain event together with the tags it is appended under. The tags are what a
/// <see cref="TagQuery"/> later matches on, so they decide which decisions this event participates
/// in the consistency of.
/// </summary>
/// <param name="Event">The domain event.</param>
/// <param name="Tags">The tags the event is appended under. Never empty.</param>
public sealed record TaggedEvent(IEvent Event, IReadOnlyCollection<Tag> Tags)
{
    /// <summary>
    /// The domain event.
    /// </summary>
    public IEvent Event { get; } = Event ?? throw new ArgumentNullException(nameof(Event));

    /// <summary>
    /// The tags the event is appended under.
    /// </summary>
    public IReadOnlyCollection<Tag> Tags { get; } = Validate(Tags);

    private static IReadOnlyCollection<Tag> Validate(IReadOnlyCollection<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            throw new ArgumentException(
                "An event must be appended under at least one tag, otherwise no query can ever reach it.",
                nameof(tags));
        }

        return tags;
    }
}
