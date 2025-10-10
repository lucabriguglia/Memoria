using Memoria.EventSourcing.Domain;

namespace OpenCqrs.EventSourcing.Store.Tests.Models.Streams;

public class TestStreamId(string id) : IStreamId
{
    public string Id => $"test:{id}";
}
