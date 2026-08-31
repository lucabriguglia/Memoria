using Xunit;

namespace Memoria.EventSourcing.Tests;

/// <summary>
/// Groups the test classes that mutate <see cref="Memoria.EventSourcing.Domain.TypeBindings"/>.
/// </summary>
/// <remarks>
/// The binding maps are process-wide static state. xUnit runs test classes in parallel across
/// collections, so two classes each saving, replacing and restoring those maps will interleave and
/// fail unpredictably. Naming one collection serialises them; it is not a hint that either class is
/// flaky on its own.
/// </remarks>
[CollectionDefinition(Name)]
public class TypeBindingsCollection
{
    public const string Name = "TypeBindings";
}
