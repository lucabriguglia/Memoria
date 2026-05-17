namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// A single conjunctive predicate: an event matches iff its type is in <paramref name="Types"/>
/// (or <paramref name="Types"/> is empty) AND its tags contain every tag in <paramref name="Tags"/>.
/// </summary>
public sealed record QueryItem(IReadOnlyList<Type> Types, TagSet Tags)
{
    public QueryItem(Type[] types, TagSet tags) : this((IReadOnlyList<Type>)types, tags) { }

    public bool Matches(Type eventType, TagSet eventTags)
    {
        if (Types.Count > 0 && !Types.Contains(eventType)) return false;
        return eventTags.Contains(Tags);
    }
}
