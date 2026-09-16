using AwesomeAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Turning one stored event row into what the page shows, and which events a model applies. Which
/// rows are inside a boundary, and how a page of them is counted, ordered and cut, is the store's
/// own work and is covered against a real store in <see cref="SqliteBoundaryEventsTests"/>.
/// </summary>
/// <remarks>
/// A row is read through the set the store it came from was given — the service's own — so the
/// bindings here are a set of this test's, and nothing process-wide is touched.
/// </remarks>
public class BoundaryEventsTests
{
    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private static readonly TypeBindingSet Bindings = new()
    {
        EventTypeBindings = new Dictionary<string, Type>
        {
            { "SampleHappened:1", typeof(SampleHappenedEvent) }
        }
    };

    [Fact]
    public void Reads_the_event_the_payload_was_written_from()
    {
        var data = DomainSerializer.Current.Serialize(new SampleHappenedEvent("abc-1"));

        var read = BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", data, Written);

        read.Position.Should().Be(7);
        read.Type.Should().Be("SampleHappened:1");
        read.Written.Should().Be(Written);
        read.State.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Name = "Id", Value = "abc-1" });
        read.Error.Should().BeNull();
    }

    /// <summary>
    /// An event whose type was never uploaded, or was uploaded and then replaced. The row is still
    /// worth listing — position, type and date say something even when the payload cannot be read.
    /// </summary>
    [Fact]
    public void Lists_an_event_whose_type_is_not_registered()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "NeverUploaded:1", """{"Id":"abc-1"}""", Written);

        read.Position.Should().Be(7);
        read.Type.Should().Be("NeverUploaded:1");
        read.State.Should().BeEmpty();
        read.Error.Should().Contain("NeverUploaded:1");
    }

    /// <summary>
    /// The key is one string in the store and two facts on a page — the name a type is written
    /// under, and the version of that name — so a row can say each of them in its own column.
    /// </summary>
    [Fact]
    public void Reads_the_name_and_version_out_of_the_key_it_was_stored_under()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", """{"Id":"abc-1"}""", Written);

        read.Name.Should().Be("SampleHappened");
        read.Version.Should().Be("1");
    }

    /// <summary>
    /// A key is written as name:version, so a name that contains a colon of its own still leaves
    /// the version as everything after the last one.
    /// </summary>
    [Fact]
    public void Takes_the_version_from_the_last_separator_in_the_key()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "Sample:Happened:2", "{}", Written);

        read.Name.Should().Be("Sample:Happened");
        read.Version.Should().Be("2");
    }

    /// <summary>
    /// A row is listed whatever its type string turns out to be, so one that carries no version at
    /// all is a name with nothing to say beside it rather than a row that cannot be drawn.
    /// </summary>
    [Fact]
    public void Reports_no_version_for_a_key_that_carries_none()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "SampleHappened", "{}", Written);

        read.Name.Should().Be("SampleHappened");
        read.Version.Should().BeNull();
    }

    [Fact]
    public void Reports_a_payload_that_cannot_be_read_back()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", "{not json", Written);

        read.State.Should().BeEmpty();
        read.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Reports_a_payload_that_reads_back_as_nothing()
    {
        var read = BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", "null", Written);

        read.State.Should().BeEmpty();
        read.Error.Should().Contain("empty");
    }

    /// <summary>
    /// The payload as the log wrote it, kept beside the properties it was read into: the Json column
    /// shows the row's own text, and it has to survive whatever becomes of the read — a type nothing
    /// uploaded describes and a payload that will not open are exactly the rows worth looking at.
    /// </summary>
    [Fact]
    public void Keeps_the_payload_the_log_wrote()
    {
        BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", """{"Id":"abc-1"}""", Written)
            .Data.Should().Be("""{"Id":"abc-1"}""");

        BoundaryEvents.Read(Bindings, position: 7, "NeverUploaded:1", """{"Id":"abc-1"}""", Written)
            .Data.Should().Be("""{"Id":"abc-1"}""");

        BoundaryEvents.Read(Bindings, position: 7, "SampleHappened:1", "{not json", Written)
            .Data.Should().Be("{not json");
    }

    /// <summary>
    /// The filter is a model's own account of which events it applies, so it is read off the model
    /// rather than worked out from anything else.
    /// </summary>
    [Fact]
    public void Reads_the_event_types_the_aggregate_applies()
    {
        BoundaryEvents.AppliedBy(typeof(SampleDcbAggregate), loaded: null)
            .Should().BeEquivalentTo([typeof(SampleHappenedEvent)]);
    }

    /// <summary>
    /// A null filter is the model saying it applies everything, so nothing is narrowed.
    /// </summary>
    [Fact]
    public void Narrows_nothing_for_an_aggregate_that_applies_everything()
    {
        BoundaryEvents.AppliedBy(typeof(SampleAggregate), loaded: null).Should().BeNull();
    }

    /// <summary>
    /// The one already read is the real object, so it answers for itself rather than being built a
    /// second time.
    /// </summary>
    [Fact]
    public void Asks_the_aggregate_it_was_given_rather_than_a_fresh_one()
    {
        BoundaryEvents.AppliedBy(typeof(SampleAggregate), loaded: new SampleDcbAggregate())
            .Should().BeEquivalentTo([typeof(SampleHappenedEvent)]);
    }
}
