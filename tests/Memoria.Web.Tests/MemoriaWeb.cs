using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Memoria.Web.Tests;

/// <summary>
/// The application started in-process, pointed at a SQLite store and an extensions directory that
/// exist only for this instance and are gone when it is. Nothing here reaches the user's own
/// App_Data.
/// </summary>
/// <remarks>
/// The provider is not reached either. When one is configured, its discovery document is replaced
/// with a static one naming <see cref="Provider"/>'s endpoints, so the real OpenID Connect handler
/// builds its real redirect without a network to build it against. What is proved is therefore the
/// tool's side of the protocol — that it challenges, where it sends, what it asks for — and not the
/// provider's.
/// </remarks>
internal sealed class MemoriaWeb : WebApplicationFactory<Program>
{
    /// <summary>The provider every signing-in instance is told about, and where it would send.</summary>
    public static class Provider
    {
        public const string Authority = "https://login.example.com/realms/memoria";
        public const string ClientId = "memoria-web";
        public const string ClientSecret = "s3cret";
        public const string AuthorizeEndpoint = $"{Authority}/protocol/openid-connect/auth";
        public const string TokenEndpoint = $"{Authority}/protocol/openid-connect/token";
        public const string EndSessionEndpoint = $"{Authority}/protocol/openid-connect/logout";
    }

    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), $"memoria_web_tests_{Guid.NewGuid():N}");

    private readonly Dictionary<string, string?> _settings;

    private readonly string? _operator;

    private readonly (string Type, string Value)[] _claims;

    private MemoriaWeb(
        Dictionary<string, string?> settings,
        string? @operator = null,
        (string Type, string Value)[]? claims = null)
    {
        _settings = settings;
        _operator = @operator;
        _claims = claims ?? [];
    }

    /// <summary>
    /// The same instance with one more setting, said before it starts: the way a test hands it an
    /// <c>Authorization</c> mapping alongside the provider.
    /// </summary>
    public MemoriaWeb With(string setting, string value)
    {
        _settings[setting] = value;
        return this;
    }

    /// <summary>
    /// The assembly this instance knows the domain types of, as if it had been uploaded; null for
    /// an instance knowing none. Scanned as the application's own assembly is, and attributed to
    /// the one service the instance declares, <see cref="ServiceName"/>, by a manifest-only
    /// archive naming the file it would be called — so the types are browsed under
    /// <c>/samples/...</c> the way an upload's would be.
    /// </summary>
    private System.Reflection.Assembly? _host;

    /// <summary>The service every instance knowing types declares them under.</summary>
    public const string ServiceName = "samples";

    private readonly List<(string Name, string ConnectionString, System.Reflection.Assembly? Assembly, string[] Read, string[] Update, string? Description)> _services = [];

    /// <summary>
    /// The same instance declaring one more service, under the name given as a manifest would
    /// write it, over the connection string named — <c>Memoria</c>, the one every instance is
    /// given, unless said — and naming the assembly given, or the host's when none is: so the
    /// same types, or an emitted assembly's, are browsed under a second address made from that
    /// name, over a second store when one is named. The roles are the claim values the manifest
    /// writes under <c>roles.read</c> and <c>roles.update</c>; none, unless said. The description
    /// is the sentence the manifest writes under <c>description</c>; none, unless said.
    /// </summary>
    public MemoriaWeb WithService(
        string name, string connectionString = "Memoria", System.Reflection.Assembly? assembly = null,
        string[]? read = null, string[]? update = null, string? description = null)
    {
        _services.Add((name, connectionString, assembly, read ?? [], update ?? [], description));
        return this;
    }

    /// <summary>The values as a JSON array's items, quoted and comma-separated.</summary>
    private static string Quoted(string[] values) => string.Join(", ", values.Select(value => $"\"{value}\""));

    /// <summary>The description as the manifest would write it, or nothing when there is none.</summary>
    private static string Described(string? description) =>
        description is null ? string.Empty : $"\"description\": \"{description}\",";

    /// <summary>
    /// The same instance knowing the sample domain types this test assembly carries, as if they
    /// had been uploaded: what a detail page needs before it will draw its tabs at all.
    /// </summary>
    public MemoriaWeb WithSampleTypes()
    {
        _host = typeof(SampleAggregate).Assembly;
        return this;
    }

    /// <summary>
    /// The same instance knowing one streamed type and nothing of the DCB model, as an upload of a
    /// domain that only uses streams would leave it.
    /// </summary>
    public MemoriaWeb WithStreamedTypesOnly()
    {
        _host = OneSidedAssembly.Streamed;
        return this;
    }

    /// <summary>The same instance knowing one DCB type and nothing of the streamed model.</summary>
    public MemoriaWeb WithDcbTypesOnly()
    {
        _host = OneSidedAssembly.Dcb;
        return this;
    }

    /// <summary>
    /// The same instance knowing two streamed aggregates under one namespace and nothing else.
    /// </summary>
    public MemoriaWeb WithOneNamespace()
    {
        _host = OneSidedAssembly.OneNamespace;
        return this;
    }

    private bool _telemetry;

    /// <summary>
    /// The same instance told where to send telemetry, so it builds the pipeline that exports
    /// the log and the requests — and sends them to a transport of this test's own that accepts
    /// everything and never touches the network. A real address that answers nothing is not the
    /// same: on this machine a closed local port swallows the connection rather than refusing
    /// it, and every export then waits out a connect timeout, keeping the host alive past its
    /// disposal.
    /// </summary>
    public MemoriaWeb SendingTelemetry()
    {
        _settings["APPLICATIONINSIGHTS_CONNECTION_STRING"] =
            "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
            "IngestionEndpoint=http://telemetry.invalid/;LiveEndpoint=http://telemetry.invalid/";
        _telemetry = true;
        return this;
    }

    private IStreamedReads? _reads;

    private Memoria.EventSourcing.IDomainService? _domainService;

    /// <summary>
    /// The same instance writing the streamed store through the service given rather than the
    /// SQLite file: for a test of what a refresh does around the write, not of the write itself.
    /// </summary>
    public MemoriaWeb WithDomainService(Memoria.EventSourcing.IDomainService domainService)
    {
        _domainService = domainService;
        return this;
    }

    /// <summary>
    /// The same instance reading the streamed store through the reads given rather than the
    /// SQLite file: for a page test that wants to say what the page asked the store, and answer it.
    /// </summary>
    public MemoriaWeb WithReads(IStreamedReads reads)
    {
        _reads = reads;
        return this;
    }

    /// <summary>
    /// A scope inside one of this instance's services, the way a request under it would be — so a
    /// test seeding or reading the store through the application's own container resolves that
    /// service's context, over that service's store, and not nothing.
    /// </summary>
    public IServiceScope Scope(string service = ServiceName)
    {
        var scope = Services.CreateScope();
        var types = Services.GetRequiredService<DomainTypeRegistry>();

        scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(
            types.Current.ServiceAt(service) ?? throw new InvalidOperationException($"No service is at /{service}."),
            types.Current);

        return scope;
    }

    /// <summary>Where one of this instance's services remembers its list totals between pages.</summary>
    public TotalsCache Totals(string service = ServiceName)
    {
        var types = Services.GetRequiredService<DomainTypeRegistry>();

        return Services.GetRequiredService<Memoria.Web.Data.ServiceStores>()
            .For(types.Current.ServiceAt(service) ?? throw new InvalidOperationException($"No service is at /{service}."))
            .Totals;
    }

    /// <summary>The address of the sample aggregate's detail page, on the tab asked for.</summary>
    public static string SampleAggregateDetail(string tab) =>
        $"/{ServiceName}/streamed/aggregates/detail?type={typeof(SampleAggregate).FullName}&stream=sample:1&id=sample-1:1&tab={tab}";

    private static Dictionary<string, string?> ProviderSettings => new()
    {
        ["Authentication:Oidc:Authority"] = Provider.Authority,
        ["Authentication:Oidc:ClientId"] = Provider.ClientId,
        ["Authentication:Oidc:ClientSecret"] = Provider.ClientSecret
    };

    /// <summary>An instance that signs operators in through <see cref="Provider"/>.</summary>
    public static MemoriaWeb SigningIn() => new(ProviderSettings);

    /// <summary>
    /// An instance that signs operators in through <see cref="Provider"/>, and on which one already
    /// has: every request arrives as <paramref name="name"/>, as if the provider had sent them back
    /// and the cookie were set.
    /// </summary>
    /// <remarks>
    /// The identity is put on the request by a scheme of this test's own, in place of reading the
    /// cookie, because the cookie can only be written by the provider's answer. Everything after
    /// that point — the fallback policy, the pages, the header, sign-out — sees the principal it
    /// would have seen, with the name under the claim the real one carries it in.
    /// </remarks>
    public static MemoriaWeb SignedInAs(string name, params (string Type, string Value)[] claims) =>
        new(ProviderSettings, name, claims);

    /// <summary>An instance told, in so many words, to run open.</summary>
    public static MemoriaWeb Open() => new(new Dictionary<string, string?>
    {
        ["Authentication:Disabled"] = "true"
    });

    /// <summary>An instance told nothing about authentication at all.</summary>
    public static MemoriaWeb Unconfigured() => new([]);

    /// <summary>Where this instance keeps what is uploaded to it.</summary>
    public string ExtensionsDirectory => Path.Combine(_scratch, "extensions");

    /// <summary>Every file an upload has left under the extensions directory, archives and assemblies alike.</summary>
    public string[] Installed =>
        Directory.Exists(ExtensionsDirectory)
            ? Directory.GetFiles(ExtensionsDirectory, "*", SearchOption.AllDirectories)
            : [];

    /// <summary>Everything the application has logged since it started, in order.</summary>
    public IReadOnlyList<LogEntry> Logged => _logged.ToArray();

    /// <summary>One line of the application's log.</summary>
    /// <param name="Level">How loud it was said.</param>
    /// <param name="Category">Which logger said it.</param>
    /// <param name="Event">The name the line is filed under, or null when it was given none.</param>
    /// <param name="Message">What it said.</param>
    /// <param name="Columns">
    /// The named values it was said with — what a tool like Application Insights shows as columns
    /// beside the message — by name, without the template itself.
    /// </param>
    public sealed record LogEntry(
        LogLevel Level, string Category, string? Event, string Message, IReadOnlyDictionary<string, object?> Columns);

    private readonly ConcurrentQueue<LogEntry> _logged = new();

    /// <summary>
    /// The requests the application has exported so far: what a tool like Application Insights
    /// would have been sent as its <c>requests</c> rows. Empty unless the instance was told where
    /// to send them, since nothing is exported until it is.
    /// </summary>
    /// <remarks>
    /// Waited for, briefly, because a request's span ends after its response has been read: the
    /// host stops it as it disposes the request, which is after the client already holds the
    /// body. On a quiet machine the span is there by the time the client returns; on a busy one,
    /// after hundreds of tests, it is not yet, and a test that looked straight away would find the
    /// exporter empty and say so wrongly.
    /// </remarks>
    public async Task<IReadOnlyList<Activity>> Requests()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (true)
        {
            var requests = _spans.Where(span => span.Kind == ActivityKind.Server).ToList();

            if (requests.Count > 0 || DateTime.UtcNow > deadline)
            {
                return requests;
            }

            await Task.Delay(20);
        }
    }

    private readonly ConcurrentQueue<Activity> _spans = new();

    /// <summary>
    /// A client that stops at the first redirect rather than following it, since following it
    /// would leave the test server for the provider.
    /// </summary>
    public HttpClient Client => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    /// <summary>
    /// A client that keeps no cookies of its own, for a test that sends the one it made.
    /// </summary>
    public HttpClient BareClient => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    /// <summary>
    /// A session cookie as the application would have issued it after the provider sent the
    /// operator back: a ticket for <paramref name="name"/>, protected with the cookie scheme's
    /// own format and keys, issued and expiring when told. What a request carrying it exercises
    /// is the real cookie path — the one <see cref="SignedInAs"/> stands in for.
    /// </summary>
    /// <returns>The value of the <c>Cookie</c> header to send.</returns>
    public string SessionCookie(string name, DateTimeOffset issuedUtc, DateTimeOffset expiresUtc)
    {
        _ = Client;

        var options = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        var identity = new ClaimsIdentity(
            [new Claim("sub", name.ToLowerInvariant()), new Claim("name", name)],
            CookieAuthenticationDefaults.AuthenticationScheme, nameType: "name", roleType: "roles");

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IssuedUtc = issuedUtc, ExpiresUtc = expiresUtc },
            CookieAuthenticationDefaults.AuthenticationScheme);

        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_scratch);

        // The services this instance declares: a manifest-only archive each, put in the directory
        // the way a zip from before manifests would be, naming the file the assembly would be
        // called. Nothing is extracted from them; the registry scans the assemblies it was given
        // — the host and any an extra service brought — and attributes their types to the service
        // naming each by that name. The host's own service comes first, when there is a host.
        var declared = new List<(string Name, string ConnectionString, System.Reflection.Assembly Assembly, string[] Read, string[] Update, string? Description)>();

        if (_host is { } host)
        {
            declared.Add((ServiceName, "Memoria", host, [], [], null));
        }

        declared.AddRange(_services
            .Where(service => service.Assembly is not null || _host is not null)
            .Select(service => (service.Name, service.ConnectionString, service.Assembly ?? _host!, service.Read, service.Update, service.Description)));

        if (declared.Count > 0)
        {
            var zips = Path.Combine(ExtensionsDirectory, "zips");
            Directory.CreateDirectory(zips);

            foreach (var ((name, connectionString, assembly, read, update, description), index) in declared.Select((service, index) => (service, index)))
            {
                using var archive = ZipFile.Open(Path.Combine(zips, $"service-{index}.zip"), ZipArchiveMode.Create);
                using var manifest = new StreamWriter(archive.CreateEntry("memoria.json").Open());
                manifest.Write($$"""
                    { "services": [ { "name": "{{name}}", {{Described(description)}} "assemblies": ["{{assembly.GetName().Name}}.dll"],
                                      "connectionString": "{{connectionString}}",
                                      "roles": { "read": [{{Quoted(read)}}], "update": [{{Quoted(update)}}] } } ] }
                    """);
            }
        }

        // Everything the application's own settings files say about authentication is unset
        // first, so that the development file's choice to run open does not reach these tests.
        // UseSetting rather than a configuration source: it is what reaches the application's
        // configuration before Program reads it. A null lands as an empty string, which the
        // application reads as no setting at all.
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Memoria"] = $"Data Source={Path.Combine(_scratch, "store.db")}",
            ["Extensions:Directory"] = ExtensionsDirectory,
            ["Authentication:Disabled"] = null,
            ["Authentication:Oidc:Authority"] = null,
            ["Authentication:Oidc:ClientId"] = null,
            ["Authentication:Oidc:ClientSecret"] = null,
            ["Authentication:Oidc:Scopes"] = null
        };

        foreach (var (key, value) in _settings)
        {
            settings[key] = value;
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(new Capture(_logged)));

        builder.ConfigureTestServices(services =>
        {
            services.Configure<OpenIdConnectOptions>(
                OpenIdConnectDefaults.AuthenticationScheme,
                options => options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = Provider.Authority,
                    AuthorizationEndpoint = Provider.AuthorizeEndpoint,
                    TokenEndpoint = Provider.TokenEndpoint,
                    EndSessionEndpoint = Provider.EndSessionEndpoint
                });

            // Only ever built when the application registered a pipeline to build, so an instance
            // told nowhere to send telemetry records nothing here either.
            services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
                tracing.AddProcessor(new SpanCapture(_spans)));

            if (_telemetry)
            {
                // Registered after the application's own, so it is applied after: the address the
                // application read stays, and what is sent there goes to the accepting transport
                // instead. Live metrics and offline storage are off because both would reach
                // outside the process — the one for a channel to the portal, the other for a
                // directory under the profile of whoever runs the tests.
                services.Configure<AzureMonitorOptions>(options =>
                {
                    options.Transport = new HttpClientTransport(new Accepting());
                    options.EnableLiveMetrics = false;
                    options.DisableOfflineStorage = true;
                });
            }

            var scanned = new List<System.Reflection.Assembly>();

            if (_host is { } host)
            {
                scanned.Add(host);
            }

            scanned.AddRange(_services.Select(service => service.Assembly).OfType<System.Reflection.Assembly>());

            if (scanned.Count > 0)
            {
                // Registered after the application's own, so it is the one resolved — and the one
                // Program reloads at start-up. Over the same store, so an upload still lands. Every
                // assembly a declared service names is scanned the way the application's own is.
                services.AddSingleton(provider =>
                    new DomainTypeRegistry(provider.GetRequiredService<ExtensionStore>(), scanned.ToArray()));
            }

            if (_reads is { } reads)
            {
                // Registered after the application's own, so it is the one resolved.
                services.AddScoped(_ => reads);
            }

            if (_domainService is { } domainService)
            {
                services.AddScoped(_ => domainService);
            }

            if (_operator is null)
            {
                return;
            }

            services.AddAuthentication()
                .AddScheme<OperatorOptions, OperatorHandler>(OperatorHandler.Scheme, options =>
                {
                    options.Name = _operator;
                    options.Claims = _claims;
                });

            // Asked first, in place of the cookie. The challenge and the sign-out stay the
            // application's own, so those are still what is proved.
            services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultAuthenticateScheme = OperatorHandler.Scheme);
        });
    }

    private sealed class OperatorOptions : AuthenticationSchemeOptions
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>What else the provider said about them: the groups, under whatever claim.</summary>
        public (string Type, string Value)[] Claims { get; set; } = [];
    }

    /// <summary>Signs every request in as the one operator it was told about.</summary>
    private sealed class OperatorHandler(
        IOptionsMonitor<OperatorOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<OperatorOptions>(options, logger, encoder)
    {
        public const string Scheme = "Operator";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // The claim the real sign-in reads the name from, and no SOAP-era URI: the
            // application asks the provider to keep the names it gave them.
            var identity = new ClaimsIdentity(
                [new Claim("sub", Options.Name.ToLowerInvariant()), new Claim("name", Options.Name)],
                Scheme, nameType: "name", roleType: "roles");

            foreach (var (type, value) in Options.Claims)
            {
                identity.AddClaim(new Claim(type, value));
            }

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }

    /// <summary>
    /// Answers every export as the ingestion endpoint would have, in the shape the exporter reads
    /// back, without a connection: what leaves the process is what is being tested, not whether
    /// it arrived.
    /// </summary>
    private sealed class Accepting : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"itemsReceived":1,"itemsAccepted":1,"errors":[]}""",
                    System.Text.Encoding.UTF8, "application/json")
            });
    }

    /// <summary>Keeps every span as it ends, so a test can ask what would have been exported.</summary>
    private sealed class SpanCapture(ConcurrentQueue<Activity> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Enqueue(data);
    }

    /// <summary>A logger that keeps every line, so a test can ask what the application said.</summary>
    private sealed class Capture(ConcurrentQueue<LogEntry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new LogEntry(
                    logLevel, category, eventId.Name, formatter(state, exception), Columns(state)));

            /// <summary>
            /// The named values, as every structured logger hands them over: pairs, the last of
            /// which is the template under a name of its own that is not a value.
            /// </summary>
            private static IReadOnlyDictionary<string, object?> Columns<TState>(TState state) =>
                state is IReadOnlyList<KeyValuePair<string, object?>> pairs
                    ? pairs.Where(pair => pair.Key != "{OriginalFormat}")
                        .GroupBy(pair => pair.Key)
                        .ToDictionary(group => group.Key, group => group.First().Value)
                    : new Dictionary<string, object?>();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
            // Left behind in the temp directory, which is what it is for.
        }
    }
}
