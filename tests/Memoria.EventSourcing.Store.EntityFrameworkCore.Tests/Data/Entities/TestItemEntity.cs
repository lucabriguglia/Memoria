namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Data.Entities;

public class TestItemEntity
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
