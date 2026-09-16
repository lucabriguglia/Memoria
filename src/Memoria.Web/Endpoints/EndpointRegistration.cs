using System.Security.Claims;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.Web.Components;
using Memoria.Web.Extensibility;
using Memoria.Web.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Memoria.Web.Endpoints;

// Inside the namespace, and aliased at all, because this file sits under Memoria, where a plain
// Results binds to the framework's own namespace of that name rather than to the class the
// redirects below are built with. An alias outside the namespace would still lose to it.
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>
/// Everything this application answers on: the pages themselves, and the handful of form posts the
/// statically rendered ones send.
/// </summary>
/// <remarks>
/// The posts are plain minimal-API endpoints rather than interactive components because the pages
/// that send them render statically like the rest. Antiforgery still applies — binding a form marks
/// the endpoint, and each form carries the token via <c>&lt;AntiforgeryToken/&gt;</c>.
/// </remarks>
public static class EndpointRegistration
{
    /// <summary>
    /// Maps the components, their static assets, and the writes the pages post to.
    /// </summary>
    /// <param name="app">The application.</param>
    public static WebApplication MapPages(this WebApplication app)
    {
        app.MapSettings();
        app.MapDcbModels();
        app.MapStreamedModels();

        // The stylesheet and the script answer anyone: a page has to be signed into before it is
        // drawn, and a redirect that cannot draw itself would be the one thing lost by asking.
        // Everything else is protected by saying nothing, which is what the fallback policy is for.
        app.MapStaticAssets().AllowAnonymous();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>
    /// The three writes the settings page offers: installing uploads, removing one, and rereading
    /// what is installed.
    /// </summary>
    private static void MapSettings(this WebApplication app)
    {
        app.MapPost("/settings/upload", (
            DomainTypeRegistry types,
            ExtensionStore store,
            ILoggerFactory loggerFactory,
            ClaimsPrincipal user,
            [FromForm] IFormFileCollection files) =>
        {
            var logger = loggerFactory.CreateLogger("Memoria.Web.Settings");
            var asked = Operator.Of(user);

            if (files.Count == 0)
            {
                return Back(error: "Choose at least one zip file.");
            }

            foreach (var file in files)
            {
                try
                {
                    using var content = file.OpenReadStream();
                    store.Install(file.FileName, content);
                    logger.ExtensionInstalled(file.FileName, asked);
                }
                catch (Exception exception)
                {
                    logger.ExtensionNotInstalled(exception, file.FileName, asked);
                    return Back(error: $"{file.FileName} could not be installed: {exception.Message}");
                }
            }

            types.Reload();
            logger.LogCatalogue(types.Current);

            return Back(message: $"Uploaded {files.Count} file(s). {types.Current.Count} type(s) registered.");
        }).RequireAuthorization(Roles.Administrator);

        app.MapPost("/settings/delete", (
            DomainTypeRegistry types,
            ExtensionStore store,
            ILoggerFactory loggerFactory,
            ClaimsPrincipal user,
            [FromForm] string name) =>
        {
            var logger = loggerFactory.CreateLogger("Memoria.Web.Settings");
            var asked = Operator.Of(user);

            try
            {
                store.Remove(name);
                logger.ExtensionRemoved(name, asked);
            }
            catch (Exception exception)
            {
                logger.ExtensionNotRemoved(exception, name, asked);
                return Back(error: $"{name} could not be removed: {exception.Message}");
            }

            types.Reload();
            logger.LogCatalogue(types.Current);

            return Back(message: $"Removed {name}. {types.Current.Count} type(s) registered.");
        }).RequireAuthorization(Roles.Administrator);

        app.MapPost("/settings/refresh", async (
            HttpContext context,
            IAntiforgery antiforgery,
            DomainTypeRegistry types,
            ILoggerFactory loggerFactory,
            ClaimsPrincipal user) =>
        {
            var logger = loggerFactory.CreateLogger("Memoria.Web.Settings");

            // This endpoint binds no form field, so the antiforgery middleware does not treat it as a
            // form post and would let it through unchecked. Refreshing changes what every user
            // resolves, so it is checked here instead.
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Back(error: "That request could not be verified. Reload the page and try again.",
                    tab: "types");
            }

            // Said as a write, because it is one in every sense but the store's: what every operator
            // resolves changes the moment it runs.
            logger.ExtensionsReread(Operator.Of(user));

            types.Reload();
            logger.LogCatalogue(types.Current);

            return Back(message: $"{types.Current.Count} type(s) registered.", tab: "types");
        }).DisableAntiforgery().RequireAuthorization(Roles.Administrator);
    }

