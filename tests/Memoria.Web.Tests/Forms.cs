using System.IO;
using System.IO.Compression;
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

    /// <summary>An archive with one entry of the given name, which need not be an assembly.</summary>
    public static byte[] Zip(string entry)
    {
        using var bytes = new MemoryStream();

        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var content = archive.CreateEntry(entry).Open();
            content.Write("not really an assembly"u8);
        }

        return bytes.ToArray();
    }
}
