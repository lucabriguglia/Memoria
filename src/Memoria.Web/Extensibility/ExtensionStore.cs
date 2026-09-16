using System.IO.Compression;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Where uploaded archives and the assemblies taken out of them are kept.
/// </summary>
/// <param name="root">The directory holding both, created on first use.</param>
public sealed class ExtensionStore(string root)
{
    private const string AssemblyExtension = ".dll";

    /// <summary>Gets the directory the uploaded archives are kept in, as sent.</summary>
    public string ArchiveDirectory { get; } = Path.Combine(root, "zips");

    /// <summary>Gets the directory the assemblies are extracted to and loaded from.</summary>
    public string LibraryDirectory { get; } = Path.Combine(root, "lib");

    /// <summary>
    /// Stores one uploaded archive and extracts the assemblies in it.
    /// </summary>
    /// <param name="fileName">The name the archive was uploaded under.</param>
    /// <param name="content">The archive.</param>
    /// <exception cref="InvalidDataException">
    /// The upload is not a zip archive, carries no <c>memoria.json</c> at its root, carries one that
    /// breaks a rule, names an assembly the archive does not hold, or declares a service another
    /// installed archive already declares.
    /// </exception>
    /// <remarks>
    /// The archive is read and its manifest held to every rule before anything is written, so an
    /// upload that is refused leaves the store as it was. Assemblies are then written by file name
    /// alone: an entry named <c>bin/Release/Contoso.dll</c> and one named <c>../../Contoso.dll</c>
    /// both land on <c>lib/Contoso.dll</c>, which is what keeps a crafted archive from writing
    /// outside the store. An assembly of the same name from an earlier upload is replaced. Every
    /// assembly in the archive is extracted, named by a service or not: naming decides what is
    /// scanned for domain types, and a dependency the domain needs still has to load.
    /// </remarks>
    public void Install(string fileName, Stream content)
    {
        var archiveName = Path.GetFileName(fileName);

        // Buffered because the archive is read twice — once to validate, once to extract — and an
        // upload stream is not necessarily seekable.
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        buffer.Position = 0;

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
        {
            var manifest = ManifestOf(archive);
            var held = AssembliesIn(archive);

            foreach (var service in manifest.Services)
            {
                var missing = service.Assemblies.FirstOrDefault(named => !held.Contains(named));

                if (missing is not null)
                {
                    throw new InvalidDataException(
                        $"The service '{service.Name}' names {missing}, which is not in the archive.");
                }

                var claimedBy = InstalledArchives()
                    .Where(installed => !string.Equals(installed.Name, archiveName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(installed => installed.Services.Any(declared =>
                        string.Equals(declared.Name, service.Name, StringComparison.OrdinalIgnoreCase)));

                if (claimedBy is not null)
                {
                    throw new InvalidDataException(
                        $"The service '{service.Name}' is already declared by {claimedBy.Name}.");
                }
            }

            ExtractAssemblies(archive);
        }

        Directory.CreateDirectory(ArchiveDirectory);
        buffer.Position = 0;

        using var stored = File.Create(Path.Combine(ArchiveDirectory, archiveName));
        buffer.CopyTo(stored);
    }

    /// <summary>
    /// Takes the assemblies out of one open archive.
    /// </summary>
    private void ExtractAssemblies(ZipArchive archive)
    {
        Directory.CreateDirectory(LibraryDirectory);

        foreach (var entry in archive.Entries.Where(IsAssembly))
        {
            entry.ExtractToFile(Path.Combine(LibraryDirectory, Path.GetFileName(entry.Name)),
                overwrite: true);
        }
    }

    /// <summary>
    /// Removes one archive and the assemblies it brought.
    /// </summary>
    /// <param name="fileName">The name it was uploaded under.</param>
    /// <remarks>
    /// Which assembly came from which archive is not recorded, so the library is emptied and the
    /// archives that remain are extracted into it again. That is also what makes the case of two
    /// archives carrying the same assembly come out right: the one still installed puts it back.
    /// A name that is not installed is not an error — the file is already not there.
    /// </remarks>
    public void Remove(string fileName)
    {
        if (fileName != Path.GetFileName(fileName))
        {
            return;
        }

        var archive = Path.Combine(ArchiveDirectory, fileName);

        if (!File.Exists(archive))
        {
            return;
        }

        File.Delete(archive);

        if (Directory.Exists(LibraryDirectory))
        {
            Directory.Delete(LibraryDirectory, recursive: true);
        }

        foreach (var remaining in InstalledArchives())
        {
            try
            {
                using var content = ZipFile.OpenRead(Path.Combine(ArchiveDirectory, remaining.Name));
                ExtractAssemblies(content);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                // A file put in the directory by hand that is not a zip: it was listed as holding
                // nothing, and it extracts nothing.
            }
        }
    }

    /// <summary>Gets the archives uploaded so far, newest first.</summary>
    public IReadOnlyList<InstalledArchive> InstalledArchives()
    {
        if (!Directory.Exists(ArchiveDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(ArchiveDirectory, "*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(Describe)
            .ToList();
    }

    /// <summary>
    /// One stored archive as the settings page lists it: its file, the assembly files it holds by
    /// the names they were extracted under, and the services its manifest declares.
    /// </summary>
    /// <remarks>
    /// Read off the archive each time rather than written down when it arrived: the archive is
    /// kept whole, so it is its own record of what it brought. An archive that cannot be read, or
    /// that carries no manifest or a manifest that breaks a rule — which only a file put in the
    /// directory by hand could be, since an upload is checked before it is kept — is listed with
    /// the reason it declares nothing rather than hidden, so whoever put it there is told.
    /// </remarks>
    private static InstalledArchive Describe(FileInfo file)
    {
        try
        {
            using var archive = ZipFile.OpenRead(file.FullName);

            var assemblies = AssembliesIn(archive).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

            try
            {
                return new InstalledArchive(
                    file.Name, file.Length, file.LastWriteTimeUtc, assemblies, ManifestOf(archive).Services, Refusal: null);
            }
            catch (InvalidDataException refusal)
            {
                return new InstalledArchive(
                    file.Name, file.Length, file.LastWriteTimeUtc, assemblies, Services: [], refusal.Message);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return new InstalledArchive(
                file.Name, file.Length, file.LastWriteTimeUtc, Assemblies: [], Services: [],
                Refusal: $"{file.Name} is not a zip archive, so it holds nothing that could have been extracted.");
        }
    }

    /// <summary>
    /// The manifest one archive carries at its root.
    /// </summary>
    /// <exception cref="InvalidDataException">There is none there, or it breaks a rule.</exception>
    private static Manifest ManifestOf(ZipArchive archive)
    {
        // At the root only: an entry's full name carries its folders, so one in a folder is not
        // the manifest even when the file is called the same.
        var entry = archive.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.FullName, Manifest.FileName, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            throw new InvalidDataException(
                $"The archive carries no {Manifest.FileName} at its root; a manifest naming its services is required.");
        }

        using var reader = new StreamReader(entry.Open());

        return Manifest.Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// The assembly files one open archive holds, by the names they are extracted under. Two
    /// entries that flatten to one name are one file in the library, and are listed once.
    /// </summary>
    private static HashSet<string> AssembliesIn(ZipArchive archive) =>
        archive.Entries
            .Where(IsAssembly)
            .Select(entry => Path.GetFileName(entry.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the assemblies to load, in a stable order.</summary>
    public IReadOnlyList<string> AssemblyPaths()
    {
        if (!Directory.Exists(LibraryDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(LibraryDirectory, $"*{AssemblyExtension}")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // A directory entry has an empty Name; only files are extracted.
    private static bool IsAssembly(ZipArchiveEntry entry) =>
        !string.IsNullOrEmpty(entry.Name) &&
        entry.Name.EndsWith(AssemblyExtension, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One archive on disk.
/// </summary>
/// <param name="Name">The file name it was uploaded under.</param>
/// <param name="Length">Its size in bytes.</param>
/// <param name="UploadedUtc">When it was last written.</param>
/// <param name="Assemblies">The assembly files it holds, by the names they were extracted under.</param>
/// <param name="Services">The services its manifest declares; none when it has no usable manifest.</param>
/// <param name="Refusal">
/// Why it declares nothing, when it declares nothing: no manifest, one that breaks a rule, or a
/// file that is not a zip at all. Null for an archive that was installed.
/// </param>
public sealed record InstalledArchive(
    string Name,
    long Length,
    DateTime UploadedUtc,
    IReadOnlyList<string> Assemblies,
    IReadOnlyList<Service> Services,
    string? Refusal);
