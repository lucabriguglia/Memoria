using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A service's view of the catalogue: only the types registered from the assemblies its manifest
/// names, so a page under one service never lists another's. Which service a type belongs to is
/// decided by the file its assembly was loaded from — the name the manifest names it by.
/// </summary>
public class ServiceCatalogueTests
{
    private static readonly LoadedAssembly Samples = new("Samples.dll", typeof(SampleAggregate).Assembly);

    private static readonly LoadedAssembly Streamed = new("StreamedOnly.dll", OneSidedAssembly.Streamed);

    private static Service ServiceNaming(string name, params string[] assemblies) =>
        new(name, assemblies, "Memoria", ReadRoles: [], UpdateRoles: []);

    private static DomainTypeCatalogue Catalogue() => new()
    {
        Assemblies = [Samples, Streamed],
        Services = [ServiceNaming("samples", "Samples.dll"), ServiceNaming("streamed", "StreamedOnly.dll")],
        Events = [typeof(SampleHappenedEvent)],
        StreamedEvents = [typeof(SampleHappenedEvent)],
        StreamedAggregates = [typeof(SampleAggregate)],
        StreamedStreamIds = [typeof(SampleStreamId), .. OneSidedAssembly.Streamed.GetTypes()],
        DcbProjectionIds = [typeof(SampleDcbProjectionId)],
        Errors = ["one line"]
    };

    [Fact]
    public void Keeps_only_the_types_registered_from_the_assemblies_the_service_names()
    {
        var samples = Catalogue().For(ServiceNaming("samples", "Samples.dll"));

        using (new AssertionScope())
        {
            samples.StreamedAggregates.Should().Equal(typeof(SampleAggregate));
            samples.StreamedStreamIds.Should().Equal(typeof(SampleStreamId));
            samples.StreamedEvents.Should().Equal(typeof(SampleHappenedEvent));
            samples.DcbProjectionIds.Should().Equal(typeof(SampleDcbProjectionId));
        }
    }

    [Fact]
    public void Leaves_another_service_s_types_out()
    {
        var streamed = Catalogue().For(ServiceNaming("streamed", "StreamedOnly.dll"));

        using (new AssertionScope())
        {
            streamed.StreamedStreamIds.Should().Equal(OneSidedAssembly.Streamed.GetTypes());
            streamed.StreamedAggregates.Should().BeEmpty();
            streamed.Events.Should().BeEmpty();
            streamed.HasDcbTypes.Should().BeFalse();
        }
    }

    /// <summary>
    /// Matched the way the store matches a named assembly: by file name, without regard to case.
    /// </summary>
    [Fact]
    public void Matches_a_named_assembly_whatever_its_case()
    {
        Catalogue().For(ServiceNaming("samples", "samples.DLL")).StreamedAggregates
            .Should().Equal(typeof(SampleAggregate));
    }

    [Fact]
    public void Has_nothing_for_a_service_naming_an_assembly_that_did_not_load()
    {
        var missing = Catalogue().For(ServiceNaming("missing", "Missing.dll"));

        using (new AssertionScope())
        {
            missing.Count.Should().Be(0);
            missing.HasStreamedTypes.Should().BeFalse();
        }
    }

    /// <summary>What is not a list of types is the catalogue's and stays: the errors, the services, the files.</summary>
    [Fact]
    public void Keeps_what_belongs_to_the_whole_catalogue()
    {
        var samples = Catalogue().For(ServiceNaming("samples", "Samples.dll"));

        using (new AssertionScope())
        {
            samples.Errors.Should().Equal("one line");
            samples.Services.Should().HaveCount(2);
            samples.Assemblies.Should().HaveCount(2);
        }
    }

    /// <summary>
    /// The application's own assembly is scanned alongside the uploads, and belongs to a service
    /// the same way: when a manifest names the file it would be called.
    /// </summary>
    [Fact]
    public void Counts_the_host_assembly_as_the_file_a_manifest_would_name_it_by()
    {
        var catalogue = Catalogue() with
        {
            Assemblies = [Streamed],
            Hosts = [new LoadedAssembly("Samples.dll", typeof(SampleAggregate).Assembly)]
        };

        catalogue.For(ServiceNaming("samples", "Samples.dll")).StreamedAggregates
            .Should().Equal(typeof(SampleAggregate));
    }

    /// <summary>
    /// A service is found by the address its name makes, whatever case the address was typed in —
    /// never by the name as written, which may carry spaces and marks an address cannot.
    /// </summary>
    [Theory]
    [InlineData("samples", "samples")]
    [InlineData("SAMPLES", "samples")]
    [InlineData("samples-streamed", "Samples Streamed")]
    [InlineData("Samples-Streamed", "Samples Streamed")]
    public void Finds_a_service_by_its_address_whatever_its_case(string asked, string found)
    {
        var catalogue = Catalogue() with
        {
            Services = [ServiceNaming("samples", "Samples.dll"), ServiceNaming("Samples Streamed", "StreamedOnly.dll")]
        };

        catalogue.ServiceAt(asked)!.Name.Should().Be(found);
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("Samples Streamed")]
    public void Finds_no_service_at_an_address_nobody_s_name_makes(string asked)
    {
        var catalogue = Catalogue() with
        {
            Services = [ServiceNaming("samples", "Samples.dll"), ServiceNaming("Samples Streamed", "StreamedOnly.dll")]
        };

        catalogue.ServiceAt(asked).Should().BeNull();
    }
}
