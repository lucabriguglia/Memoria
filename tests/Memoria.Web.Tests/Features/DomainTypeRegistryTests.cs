using System.IO.Compression;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The registry rebuilds binding maps the whole process shares, so these run one at a time.
/// </summary>
[Collection(nameof(TypeBindingsCollection))]
public class DomainTypeRegistryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private ExtensionStore Store() => new(_root);

    private DomainTypeRegistry Registry() => new(Store());

    /// <summary>
    /// Installs an archive carrying this test assembly under each of the given file names, with a
    /// manifest declaring one service that names the assemblies in <paramref name="named"/> —
    /// every file given, unless told which.
    /// </summary>
    private void Install(string archive, string service, string[] files, string[]? named = null)
    {
        var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                using var entry = zip.CreateEntry(file).Open();
                entry.Write(File.ReadAllBytes(typeof(SampleAggregate).Assembly.Location));
            }

            using var manifest = new StreamWriter(zip.CreateEntry("memoria.json").Open());
            var assemblies = string.Join(", ", (named ?? files).Select(file => $"\"{file}\""));
            manifest.Write($$"""
                { "services": [ { "name": "{{service}}", "assemblies": [{{assemblies}}], "connectionString": "Memoria" } ] }
                """);
        }

        buffer.Position = 0;
        Store().Install(archive, buffer);
    }

    [Fact]
    public void Publishes_an_empty_catalogue_before_the_first_reload()
    {
        Registry().Current.Count.Should().Be(0);
    }

    [Fact]
    public void Publishes_what_it_found()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        registry.Current.StreamedAggregates.Should().Contain(type => type.Name == nameof(SampleAggregate));
        registry.Current.DcbAggregateIds.Should().Contain(type => type.Name == nameof(SampleDcbAggregateId));
    }

    /// <summary>
    /// Each service gets a binding set of its own, built from its own assemblies, which is what
    /// its stores are read through; the process-wide maps are left alone, so two services may
    /// bind one key to two types.
    /// </summary>
    [Fact]
    public void Binds_each_service_s_types_to_both_models_in_a_set_of_its_own()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        var bindings = registry.Current.BindingsOf(registry.Current.ServiceAt("domain")!);
        using (new AssertionScope())
        {
            bindings.EventTypeBindings.Should().ContainKey("SampleHappened:1");
            bindings.AggregateTypeBindings.Should().ContainKey("SampleAggregate:1");
            bindings.ProjectionTypeBindings.Should().ContainKey("SampleProjection:1");
            bindings.DcbAggregateTypeBindings.Should().ContainKey("SampleDcbAggregate:1");
            bindings.DcbProjectionTypeBindings.Should().ContainKey("SampleDcbProjection:1");
            TypeBindings.EventTypeBindings.Should().NotContainKey("SampleHappened:1");
        }
    }

    [Fact]
    public void Binds_nothing_for_a_service_naming_an_assembly_that_did_not_load()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        var stranger = new Service("Stranger", ["Missing.dll"], "Memoria", [], []);
        registry.Current.BindingsOf(stranger).EventTypeBindings.Should().BeEmpty();
    }

    /// <summary>
    /// Registering from scratch: what an earlier reload bound has to go, or a type the user removed
    /// would go on being offered.
    /// </summary>
    [Fact]
    public void Forgets_types_that_are_no_longer_there()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();
        registry.Reload();

        Store().Remove("domain.zip");
        registry.Reload();

        using (new AssertionScope())
        {
            registry.Current.Services.Should().BeEmpty();
            registry.Current.Bindings.Should().BeEmpty();
            registry.Current.Count.Should().Be(0);
        }
    }

    [Fact]
    public void Picks_up_a_type_added_since_the_last_reload()
    {
        var registry = Registry();
        registry.Reload();

        Install("domain.zip", "domain", ["Domain.dll"]);
        registry.Reload();

        registry.Current.BindingsOf(registry.Current.ServiceAt("domain")!).EventTypeBindings
            .Should().ContainKey("SampleHappened:1");
    }

    [Fact]
    public void Records_when_it_last_reloaded()
    {
        var registry = Registry();

        registry.Reload();

        registry.Current.ReloadedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Two uploads claiming one name and version cannot both be bound. Reporting beats throwing:
    /// the reload runs inside a request someone is waiting on, and everyone shares the result.
    /// </summary>
    [Fact]
    public void Reports_a_name_claimed_twice_rather_than_throwing()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        Install("again.zip", "again", ["DomainAgain.dll"]);
        var registry = Registry();

        var reload = () => registry.Reload();

        reload.Should().NotThrow();
        registry.Current.Errors.Should().Contain(error => error.Contains("SampleHappened"));
    }

    /// <summary>
    /// The settings page lists, under each archive, the types the assemblies in it brought — so the
    /// catalogue has to say which file each type was registered from.
    /// </summary>
    [Fact]
    public void Says_which_types_were_registered_from_each_file()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        var registered = registry.Current.RegisteredFrom("Domain.dll");
        registered.Should().Contain(kind => kind.Label == "Streamed aggregates")
            .Which.Types.Should().Contain(type => type.Name == nameof(SampleAggregate));
        registered.Should().Contain(kind => kind.Label == "Streamed events")
            .Which.Types.Should().Contain(type => type.Name == nameof(SampleHappenedEvent));
    }

    [Fact]
    public void Registers_nothing_from_a_file_that_was_not_loaded()
    {
        Install("domain.zip", "domain", ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        registry.Current.RegisteredFrom("Missing.dll").Should().BeEmpty();
    }

    /// <summary>
    /// The manifest says whose types are whose: an assembly in the archive that no service names
    /// is a dependency, loaded so the named ones resolve and scanned for nothing — even when it
    /// carries attributed types of its own.
    /// </summary>
    [Fact]
    public void Scans_only_the_assemblies_a_service_names()
    {
        Install("domain.zip", "domain", ["Domain.dll", "Dependency.dll"], named: ["Domain.dll"]);
        var registry = Registry();

        registry.Reload();

        using (new AssertionScope())
        {
            registry.Current.RegisteredFrom("Domain.dll").Should().NotBeEmpty();
            registry.Current.RegisteredFrom("Dependency.dll").Should().BeEmpty();
            registry.Current.StreamedAggregates.Should().ContainSingle(type => type.Name == nameof(SampleAggregate));
        }
    }

    /// <summary>
    /// An assembly in the library that no installed archive accounts for — left by hand, or by an
    /// archive since removed — is not scanned either: only a service names what is registered.
    /// </summary>
    [Fact]
    public void Scans_nothing_no_installed_archive_names()
    {
        var store = Store();
        Directory.CreateDirectory(store.LibraryDirectory);
        File.WriteAllBytes(Path.Combine(store.LibraryDirectory, "Stray.dll"),
            File.ReadAllBytes(typeof(SampleAggregate).Assembly.Location));
        var registry = Registry();

        registry.Reload();

        registry.Current.Count.Should().Be(0);
    }

    public void Dispose()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>();
        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>();
        TypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>();
        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>();
        DcbTypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

[CollectionDefinition(nameof(TypeBindingsCollection), DisableParallelization = true)]
public class TypeBindingsCollection;
