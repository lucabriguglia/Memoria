using System.IO.Compression;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

public class ExtensionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private ExtensionStore Store() => new(_root);

    /// <summary>
    /// A zip holding the named entries, each carrying its own name as its bytes so a test can tell
    /// one file's content from another's — and, at its root, a manifest declaring one service
    /// under <paramref name="service"/> that names every assembly among the entries, since an
    /// archive without one is refused.
    /// </summary>
    private static Stream ZipOf(string service, params string[] entryNames)
    {
        var assemblies = entryNames
            .Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName);

        return ZipWith(ManifestOf(service, assemblies.ToArray()), entryNames);
    }

    /// <summary>A zip holding the named entries and, when given one, the manifest text at its root.</summary>
    private static Stream ZipWith(string? manifest, params string[] entryNames)
    {
        var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entryName in entryNames)
            {
                using var entry = archive.CreateEntry(entryName).Open();
                using var writer = new StreamWriter(entry);
                writer.Write(entryName);
            }

            if (manifest is not null)
            {
                using var entry = archive.CreateEntry("memoria.json").Open();
                using var writer = new StreamWriter(entry);
                writer.Write(manifest);
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string ManifestOf(string service, params string[] assemblies) =>
        $$"""
        { "services": [ { "name": "{{service}}", "assemblies": [{{string.Join(", ", assemblies.Select(name => $"\"{name}\""))}}],
                          "connectionString": "Memoria" } ] }
        """;

    private static Stream NotAZip() => new MemoryStream("plain text"u8.ToArray());

    private string[] FilesUnder(string directory) =>
        Directory.Exists(Path.Combine(_root, directory))
            ? Directory.GetFiles(Path.Combine(_root, directory)).Select(Path.GetFileName).ToArray()!
            : [];

    [Fact]
    public void Extracts_assemblies_from_the_uploaded_archive()
    {
        Store().Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        File.Exists(Path.Combine(_root, "lib", "Contoso.Domain.dll")).Should().BeTrue();
    }

    [Fact]
    public void Keeps_the_uploaded_archive()
    {
        Store().Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        File.Exists(Path.Combine(_root, "zips", "pack.zip")).Should().BeTrue();
    }

    [Fact]
    public void Ignores_entries_that_are_not_assemblies()
    {
        Store().Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll", "readme.txt", "Contoso.Domain.xml"));

        FilesUnder("lib").Should().BeEquivalentTo("Contoso.Domain.dll");
    }

    [Fact]
    public void Flattens_assemblies_held_in_folders()
    {
        Store().Install("pack.zip", ZipOf("pack", "bin/Release/Contoso.Domain.dll"));

        File.Exists(Path.Combine(_root, "lib", "Contoso.Domain.dll")).Should().BeTrue();
    }

    /// <summary>
    /// A dependency the domain needs is extracted with the rest whether or not a service names it —
    /// naming decides what is scanned, not what is loaded.
    /// </summary>
    [Fact]
    public void Extracts_an_assembly_no_service_names()
    {
        Store().Install("pack.zip", ZipWith(ManifestOf("pack", "Contoso.Domain.dll"), "Contoso.Domain.dll", "FluentValidation.dll"));

        FilesUnder("lib").Should().BeEquivalentTo("Contoso.Domain.dll", "FluentValidation.dll");
    }

    [Fact]
    public void Overwrites_an_assembly_a_previous_upload_left_behind()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        store.Install("other.zip", ZipOf("other", "bin/Contoso.Domain.dll"));

        File.ReadAllText(Path.Combine(_root, "lib", "Contoso.Domain.dll"))
            .Should().Be("bin/Contoso.Domain.dll");
    }

    [Fact]
    public void Overwrites_an_archive_of_the_same_name()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "First.dll"));

        store.Install("pack.zip", ZipOf("pack", "Second.dll"));

        FilesUnder("zips").Should().BeEquivalentTo("pack.zip");
    }

    [Fact]
    public void Writes_nothing_outside_the_library_directory()
    {
        Store().Install("pack.zip", ZipOf("pack", "../../escaped.dll"));

        File.Exists(Path.Combine(_root, "lib", "escaped.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_root, "..", "escaped.dll")).Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_upload_that_is_not_a_zip_archive()
    {
        var install = () => Store().Install("pack.zip", NotAZip());

        install.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Leaves_no_archive_behind_when_the_upload_is_not_a_zip()
    {
        try
        {
            Store().Install("pack.zip", NotAZip());
        }
        catch (InvalidDataException)
        {
            // The point of the test is what is on disk afterwards.
        }

        File.Exists(Path.Combine(_root, "zips", "pack.zip")).Should().BeFalse();
    }

    /// <summary>
    /// The manifest is what says which service the assemblies belong to and where its store is,
    /// so a zip without one has nothing to be installed as.
    /// </summary>
    [Fact]
    public void Refuses_an_archive_without_a_manifest_and_installs_nothing()
    {
        var install = () => Store().Install("pack.zip", ZipWith(manifest: null, "Contoso.Domain.dll"));

        using (new AssertionScope())
        {
            install.Should().Throw<InvalidDataException>().WithMessage("*memoria.json*required*");
            FilesUnder("zips").Should().BeEmpty();
            FilesUnder("lib").Should().BeEmpty();
        }
    }

    /// <summary>At the root, not anywhere in the archive, so there is one place to look.</summary>
    [Fact]
    public void Reads_the_manifest_only_at_the_archive_root()
    {
        var install = () => Store().Install("pack.zip",
            ZipWith(manifest: null, "Contoso.Domain.dll", "sub/memoria.json"));

        install.Should().Throw<InvalidDataException>().WithMessage("*memoria.json*required*");
    }

    [Fact]
    public void Refuses_a_manifest_that_breaks_a_rule_and_installs_nothing()
    {
        var install = () => Store().Install("pack.zip",
            ZipWith(ManifestOf("not a name", "Contoso.Domain.dll"), "Contoso.Domain.dll"));

        using (new AssertionScope())
        {
            install.Should().Throw<InvalidDataException>().WithMessage("*letters, digits and hyphens*");
            FilesUnder("zips").Should().BeEmpty();
            FilesUnder("lib").Should().BeEmpty();
        }
    }

    [Fact]
    public void Refuses_a_service_naming_an_assembly_the_archive_does_not_carry()
    {
        var install = () => Store().Install("pack.zip",
            ZipWith(ManifestOf("pack", "Contoso.Domain.dll", "Missing.dll"), "Contoso.Domain.dll"));

        using (new AssertionScope())
        {
            install.Should().Throw<InvalidDataException>().WithMessage("*'pack'*Missing.dll*not in the archive*");
            FilesUnder("zips").Should().BeEmpty();
        }
    }

    /// <summary>
    /// Matched the way extraction names files: by file name alone, wherever the entry sits, and
    /// without regard to case — neither a zip entry nor a Windows file system treats a difference
    /// of case as a different file.
    /// </summary>
    [Fact]
    public void Matches_a_named_assembly_by_file_name_anywhere_in_the_archive_whatever_its_case()
    {
        Store().Install("pack.zip",
            ZipWith(ManifestOf("pack", "contoso.domain.dll"), "bin/Release/Contoso.Domain.dll"));

        FilesUnder("zips").Should().BeEquivalentTo("pack.zip");
    }

    /// <summary>
    /// A service name is the address it is browsed under, so two archives cannot both declare one.
    /// </summary>
    [Fact]
    public void Refuses_a_service_name_another_archive_already_declares_whatever_its_case()
    {
        var store = Store();
        store.Install("one.zip", ZipWith(ManifestOf("orders", "One.dll"), "One.dll"));

        var install = () => store.Install("two.zip", ZipWith(ManifestOf("Orders", "Two.dll"), "Two.dll"));

        using (new AssertionScope())
        {
            install.Should().Throw<InvalidDataException>().WithMessage("*'Orders'*already declared by one.zip*");
            FilesUnder("zips").Should().BeEquivalentTo("one.zip");
            FilesUnder("lib").Should().BeEquivalentTo("One.dll");
        }
    }

    /// <summary>An archive uploaded again replaces its own services rather than colliding with them.</summary>
    [Fact]
    public void Lets_an_archive_declare_again_the_services_it_declared_before()
    {
        var store = Store();
        store.Install("pack.zip", ZipWith(ManifestOf("orders", "One.dll"), "One.dll"));

        var install = () => store.Install("pack.zip", ZipWith(ManifestOf("orders", "Two.dll"), "Two.dll"));

        install.Should().NotThrow();
    }

    /// <summary>
    /// Removing an archive has to take its assemblies with it, or the types it brought would go on
    /// being registered from a library nothing accounts for.
    /// </summary>
    [Fact]
    public void Removing_an_archive_removes_the_assemblies_it_brought()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        store.Remove("pack.zip");

        store.AssemblyPaths().Should().BeEmpty();
    }

    [Fact]
    public void Removing_an_archive_leaves_the_others_alone()
    {
        var store = Store();
        store.Install("one.zip", ZipOf("one", "One.dll"));
        store.Install("two.zip", ZipOf("two", "Two.dll"));

        store.Remove("one.zip");

        store.AssemblyPaths().Select(Path.GetFileName).Should().BeEquivalentTo("Two.dll");
        store.InstalledArchives().Select(archive => archive.Name).Should().BeEquivalentTo("two.zip");
    }

    /// <summary>
    /// Two archives can carry an assembly of the same name, and the one still installed keeps it.
    /// </summary>
    [Fact]
    public void Keeps_an_assembly_another_archive_also_carries()
    {
        var store = Store();
        store.Install("one.zip", ZipOf("one", "Shared.dll"));
        store.Install("two.zip", ZipOf("two", "Shared.dll"));

        store.Remove("one.zip");

        store.AssemblyPaths().Select(Path.GetFileName).Should().BeEquivalentTo("Shared.dll");
    }

    [Fact]
    public void Removing_something_that_is_not_installed_does_nothing()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        var remove = () => store.Remove("never-installed.zip");

        remove.Should().NotThrow();
        store.InstalledArchives().Should().ContainSingle();
    }

    /// <summary>
    /// The name arrives from a form, where a path could be typed instead.
    /// </summary>
    [Fact]
    public void Refuses_to_remove_anything_outside_the_store()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll"));

        store.Remove("../../pack.zip");

        store.InstalledArchives().Should().ContainSingle();
    }

    [Fact]
    public void Lists_the_installed_archives()
    {
        var store = Store();
        store.Install("one.zip", ZipOf("one", "One.dll"));
        store.Install("two.zip", ZipOf("two", "Two.dll"));

        store.InstalledArchives().Select(archive => archive.Name)
            .Should().BeEquivalentTo("one.zip", "two.zip");
    }

    /// <summary>
    /// The settings page says what each archive brought, so an archive has to know which assembly
    /// files it holds — by file name alone, the same way they were extracted.
    /// </summary>
    [Fact]
    public void Lists_the_assemblies_each_archive_holds()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "bin/Release/Two.dll", "One.dll", "readme.txt"));

        store.InstalledArchives().Single().Assemblies.Should().Equal("One.dll", "Two.dll");
    }

    /// <summary>
    /// The sheet over the installed table says what each archive declares, so an archive knows
    /// its services — read off its manifest each time, the way its assemblies are, since the
    /// archive is kept whole and is its own record.
    /// </summary>
    [Fact]
    public void Lists_the_services_each_archive_declares()
    {
        var store = Store();
        store.Install("pack.zip", ZipWith("""
            { "services": [ { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "Orders",
                              "roles": { "read": ["orders-team"], "update": ["orders-leads"] } } ] }
            """, "Orders.dll"));

        var service = store.InstalledArchives().Single().Services.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            service.Name.Should().Be("orders");
            service.Assemblies.Should().Equal("Orders.dll");
            service.ConnectionString.Should().Be("Orders");
            service.ReadRoles.Should().Equal("orders-team");
            service.UpdateRoles.Should().Equal("orders-leads");
        }
    }

    /// <summary>
    /// An archive put in the directory by hand — before manifests were required, say — is listed
    /// rather than hidden, declares nothing, and says why.
    /// </summary>
    [Fact]
    public void Lists_an_archive_without_a_manifest_with_the_reason_it_declares_nothing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "zips"));
        using (var file = File.Create(Path.Combine(_root, "zips", "old.zip")))
        {
            ZipWith(manifest: null, "Old.dll").CopyTo(file);
        }

        var archive = Store().InstalledArchives().Single();

        using (new AssertionScope())
        {
            archive.Services.Should().BeEmpty();
            archive.Refusal.Should().Contain("memoria.json").And.Contain("required");
        }
    }

    [Fact]
    public void Lists_an_installed_archive_with_no_refusal()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "One.dll"));

        store.InstalledArchives().Single().Refusal.Should().BeNull();
    }

    [Fact]
    public void Lists_no_archives_before_anything_is_uploaded()
    {
        Store().InstalledArchives().Should().BeEmpty();
    }

    [Fact]
    public void Reports_the_assemblies_to_load()
    {
        var store = Store();
        store.Install("pack.zip", ZipOf("pack", "Contoso.Domain.dll", "Contoso.Contracts.dll"));

        store.AssemblyPaths().Select(Path.GetFileName)
            .Should().BeEquivalentTo("Contoso.Domain.dll", "Contoso.Contracts.dll");
    }

    [Fact]
    public void Reports_no_assemblies_before_anything_is_uploaded()
    {
        Store().AssemblyPaths().Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
