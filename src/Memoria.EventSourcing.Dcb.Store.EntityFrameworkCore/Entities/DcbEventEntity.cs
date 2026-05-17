namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

public class DcbEventEntity
{
    public string EventId { get; set; } = null!;
    public long GlobalPosition { get; set; }
    public string EventType { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }

    public List<DcbEventTagEntity> Tags { get; set; } = [];
}
