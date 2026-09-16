using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Events;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

/// <summary>
/// The process-wide bindings are one <see cref="TypeBindingSet"/>, <see cref="TypeBindingSet.Default"/>,
/// and the static properties on <see cref="TypeBindings"/> are views over it — so an application
/// that never mentions the set is bound exactly as before, and a store given a set of its own has
/// maps and an inverted lookup that the process-wide ones never touch.
/// </summary>
[Collection(TypeBindingsCollection.Name)]
public class TypeBindingSetTests : IDisposable
{
    private readonly Dictionary<string, Type> _originalEventTypeBindings = TypeBindings.EventTypeBindings;

    public void Dispose() => TypeBindings.EventTypeBindings = _originalEventTypeBindings;

    [Fact]
    public void GivenTheStaticIsAssigned_WhenTheDefaultSetIsRead_ThenItHoldsTheSameDictionary()
    {
        var bindings = new Dictionary<string, Type> { { "ItemCreated:1", typeof(ItemCreatedEvent) } };

        TypeBindings.EventTypeBindings = bindings;

        TypeBindingSet.Default.EventTypeBindings.Should().BeSameAs(bindings);
    }

    [Fact]
    public void GivenTheDefaultSetIsAssigned_WhenTheStaticIsRead_ThenItHoldsTheSameDictionary()
    {
        var bindings = new Dictionary<string, Type> { { "ItemCreated:1", typeof(ItemCreatedEvent) } };

        TypeBindingSet.Default.EventTypeBindings = bindings;

        TypeBindings.EventTypeBindings.Should().BeSameAs(bindings);
    }

    [Fact]
    public void GivenTwoSets_WhenEachIsInverted_ThenNeitherSeesTheOthersKeys()
    {
        var first = new TypeBindingSet
        {
            EventTypeBindings = new Dictionary<string, Type> { { "ItemCreated:1", typeof(ItemCreatedEvent) } }
        };
        var second = new TypeBindingSet
        {
            EventTypeBindings = new Dictionary<string, Type> { { "ItemCreated:7", typeof(ItemCreatedEvent) } }
        };

        using (new AssertionScope())
        {
            first.GetEventBindingKeysByType().GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");
            second.GetEventBindingKeysByType().GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:7");
        }
    }

    [Fact]
    public void GivenASetOfItsOwn_WhenTheStaticIsReassigned_ThenTheSetIsUntouched()
    {
        var own = new TypeBindingSet
        {
            EventTypeBindings = new Dictionary<string, Type> { { "ItemCreated:1", typeof(ItemCreatedEvent) } }
        };

        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { { "ItemRenamed:1", typeof(ItemRenamedEvent) } };

        using (new AssertionScope())
        {
            own.EventTypeBindings.Should().ContainKey("ItemCreated:1").And.NotContainKey("ItemRenamed:1");
            own.GetEventBindingKeysByType().GetValueOrDefault(typeof(ItemRenamedEvent)).Should().BeNull();
        }
    }

    [Fact]
    public void GivenASetsBindingsAreReplaced_WhenInvertedAgain_ThenTheNewBindingsAreUsed()
    {
        var set = new TypeBindingSet
        {
            EventTypeBindings = new Dictionary<string, Type> { { "ItemCreated:1", typeof(ItemCreatedEvent) } }
        };
        set.GetEventBindingKeysByType().GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");

        set.EventTypeBindings = new Dictionary<string, Type> { { "ItemCreated:7", typeof(ItemCreatedEvent) } };

        set.GetEventBindingKeysByType().GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:7");
    }
}
