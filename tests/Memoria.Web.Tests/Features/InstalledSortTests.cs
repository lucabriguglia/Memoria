using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The Installed table can be sorted by file name, size and upload date, from its headings, and
/// opens newest first: what was just uploaded is what an operator most often came to see.
/// </summary>
public class InstalledSortTests
{
    private const string Installed = "/settings?tab=installed";

    [Fact]
    public async Task Lists_the_newest_upload_first_until_told_otherwise()
    {
        using var web = MemoriaWeb.Open();
        Install(web, "oldest.zip", size: 100, uploaded: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Install(web, "newest.zip", size: 100, uploaded: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        Install(web, "middle.zip", size: 100, uploaded: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var page = Markup.Plain(await web.Client.GetStringAsync(Installed));

        using (new AssertionScope())
        {
            Order(page, "newest.zip", "middle.zip", "oldest.zip").Should().BeInAscendingOrder();
            page.Should().Contain("class=\"col-uploaded\" aria-sort=\"descending\"");
        }
    }

    [Theory]
    [InlineData("sort=file&dir=asc", "alpha.zip", "Beta.zip", "gamma.zip")]
    [InlineData("sort=file&dir=desc", "gamma.zip", "Beta.zip", "alpha.zip")]
    [InlineData("sort=size&dir=asc", "Beta.zip", "gamma.zip", "alpha.zip")]
    [InlineData("sort=size&dir=desc", "alpha.zip", "gamma.zip", "Beta.zip")]
    [InlineData("sort=uploaded&dir=asc", "gamma.zip", "alpha.zip", "Beta.zip")]
    public async Task Sorts_by_the_column_and_direction_the_address_asks_for(string query, string first, string second, string third)
    {
        using var web = MemoriaWeb.Open();
        Install(web, "Beta.zip", size: 1_000, uploaded: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        Install(web, "alpha.zip", size: 3_000, uploaded: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        Install(web, "gamma.zip", size: 2_000, uploaded: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var page = Markup.Plain(await web.Client.GetStringAsync($"{Installed}&{query}"));

        Order(page, first, second, third).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// Each heading is the control that sorts by it. The one sorted by turns around when pressed;
    /// another starts the way its column is usually wanted — a name from A, a size or a date
    /// from the largest or the newest.
    /// </summary>
    [Fact]
    public async Task Sorts_from_the_headings()
    {
        using var web = MemoriaWeb.Open();
        Install(web, "only.zip", size: 100, uploaded: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var opened = Markup.Plain(await web.Client.GetStringAsync(Installed));
        var byName = Markup.Plain(await web.Client.GetStringAsync($"{Installed}&sort=file&dir=asc"));

        using (new AssertionScope())
        {
            opened.Should().Contain("<a href=\"settings?tab=installed&amp;sort=file&amp;dir=asc\">File")
                .And.Contain("<a href=\"settings?tab=installed&amp;sort=size&amp;dir=desc\">Size")
                .And.Contain("<a href=\"settings?tab=installed&amp;sort=uploaded&amp;dir=asc\">Uploaded");
            byName.Should().Contain("class=\"col-file\" aria-sort=\"ascending\"")
                .And.Contain("<a href=\"settings?tab=installed&amp;sort=file&amp;dir=desc\">File")
                .And.NotContain("class=\"col-uploaded\" aria-sort");
        }
    }

    /// <summary>
    /// A sheet opened from a sorted table opens over the table as sorted, and closes back to it:
    /// the order was the reader's choice, and opening a service is not a reason to lose it.
    /// </summary>
    [Fact]
    public async Task Keeps_the_sort_through_a_service_s_sheet()
    {
        using var web = MemoriaWeb.Open();
        Install(web, "only.zip", size: 100, uploaded: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var byName = Markup.Plain(await web.Client.GetStringAsync($"{Installed}&sort=file&dir=asc"));

        byName.Should().Contain("href=\"settings?tab=installed&amp;sort=file&amp;dir=asc&amp;service=only#service\"");
    }

    /// <summary>Where each name first appears down the page, which is the order the rows are in.</summary>
    private static int[] Order(string page, params string[] names) =>
        names.Select(name => page.IndexOf($"aria-label=\"Delete {name}\"", StringComparison.Ordinal) is var at && at >= 0
                ? at
                : throw new InvalidOperationException($"{name} is not on the page"))
            .ToArray();

    /// <summary>
    /// A zip put in the extensions directory by hand, the way an upload leaves one: a manifest
    /// declaring one service named after the file, and an assembly entry of random bytes so the
    /// archive is the size asked for rather than what deflate makes of it. Its write time is the
    /// upload date the table reads.
    /// </summary>
    private static void Install(MemoriaWeb web, string file, int size, DateTime uploaded)
    {
        var zips = Path.Combine(web.ExtensionsDirectory, "zips");
        Directory.CreateDirectory(zips);
        var path = Path.Combine(zips, file);
        var service = Path.GetFileNameWithoutExtension(file);
        var bytes = new byte[size];
        new Random(size).NextBytes(bytes);

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var content = archive.CreateEntry($"{service}.dll").Open())
            {
                content.Write(bytes);
            }

            using var manifest = new StreamWriter(archive.CreateEntry("memoria.json").Open());
            manifest.Write($$"""
                { "services": [ { "name": "{{service}}", "assemblies": ["{{service}}.dll"], "connectionString": "Memoria" } ] }
                """);
        }

        File.SetLastWriteTimeUtc(path, uploaded);
    }
}
