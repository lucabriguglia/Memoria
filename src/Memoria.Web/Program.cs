using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Extensions;
using Memoria.Extensions;
using Memoria.Web.Data;
using Memoria.Web.Endpoints;
using Memoria.Web.Extensibility;
using Memoria.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Which engine the store is in is read off the connection string, or taken from Database:Provider
// where the string could be more than one. The tool is pointed at a store somebody else created,
// so it is told rather than assuming Postgres.
var database = DatabaseConnection.Of(
    builder.Configuration.GetConnectionString(DatabaseConnection.Name),
    builder.Configuration[DatabaseConnection.Setting]);

// Whether operators sign in, read here for the same reason the store is: a tool told neither
// refuses to start, rather than starting open because nobody said otherwise.
var authentication = AuthenticationSettings.Of(builder.Configuration);

builder.Services.AddSignIn(authentication);

builder.Services.AddMemoria(typeof(Program));

// The two event sourcing models side by side. Each store call replaces the default no-op service
// its model registers, so it comes after.
builder.Services.AddMemoriaEventSourcing(typeof(Program));
builder.Services.AddMemoriaDcb(typeof(Program));

// Whichever store the connection string named, and the reader the streamed pages ask through. A
// relational store brings four contexts with it; a Cosmos store brings a client and no context.
builder.Services.AddStore(database, builder.Configuration);

// The domain types uploaded through the settings page. Only registered here — the assemblies are
// read below, and again whenever someone uploads or asks for a refresh.
builder.Services.AddDomainExtensions(
    new ExtensionStore(
        builder.Configuration["Extensions:Directory"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "extensions")),
    typeof(Program).Assembly);

var app = builder.Build();

// Logged because the provider is now read rather than fixed: a store that answers nothing is the
// first thing anyone will suspect the connection string of, and this says how it was read.
app.Logger.LogInformation("Store opened with {Provider}.", database.Provider);
app.Logger.LogSignIn(authentication);

var registry = app.Services.GetRequiredService<DomainTypeRegistry>();
registry.Reload();
app.Logger.LogCatalogue(registry.Current);

app.WarmInBackground(database);

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

app.UseAntiforgery();

app.MapPages();
app.MapSignOut(authentication);

app.Run();
