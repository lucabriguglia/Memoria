namespace Memoria.Web.Extensibility;

/// <summary>
/// The service the request is inside, when it is inside one: the segment in front of every page
/// that reads a store, resolved once by the router and read by the layout, the crumbs and the
/// pages under it.
/// </summary>
/// <remarks>
/// One per request, since a request is under one service or none. Set by the router rather than
/// by each page reading its own route parameter, so the layout — which has no route — knows
/// which menu to draw, and so a name nobody declares is answered in one place, as not found.
/// Outside a service, <see cref="Service"/> is null and <see cref="Catalogue"/> is empty: there
/// is nothing to lay out.
/// </remarks>
public sealed class CurrentService
{
    /// <summary>The service the request is inside, or null outside every service.</summary>
    public Service? Service { get; private set; }

    /// <summary>The service's view of the types: what the pages under it list.</summary>
    public DomainTypeCatalogue Catalogue { get; private set; } = DomainTypeCatalogue.Empty;

    /// <summary>The service's name as its manifest wrote it, for showing; empty outside one.</summary>
    public string Name => Service?.Name ?? string.Empty;

    /// <summary>The segment its pages sit under — the address its name makes; empty outside one.</summary>
    public string Segment => Service?.Slug ?? string.Empty;

    /// <summary>Whether the request is inside a service at all.</summary>
    public bool Inside => Service is not null;

    /// <summary>
    /// Enters one service for the rest of the request, with the catalogue narrowed to it.
    /// </summary>
    /// <param name="service">The service the address named.</param>
    /// <param name="catalogue">The whole catalogue, which is narrowed here.</param>
    public void Enter(Service service, DomainTypeCatalogue catalogue)
    {
        Service = service;
        Catalogue = catalogue.For(service);
    }
}
