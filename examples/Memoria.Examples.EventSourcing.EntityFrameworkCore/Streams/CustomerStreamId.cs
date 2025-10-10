using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.Streams;

public class CustomerStreamId(Guid id) : IStreamId
{
    public string Id => $"customer:{id}";
}
