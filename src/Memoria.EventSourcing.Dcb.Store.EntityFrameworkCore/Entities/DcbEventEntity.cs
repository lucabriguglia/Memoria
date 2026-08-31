using Memoria.EventSourcing.Domain;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// One appended event. Unlike the streamed store's event row it belongs to no stream and carries no
/// per-stream sequence: it is ordered by a single <see cref="Position"/> global to the whole log, and
/// reached through the tags in <see cref="Tags"/>.
/// </summary>
public class DcbEventEntity : IAuditableEntity
{
    /// <summary>
    /// Gets or sets the global position, assigned by the database on append.
    /// </summary>
    /// <remarks>
    /// Monotonic but not gap-free: concurrent transactions take positions in one order and commit in
    /// another, so a reader can briefly see a later position without an earlier one. That is safe for
    /// the append condition, which is only ever evaluated inside the transaction holding the relevant
    /// tag-head locks, and unsafe for a catch-up subscription — which is why this store does not
    /// offer one.
    /// </remarks>
    public long Position { get; set; }

    /// <summary>
    /// Gets or sets the event type binding key, in <c>{name}:{version}</c> form.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the serialised event payload.
    /// </summary>
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date the event was appended.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the user that appended the event.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the tags this event was appended under.
    /// </summary>
    public ICollection<DcbEventTagEntity> Tags { get; set; } = new List<DcbEventTagEntity>();
}

/// <summary>
/// Extension methods for <see cref="DcbEventEntity"/>.
/// </summary>
public static class DcbEventEntityExtensions
{
    /// <summary>
    /// Deserialises the stored payload back into its domain event.
    /// </summary>
    /// <param name="eventEntity">The stored event.</param>
    /// <returns>The domain event.</returns>
    /// <exception cref="InvalidOperationException">The event type is not registered.</exception>
    public static IEvent ToDomainEvent(this DcbEventEntity eventEntity)
    {
        var typeFound = TypeBindings.EventTypeBindings.TryGetValue(eventEntity.EventType, out var eventType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Event type {eventEntity.EventType} not found in TypeBindings");
        }

        return (IEvent)DomainSerializer.Current.Deserialize(eventEntity.Data, eventType!);
    }
}
