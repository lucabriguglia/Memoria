using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Projections;

public class TestProjectionId(string id) : IProjectionId<TestProjection>
{
    public string Id => id;
}