    /// <summary>
    /// The one write the DCB pages offer, and the same one for each of the two models.
    /// </summary>
    /// <remarks>
    /// A form post rather than an interactive component, so the detail pages stay statically
    /// rendered like the rest of them — and a POST rather than a link, because it writes a snapshot.
    /// </remarks>
    private static void MapDcbModels(this WebApplication app)
    {
        app.MapPost("/{service}/dcb/aggregates/update", async (
            string service,
            DomainTypeRegistry types,
            IDcbDomainService store,
            TotalsCache totals,
            ILoggerFactory loggerFactory,
            HttpRequest request,
            [FromForm] string type,
            [FromForm] string id,
            [FromForm] string returnUrl,
            ClaimsPrincipal user) =>
            Under(types, service) is { } catalogue ? await Refresh(DcbModelKind.Aggregate, catalogue, store, totals, loggerFactory, request, type, id, returnUrl, Operator.Of(user)) : Results.NotFound())
            .RequireAuthorization(Roles.Updater);

        app.MapPost("/{service}/dcb/projections/update", async (
            string service,
            DomainTypeRegistry types,
            IDcbDomainService store,
            TotalsCache totals,
            ILoggerFactory loggerFactory,
            HttpRequest request,
            [FromForm] string type,
            [FromForm] string id,
            [FromForm] string returnUrl,
            ClaimsPrincipal user) =>
            Under(types, service) is { } catalogue ? await Refresh(DcbModelKind.Projection, catalogue, store, totals, loggerFactory, request, type, id, returnUrl, Operator.Of(user)) : Results.NotFound())
            .RequireAuthorization(Roles.Updater);
    }

    /// <summary>
    /// The same one write for the two streamed models.
    /// </summary>
    /// <remarks>
    /// The streamed pages reach a row by the two ids the store keyed it with rather than by the
    /// values an identifier was built from, because that is what the row they were opened from
    /// carries. So these take the ids and work back to the things: the store folds a stream through
    /// an identifier and neither is a string to it.
    /// </remarks>
    private static void MapStreamedModels(this WebApplication app)
    {
        app.MapPost("/{service}/streamed/aggregates/update", async (
            string service,
            DomainTypeRegistry types,
            IDomainService store,
            TotalsCache totals,
            ILoggerFactory loggerFactory,
            [FromForm] string type,
            [FromForm] string stream,
            [FromForm] string id,
            [FromForm] string returnUrl,
            ClaimsPrincipal user) =>
            Under(types, service) is { } catalogue ? await RefreshStreamed(
                StreamedModelKind.Aggregate, catalogue, store, totals, loggerFactory, type, stream, id, returnUrl, Operator.Of(user)) : Results.NotFound())
            .RequireAuthorization(Roles.Updater);

        app.MapPost("/{service}/streamed/projections/update", async (
            string service,
            DomainTypeRegistry types,
            IDomainService store,
            TotalsCache totals,
            ILoggerFactory loggerFactory,
            [FromForm] string type,
            [FromForm] string stream,
            [FromForm] string id,
            [FromForm] string returnUrl,
            ClaimsPrincipal user) =>
            Under(types, service) is { } catalogue ? await RefreshStreamed(
                StreamedModelKind.Projection, catalogue, store, totals, loggerFactory, type, stream, id, returnUrl, Operator.Of(user)) : Results.NotFound())
            .RequireAuthorization(Roles.Updater);
    }

    /// <summary>
    /// The catalogue a post under a service reads through: the service's own view of the types,
    /// or null when no manifest declares the name — which the post answers as not found, the way
    /// the pages under that name are.
    /// </summary>
    private static DomainTypeCatalogue? Under(DomainTypeRegistry types, string service) =>
        types.Current.ServiceAt(service) is { } named ? types.Current.For(named) : null;

