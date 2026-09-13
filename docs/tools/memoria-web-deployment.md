# Memoria Web: deployment

[Memoria Web](memoria-web.md) is not published to NuGet. It is an ASP.NET Core application in the
repository, and you build it, publish it, and host it yourself.

> **Read [Security](memoria-web.md#security) before deciding where to put it.** Operators sign in
> through your OpenID Connect provider — see [Signing operators in](#signing-operators-in) — and
> what each may do is decided by a role mapped from a claim the provider sends. Until a mapping is
> configured every operator is a Reader; map the Administrator role only to people you would give
> shell access on the host to, because an Administrator uploads assemblies this process will load
> and execute.

## Run it locally

```bash
git clone https://github.com/lucabriguglia/Memoria.git
cd Memoria
dotnet run --project src/Memoria.Web
```

That uses the project's launch profile: the Development environment, on `http://localhost:5159`
(`https://localhost:7197` under the `https` profile). Point it at your store first — see
[Configuration](memoria-web-configuration.md) — or at the sample one, which
[Try it with sample data](memoria-web-samples.md) fills for you.

> **Use the launch profile, or set `ASPNETCORE_ENVIRONMENT` yourself.** Started with
> `--no-launch-profile`, the environment is Production and the development static-asset handler
> looks for a bundle that only exists in publish output. The scoped-CSS bundle then answers 500 and
> the application renders unstyled. The same applies to anything run out of `bin/` rather than out of
> `dotnet publish` output.

## Publish it

```bash
dotnet publish src/Memoria.Web --configuration Release --output ./web
```

Run the result with the ASP.NET Core 10 runtime:

```bash
cd web
ASPNETCORE_URLS=http://localhost:5000 \
ConnectionStrings__Memoria="Host=db;Port=5432;Database=memoria;Username=reader;Password=…" \
dotnet Memoria.Web.dll
```

Publish output serves its static assets correctly in any environment. Build output does not — see the
note above.

## What the host has to provide

| Requirement            | Why                                                                                     |
| ---------------------- | --------------------------------------------------------------------------------------- |
| ASP.NET Core 10 runtime | The published application is framework-dependent                                        |
| Network to the store    | The only external dependency there is                                                   |
| A writable content root | Uploaded archives and assemblies are written under it unless `Extensions:Directory` moves them elsewhere |

Nothing else. There is no cache, no message broker, no background worker and no scheduled job.

Every page renders statically — all of their state travels in the query string — so no component
declares an interactive render mode and no Blazor circuit is opened. Ordinary HTTP proxying is
enough; nothing here needs a WebSocket today.

### HTTPS

The pipeline calls `UseHttpsRedirection` always, and `UseHsts` outside Development. Terminating TLS at
a reverse proxy is the usual arrangement, and then the application has to be told what the proxy
saw: set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on the application and have the proxy send
`X-Forwarded-Proto` and `X-Forwarded-Host`. Without them the application sees `http://` on an
internal name, the redirect to HTTPS fights the proxy, and — worse — the address the sign-in asks the
provider to send the operator back to is built from that wrong scheme and host, so the provider
refuses it as one it was never told about. See
[forwarded headers](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer)
for a proxy that sends different header names.

### Keeping uploads across restarts

Everything in the extensions directory is read again at start-up, so uploads survive a restart as
long as that directory does. In a container, mount it:

```bash
docker run -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__Memoria="Host=db;Port=5432;Database=memoria;Username=reader;Password=…" \
  -e Extensions__Directory=/data/extensions \
  -v memoria-web-extensions:/data/extensions \
  memoria-web:latest
```

Without a volume, every deployment starts with nothing installed and everyone has to upload again.

### Run one instance

Type registration lives in the process. Two instances behind a load balancer each register their own
uploads from their own extensions directory, so the request after an upload can land on an instance
that has never seen the assembly. Run one instance unless you have a reason not to; if you must run
more, share the extensions directory between them and accept that an instance only picks up another's
upload when it is restarted or someone refreshes the types on it.

There is no horizontal-scale case to make here. The tool is read-mostly, its queries are the store's,
and the load it puts on a host is one operator at a time.

### A Dockerfile

None ships with the repository. This is the standard shape, if you want one:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Memoria.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Memoria.Web.dll"]
```

## Signing operators in

The application signs operators in itself, through whichever OpenID Connect provider it is pointed
at, and answers nothing — no page, no form post — to anyone who has not. Three settings do it; see
[Configuration](memoria-web-configuration.md#signing-operators-in) for what each one is.

```bash
docker run -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
  -e ConnectionStrings__Memoria="Host=db;Port=5432;Database=memoria;Username=reader;Password=…" \
  -e Authentication__Oidc__Authority=https://login.example.com/realms/memoria \
  -e Authentication__Oidc__ClientId=memoria-web \
  -e Authentication__Oidc__ClientSecret=… \
  -e Extensions__Directory=/data/extensions \
  -v memoria-web-extensions:/data/extensions \
  memoria-web:latest
```

At the provider, register the tool as a confidential web client with one redirect URI and one
post-logout redirect URI:

```
https://<the address operators use>/signin-oidc
https://<the address operators use>/signout-callback-oidc
```

The first is where the provider sends the operator back after they sign in; the second, after they
sign out, from where the tool takes them to its own signed-out page. The provider checks each
against what was registered character for character. Both are built from the scheme and host the
application sees, which behind a proxy is the reason for the forwarded headers above.

Sign-out ends both sessions: the tool's cookie, and the provider's own, so the next visit asks for
credentials again rather than signing the same operator straight back in.

Any provider that publishes a discovery document qualifies — Microsoft Entra ID, Amazon Cognito,
Google, Auth0, Okta, Keycloak, Zitadel, Authentik. The application never learns which; the choice
of provider, and with it of cloud, is yours.

**Map the roles.** Signed in, every operator is a Reader until a claim the provider sends is mapped
to Updater or Administrator — see [Roles](memoria-web-configuration.md#roles). Map Administrator
only to the people you would give shell access on the host to: an Administrator uploads an
assembly this process will load and execute. The provider has to send the claim, too — a group
claim, an app role, whatever it calls it — and the `Scopes` setting may need to ask for it.

A proxy that authenticates in front of the application stays perfectly valid — as a second gate, not
as the only one. It is no longer what stands between the internet and an upload form that runs
code; the application is.

### Running it open

`Authentication:Disabled=true` runs the application with nobody signed in, the way `dotnet run`
does on localhost. It is a choice that has to be written down — an application told neither this nor
a provider refuses to start — and every start-up while it is in force logs a warning saying so. Use
it on localhost, or behind a proxy that authenticates **every** request including the form posts,
and nowhere else.

## Pointing it at production data

Perfectly reasonable, with two precautions:

- **Use a read-only account** unless operators are expected to refresh snapshots. Reading needs
  `SELECT` on the store's tables (or read access to the Cosmos container); **Update** additionally
  needs to write the aggregate and projection rows. The Updater role keeps the button from
  everyone else, but it is granted per operator; a database account that cannot write is the
  guarantee that holds whatever the mapping says.
- **Expect the queries to be the store's queries.** Data pages read the same tables the application
  does. They page rather than fetching whole streams, and against a relational store the list queries
  are run once in the background at start-up so nobody's first page load pays to build the model and
  compile them — but a wide filter over a large store is still a query against your production
  database.

The tool creates nothing and deletes nothing. The only write it can make is refreshing a snapshot —
see [the one thing it writes](memoria-web.md#the-one-thing-it-writes).
