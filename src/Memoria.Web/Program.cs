using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Extensions;
using Memoria.Extensions;
using Memoria.Web;
using Memoria.Web.Data;
using Memoria.Web.Endpoints;
using Memoria.Web.Extensibility;
using Memoria.Web.Security;

var builder = WebApplication.CreateBuilder(args);

AuthenticationSettings authentication;
AuthorizationSettings roles;

try
{
    // Every connection string the tool has must be readable, whatever it is called: a service
    // names the one it reads over, and a string nobody can open is a mistake best found before a
    // page asks for it. Which strings there are is not insisted on — a service naming one that is
    // not there is listed as unreachable rather than stopping everything.
    ConnectionStrings.Validate(builder.Configuration);

    // Whether operators sign in, read here for the same reason the strings are: a tool told neither
    // refuses, rather than running open because nobody said otherwise.
    authentication = AuthenticationSettings.Of(builder.Configuration);

    // Which of the provider's claim values make an operator more than a Reader. Silence maps nobody.
    roles = AuthorizationSettings.Of(builder.Configuration);
}
catch (InvalidOperationException refusal)
{
    // Refused before a single service was added: what runs instead is a page saying which
    // settings would have let it start, and nothing else — see StartupRefusal for why the
    // process stays up to say so rather than leaving it to the host's own error page.
    builder.Refusing(refusal).Run();
    return;
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignIn(authentication, roles);

// Whether the log leaves the host, read from the setting App Service sets when Application
// Insights is connected. Nothing is exported until it is.
var sent = builder.AddTelemetry();

builder.Services.AddMemoria(typeof(Program));

// The two event sourcing models side by side. Each store registration replaces the default
// no-op service its model registers, so the stores come after.
builder.Services.AddMemoriaEventSourcing(typeof(Program));
builder.Services.AddMemoriaDcb(typeof(Program));

// The domain types uploaded through the settings page. Only registered here — the assemblies are
// read below, and again whenever someone uploads or asks for a refresh.
builder.Services.AddDomainExtensions(
    new ExtensionStore(
        builder.Configuration["Extensions:Directory"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "extensions")),
    typeof(Program).Assembly);

// One store per service, over the connection string its manifest names: the readers, contexts and
// domain services a request under a service resolves are built from the service the request is
// inside. A relational store brings the contexts with it; a Cosmos store brings a client and none.
builder.Services.AddStores();

var app = builder.Build();

app.Logger.LogSignIn(authentication, roles);
app.Logger.LogTelemetry(sent);

var registry = app.Services.GetRequiredService<DomainTypeRegistry>();
registry.Reload();
app.Logger.LogCatalogue(registry.Current);

// One line per service saying which string it opened and with which engine, or why it opened
// none: a store that answers nothing is the first thing anyone will suspect the connection string
// of, and this says how it was read.
app.Logger.LogStores(app.Services.GetRequiredService<ServiceStores>());

app.WarmInBackground();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Before antiforgery, whose tokens are bound to the signed-in identity: a form token issued to
// nobody would not match the operator who posts it back.
app.UseAuthentication();
app.UseAuthorization();

// Inside the service the address names, before anything answers: a page and a form post under
// a service both resolve that service's store.
app.UseServiceScope();

app.UseAntiforgery();

app.MapPages();
app.MapSignOut(authentication);

app.Run();