    // Back to the settings page carrying what happened, so the outcome survives the redirect.
    // The tab is carried back with the message because the settings page writes each one under the
    // button that produced it: an upload's answer belongs on the installed tab, a refresh's on the
    // types tab, and landing on the other one would leave the answer where it was not asked for.
    private static IResult Back(string? message = null, string? error = null, string tab = "installed")
    {
        var query = message is not null
            ? $"?message={Uri.EscapeDataString(message)}"
            : $"?error={Uri.EscapeDataString(error ?? string.Empty)}";

        return Results.LocalRedirect($"/settings{query}&tab={tab}");
    }

    /// <summary>
    /// Brings one model's snapshot up to date and goes back to the page the button was pressed on,
    /// carrying what happened.
    /// </summary>
    /// <remarks>
    /// One handler for aggregates and projections, because the two differ only in which list the
    /// posted names are matched against and which of the store's two update methods is called — and
    /// the second of those the refresher reads off the identifier rather than being told.
    /// </remarks>
    private static async Task<IResult> Refresh(
        DcbModelKind kind,
        DomainTypeCatalogue catalogue,
        IDcbDomainService store,
        TotalsCache totals,
        ILoggerFactory loggerFactory,
        HttpRequest request,
        string type,
        string id,
        string returnUrl,
        Operator asked)
    {
        var logger = loggerFactory.CreateLogger("Memoria.Web.Dcb");

        // Matched against what is registered, exactly as the page matches them, so a name posted here
        // can only ever reach a type this application already knows about — and only one of the two
        // kinds, so a projection cannot be refreshed through the aggregates' address.
        var model = DomainTypeDescriber.Select(catalogue.Models(kind), type);

        var identifierType = model is null
            ? null
            : DomainTypeDescriber.Describe(model, catalogue.Identifiers(kind))
                .Identifiers.FirstOrDefault(candidate => candidate.FullName == id);

        if (model is null || identifierType is null)
        {
            return BackToModel(returnUrl,
                error: $"That {Named(kind)} and identifier are no longer registered.");
        }

        // The identifier's own values, posted under the names its constructor takes — the same shape
        // the address carries them in.
        var values = request.Form.ToDictionary(
            field => field.Key, field => (string?)field.Value.LastOrDefault(),
            StringComparer.OrdinalIgnoreCase);

        var created = IdentifierFactory.Create(identifierType, values);

        if (created.Instance is null)
        {
            return BackToModel(returnUrl, error: created.Error ?? "That identifier could not be built.");
        }

        var refreshed = await ModelRefresher.Refresh(store, model, created.Instance);

        return Answer(logger, totals, returnUrl, model, Addressed(identifierType, values), refreshed, asked,
            nothingToDo: $"Nothing to refresh — no snapshot, and no events inside the boundary this " +
                         $"{Named(kind)} applies.");
    }

    /// <summary>
    /// Brings one streamed model's snapshot up to date and goes back to the page the button was
    /// pressed on, carrying what happened.
    /// </summary>
    /// <remarks>
    /// One handler for aggregates and projections, for the same reason the DCB one is: the two
    /// differ only in which lists the posted names are matched against, and which of the store's two
    /// update methods is called the refresher reads off the identifier rather than being told.
    /// <para>
    /// What differs from the DCB handler is what arrives. There the identifier's own values are
    /// posted and the identifier is built from them; here the two ids the store keyed the row by are
    /// posted, because that is what the row the page was opened from carries — so both the stream
    /// and the identifier are worked back out of them before the store is asked to write.
    /// </para>
    /// </remarks>
    private static async Task<IResult> RefreshStreamed(
        StreamedModelKind kind,
        DomainTypeCatalogue catalogue,
        IDomainService store,
        TotalsCache totals,
        ILoggerFactory loggerFactory,
        string type,
        string stream,
        string id,
        string returnUrl,
        Operator asked)
    {
        var logger = loggerFactory.CreateLogger("Memoria.Web.Streamed");

        // Matched against what is registered, exactly as the page matches it, so a name posted here
        // can only ever reach a type this application already knows about — and only one of the two
        // kinds, so a projection cannot be refreshed through the aggregates' address.
        var model = DomainTypeDescriber.Select(catalogue.Streamed(kind), type);

        if (model is null)
        {
            return BackToModel(returnUrl, error: $"That {Named(kind)} is no longer registered.");
        }

        // The id half of the store's key, which is what an identifier produced; the other half is
        // the type's version, and no identifier ever wrote it.
        var identity = StreamedIdentity.Of(
            catalogue, kind, model, stream, DomainTypeDescriber.SplitKey(id).Name);

        if (identity.Stream is null)
        {
            return BackToModel(returnUrl,
                error: "Nothing registered writes stream ids of this shape, so there is nowhere to " +
                       "fold a snapshot from.");
        }

        if (identity.Identifier is null)
        {
            return BackToModel(returnUrl,
                error: $"Nothing registered addresses this {Named(kind)} by that id, so there is " +
                       "nothing to refresh it with.");
        }

        var refreshed = await ModelRefresher.Refresh(store, model, identity.Stream, identity.Identifier);

        return Answer(logger, totals, returnUrl, model, $"in stream {stream} with id {id}", refreshed, asked,
            nothingToDo: $"Nothing to refresh — no snapshot, and no events in this stream that this " +
                         $"{Named(kind)} folds.");
    }

