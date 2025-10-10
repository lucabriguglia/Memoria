using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.Streams;

public class CustomerStreamId(Guid id) : IStreamId
{
    public string Id => $"customer:{id}";
}
