using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A row on a DCB model's events tab opens over the table the way a streamed one does, addressed
/// by the one thing that makes it unique in that log: its position.
/// </summary>
/// <remarks>
/// In the collection that does not run alongside the tests swapping the static event bindings: the
/// store keys the events a model applies through those bindings, and a page read while they are
/// swapped out finds no events inside the boundary.
/// </remarks>
[Collection(nameof(TypeBindingsCollection))]
public class DcbEventRowTests
{
    private static string EventsTab(string? row = null, string? show = null) =>
        $"/samples/dcb/aggregates/detail?type={typeof(SampleCarryingDcbAggregate).FullName}" +
        $"&id={typeof(SampleCarryingId).FullName}&sampleId=abc&tab=events" +
        (row is null ? string.Empty : $"&row={row}") +
        (show is null ? string.Empty : $"&show={show}");

    private const string Seeder = "seeder";

    /// <summary>
    /// Two rows in the boundary the identifier resolves to, through the store the application
    /// itself reads — the same file, created here because nothing has written to it yet. Appended
    /// as a named operator: the audit interceptor stamps whoever the request's accessor names, and
    /// a scope opened by a test has no request until one is put on it.
    /// </summary>
    private static async Task SeedTwoEvents(MemoriaWeb web)
    {
        using var scope = web.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Seeder)], "test"))
        };
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        await store.Database.EnsureCreatedAsync();

        foreach (var position in new long[] { 1, 2 })
        {
            store.DcbEvents.Add(new DcbEventEntity
            {
                Position = position,
                EventType = "SampleCarried:1",
                Data = $$"""{"Id":"carried-{{position}}"}""",
                Tags = { new DcbEventTagEntity { Tag = "carrying:abc" } }
            });
        }

        await store.SaveChangesAsync();
    }

    [Fact]
    public async Task Every_row_on_the_events_tab_is_a_link_to_itself_opened_over_the_table()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedTwoEvents(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab()));

        page.Should().Contain("<tr class=\"clickable\">")
            .And.Contain("&amp;row=1#row\"")
            .And.Contain("&amp;row=2#row\"")
            .And.NotContain("role=\"dialog\"");
    }

    [Fact]
    public async Task An_opened_row_says_where_it_sits_in_the_log()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedTwoEvents(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(EventsTab(row: "2")));

        page.Should().Contain("role=\"dialog\"")
            .And.Contain("<dt>Position</dt>")
            .And.Contain("<dd>2</dd>")
            .And.Contain("<dt>Type</dt>")
            .And.Contain("&amp;row=2&amp;show=json#row\"")
            // The same two facts the log's own page ends with, because the boundary's read brings
            // them back with the row.
            .And.Contain("<dt>Tags</dt>")
            .And.Contain("<dd><code>carrying:abc</code></dd>")
            .And.Contain("<dt>Appended by</dt>")
            .And.Contain(Seeder);
    }

    [Fact]
    public async Task A_row_that_is_not_on_the_page_opens_nothing()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedTwoEvents(web);

        var page = await web.Client.GetStringAsync(EventsTab(row: "99"));

        page.Should().NotContain("role=\"dialog\"");
    }
}
