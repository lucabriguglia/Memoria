using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The rows-per-page preference is this browser's own, remembered in a cookie as well as in its
/// storage. The cookie is what lets the server draw the first render at the remembered size: before
/// it, a page reached from the menu was drawn at ten rows, and the script then reloaded it at the
/// remembered size — every query run twice for one visit. A size in the address still wins, because
/// that is the one the reader just chose.
/// </summary>
public class RowsPerPageTests
{
    private const string Cookie = "memoria.rows-per-page";

    [Fact]
    public void Draws_the_remembered_size_when_the_address_names_none() =>
        InstanceQuery.PageSizeOf(asked: null, remembered: "50").Should().Be(50);

    [Fact]
    public void Lets_the_address_win_over_the_remembered_size() =>
        InstanceQuery.PageSizeOf(asked: "25", remembered: "50").Should().Be(25);

    [Theory]
    [InlineData("7")]
    [InlineData("many")]
    [InlineData(null)]
    public void Falls_back_to_the_default_when_the_remembered_size_is_not_one_offered(string? remembered) =>
        InstanceQuery.PageSizeOf(asked: null, remembered: remembered).Should().Be(InstanceQuery.DefaultPageSize);

    [Fact]
    public async Task Renders_a_data_page_at_the_size_the_cookie_remembers()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedOneEvent(web);

        var page = await Get(web, "/samples/streamed/events/data", cookie: "50");

        page.Should().Contain("<option value=\"50\" selected").And.NotContain("<option value=\"10\" selected");
    }

    [Fact]
    public async Task Renders_a_data_page_at_the_size_in_the_address_over_the_cookie()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedOneEvent(web);

        var page = await Get(web, "/samples/streamed/events/data?size=25", cookie: "50");

        page.Should().Contain("<option value=\"25\" selected").And.NotContain("<option value=\"50\" selected");
    }

    private static async Task<string> Get(MemoriaWeb web, string address, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Add("Cookie", $"{Cookie}={cookie}");
        using var response = await web.BareClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task SeedOneEvent(MemoriaWeb web)
    {
        using var scope = web.Scope();
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        await store.Database.EnsureCreatedAsync();
        store.Events.Add(new EventEntity
        {
            Id = "sample:1:0",
            StreamId = "sample:1",
            EventType = "SampleHappened:1",
            Sequence = 0,
            Data = """{"Id":"sample-1"}"""
        });
        await store.SaveChangesAsync();
    }
}
