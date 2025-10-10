using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Streams;

public class TestStreamId(string id) : IStreamId
{
    public string Id => $"test:{id}";
}