    /// <summary>
    /// What a refresh did, logged and carried back to the page that asked for it.
    /// </summary>
    /// <param name="logger">The store's own logger.</param>
    /// <param name="totals">The remembered totals, forgotten when a snapshot was written.</param>
    /// <param name="returnUrl">The page the button was pressed on.</param>
    /// <param name="model">The model that was refreshed, for the log.</param>
    /// <param name="instance">Which one, the way the page addressed it, for the log.</param>
    /// <param name="refreshed">What the store said.</param>
    /// <param name="asked">Who asked, for the log.</param>
    /// <param name="nothingToDo">
    /// What to say when there was nothing to bring up to date, which each store says in its own
    /// terms: a boundary the model applies, or a stream it folds.
    /// </param>
    private static IResult Answer(
        ILogger logger, TotalsCache totals, string returnUrl, Type model, string instance,
        RefreshedModel refreshed, Operator asked, string nothingToDo)
    {
        if (refreshed.Error is not null)
        {
            logger.SnapshotNotRefreshed(model.Name, instance, asked, refreshed.Error);
            return BackToModel(returnUrl, error: refreshed.Error);
        }

        if (!refreshed.Refreshed)
        {
            logger.SnapshotUpToDate(model.Name, instance, asked);
            return BackToModel(returnUrl, message: nothingToDo);
        }

        logger.SnapshotRefreshed(model.Name, instance, asked);

        // The one change to the store this tool can see coming: a snapshot written may be a row a
        // models page did not count.
        totals.Forget();

        return BackToModel(returnUrl, message: "Snapshot refreshed.");
    }

    /// <summary>
    /// How a DCB model is addressed, for the log: the identifier's type and the values it was
    /// built from, under the names its constructor takes.
    /// </summary>
    /// <remarks>
    /// Only the values the identifier asks for, and never the rest of the form: what else was posted
    /// — the token, the return address — is nobody's business in a log.
    /// </remarks>
    private static string Addressed(Type identifierType, IReadOnlyDictionary<string, string?> values)
    {
        var named = IdentifierFactory.Parameters(identifierType)
            .Select(parameter => $"{parameter.Name}={values.GetValueOrDefault(parameter.Name)}");

        return $"addressed by {identifierType.Name}({string.Join(", ", named)})";
    }

    /// <summary>What a kind of model is called in a sentence.</summary>
    private static string Named<TKind>(TKind kind) where TKind : struct, Enum =>
        kind.ToString()!.ToLowerInvariant();

    /// <summary>
    /// Back to the page the button was pressed on, carrying what happened so the outcome survives
    /// the redirect.
    /// </summary>
    /// <remarks>
    /// A local redirect: the return address arrives on the form, so it may not send anyone off-site.
    /// </remarks>
    private static IResult BackToModel(string returnUrl, string? message = null, string? error = null)
    {
        var separator = returnUrl.Contains('?') ? "&" : "?";
        var carried = message is not null
            ? $"message={Uri.EscapeDataString(message)}"
            : $"error={Uri.EscapeDataString(error ?? string.Empty)}";

        return Results.LocalRedirect($"{returnUrl}{separator}{carried}");
    }
}
