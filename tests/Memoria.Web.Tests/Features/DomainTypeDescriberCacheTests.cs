using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What is worked out about a type is worked out once. Describing a model builds an instance of it
/// to read its event filter, and reads its attributes and properties through reflection; none of
/// that changes until the assemblies are read again, yet every page was doing it afresh on every
/// request. So it is remembered, and forgotten when the types are reloaded — as the shapes of the
/// identifiers already are.
/// </summary>
/// <remarks>
/// In the bindings collection because a reload forgets what is remembered, and the registry tests
/// reload.
/// </remarks>
[Collection(nameof(TypeBindingsCollection))]
public class DomainTypeDescriberCacheTests
{
    private static DomainTypeDescription Describe() =>
        DomainTypeDescriber.Describe(typeof(SampleBuiltCountingAggregate), [typeof(SampleDcbAggregateId)]);

    [Fact]
    public void Builds_a_model_once_to_read_its_event_types_however_often_it_is_described()
    {
        DomainTypeDescriber.Forget();
        SampleBuiltCountingAggregate.Built = 0;

        Describe();
        Describe();
        Describe();

        SampleBuiltCountingAggregate.Built.Should().Be(1);
    }

    [Fact]
    public void Hands_back_the_same_shape_of_a_type_until_told_to_forget()
    {
        DomainTypeDescriber.Forget();

        var first = DomainTypeDescriber.PropertiesOf(typeof(SampleBuiltCountingAggregate));
        var again = DomainTypeDescriber.PropertiesOf(typeof(SampleBuiltCountingAggregate));

        DomainTypeDescriber.Forget();

        var afterwards = DomainTypeDescriber.PropertiesOf(typeof(SampleBuiltCountingAggregate));

        using var scope = new AssertionScope();

        again.Should().BeSameAs(first);
        afterwards.Should().NotBeSameAs(first).And.BeEquivalentTo(first);
    }

    [Fact]
    public void Builds_the_model_again_once_told_to_forget()
    {
        DomainTypeDescriber.Forget();
        SampleBuiltCountingAggregate.Built = 0;

        Describe();
        DomainTypeDescriber.Forget();
        Describe();

        SampleBuiltCountingAggregate.Built.Should().Be(2);
    }

    /// <summary>
    /// A reload is when the types may have changed, so it is when what was worked out about them
    /// goes: the model is built again for the first description after it.
    /// </summary>
    [Fact]
    public void Forgets_on_a_reload_of_the_types()
    {
        var registry = new DomainTypeRegistry(
            new ExtensionStore(Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}")),
            typeof(SampleBuiltCountingAggregate).Assembly);

        Describe();
        registry.Reload();
        var builtByTheReload = SampleBuiltCountingAggregate.Built;

        Describe();

        SampleBuiltCountingAggregate.Built.Should().BeGreaterThan(builtByTheReload,
            "the description made before the reload is not the one handed out after it");
    }
}

/// <summary>A model that counts how often it is built, so a description's cost can be read off it.</summary>
public class SampleBuiltCountingAggregate : DcbAggregateRoot
{
    public static int Built { get; set; }

    public SampleBuiltCountingAggregate() => Built++;

    public string? Name { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event) => false;
}
