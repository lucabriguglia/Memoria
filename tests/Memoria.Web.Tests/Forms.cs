using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Memoria.Web.Tests;

/// <summary>What a test needs to post one of the pages' forms the way the page would.</summary>
internal static class Forms
{
    /// <summary>The form field every post carries, and where it is read from.</summary>
    public const string AntiforgeryField = "__RequestVerificationToken";

    /// <summary>The token a page rendered into its forms, read off the page.</summary>
    public static string AntiforgeryToken(string page) =>
        Regex.Match(page, AntiforgeryField + "\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    /// <summary>
    /// An archive the tool will install: one entry of the given name, which need not be a real
    /// assembly, and at its root a manifest declaring one service that names it.
    /// </summary>
    /// <param name="entry">The assembly file the archive holds.</param>
    /// <param name="service">The service the manifest declares; <c>orders</c> unless said.</param>
    /// <param name="connectionString">
    /// The connection string name the service reads over; <c>Memoria</c>, the one every instance
    /// is given, unless said.
    /// </param>
    /// <param name="readRoles">The claim values that may read it, if any.</param>
    /// <param name="updateRoles">The claim values that may update it, if any.</param>
    public static byte[] Zip(
        string entry,
        string service = "orders",
        string connectionString = "Memoria",
        string[]? readRoles = null,
        string[]? updateRoles = null,
        string? description = null)
    {
        static string List(string[]? values) =>
            string.Join(", ", (values ?? []).Select(value => $"\"{value}\""));

        var described = description is null ? string.Empty : $"\"description\": \"{description}\",";

        return Zip(entry, $$"""
            { "services": [ { "name": "{{service}}", {{described}} "assemblies": ["{{Path.GetFileName(entry)}}"],
                              "connectionString": "{{connectionString}}",
                              "roles": { "read": [{{List(readRoles)}}], "update": [{{List(updateRoles)}}] } } ] }
            """);
    }

    /// <summary>An archive holding one entry and the given manifest text at its root.</summary>
    public static byte[] Zip(string entry, string manifest) => Archive(entry, manifest);

    /// <summary>
    /// An archive holding one entry and no manifest at all — what an upload from before manifests
    /// were required looks like, and what the tool refuses.
    /// </summary>
    public static byte[] ZipWithoutManifest(string entry) => Archive(entry, manifest: null);

    private static byte[] Archive(string entry, string? manifest)
    {
        using var bytes = new MemoryStream();

        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var content = archive.CreateEntry(entry).Open())
            {
                content.Write("not really an assembly"u8);
            }

            if (manifest is not null)
            {
                using var writer = new StreamWriter(archive.CreateEntry("memoria.json").Open());
                writer.Write(manifest);
            }
        }

        return bytes.ToArray();
    }
}
