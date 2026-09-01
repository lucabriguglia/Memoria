using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Projections;

/// <summary>
/// A projection identifier that narrows to its own events inside a shared stream, the way
/// <see cref="Aggregates.TestAggregateIdWithPropertyFilter"/> does for a write model.
/// </summary>
public class TestProjectionIdWithPropertyFilter(string projectionId, string name)
    : IProjectionId<TestProjection>
{
    public string Id => $"test-projection-with-filter:{projectionId}:{name}";

    public IDictionary<string, string>? EventPropertyFilter { get; } =
        new Dictionary<string, string> { { "Name", name } };
}
