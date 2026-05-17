using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// An event the caller wants to append. The store assigns identity and position.
/// </summary>
public sealed record EventToAppend(IEvent Payload, TagSet Tags)
{
    public static EventToAppend Of(IEvent payload, params Tag[] tags) =>
        new(payload, TagSet.Of(tags));
}
