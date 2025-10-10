using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Streams;

public class CustomerStreamId(Guid id) : IStreamId
{
    public string Id => $"customer:{id}";
}
