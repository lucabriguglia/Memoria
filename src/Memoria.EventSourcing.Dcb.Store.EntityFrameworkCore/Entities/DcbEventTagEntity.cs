namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

public class DcbEventTagEntity
{
    public string EventId { get; set; } = null!;
    public string TagKey { get; set; } = null!;
    public string TagValue { get; set; } = null!;
    public long GlobalPosition { get; set; }

    public DcbEventEntity Event { get; set; } = null!;
}
