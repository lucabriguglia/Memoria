using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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

    private MemoriaWeb(Dictionary<string, string?> settings) => _settings = settings;

    /// <summary>An instance that signs operators in through <see cref="Provider"/>.</summary>
    public static MemoriaWeb SigningIn() => new(new Dictionary<string, string?>
    {
        ["Authentication:Oidc:Authority"] = Provider.Authority,
        ["Authentication:Oidc:ClientId"] = Provider.ClientId,
        ["Authentication:Oidc:ClientSecret"] = Provider.ClientSecret
    });

    /// <summary>An instance told, in so many words, to run open.</summary>
    public static MemoriaWeb Open() => new(new Dictionary<string, string?>
    {
        ["Authentication:Disabled"] = "true"
    });

    /// <summary>An instance told nothing about authentication at all.</summary>
    public static MemoriaWeb Unconfigured() => new([]);

    /// <summary>Where this instance keeps what is uploaded to it.</summary>
    public string ExtensionsDirectory => Path.Combine(_scratch, "extensions");

    /// <summary>Everything the application has logged since it started, in order.</summary>
    public IReadOnlyList<LogEntry> Logged => _logged.ToArray();

    /// <summary>One line of the application's log.</summary>
    public sealed record LogEntry(LogLevel Level, string Category, string Message);

    private readonly ConcurrentQueue<LogEntry> _logged = new();

    /// <summary>
    /// A client that stops at the first redirect rather than following it, since following it
    /// would leave the test server for the provider.
    /// </summary>
    public HttpClient Client => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_scratch);

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

        builder.ConfigureTestServices(services => services.Configure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme,
            options => options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = Provider.Authority,
                AuthorizationEndpoint = Provider.AuthorizeEndpoint,
                TokenEndpoint = Provider.TokenEndpoint,
                EndSessionEndpoint = Provider.EndSessionEndpoint
            }));
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
                entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception)));
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
