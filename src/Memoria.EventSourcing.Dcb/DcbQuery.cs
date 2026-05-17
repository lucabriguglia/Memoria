namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Selects the slice of the event log a decision depends on.
/// Multiple <see cref="QueryItem"/>s are OR-ed; a query with no items matches nothing.
/// </summary>
public sealed record DcbQuery(IReadOnlyList<QueryItem> Items)
{
    public static DcbQuery Of(params QueryItem[] items) => new(items);

    public bool Matches(Type eventType, TagSet eventTags)
    {
        foreach (var item in Items)
        {
            if (item.Matches(eventType, eventTags)) return true;
        }
        return false;
    }
}
