# Memoria Web: deployment

[Memoria Web](memoria-web.md) is not published to NuGet. It is an ASP.NET Core application in the
repository, and you build it, publish it, and host it yourself.

> **Read [Security](memoria-web.md#security) before deciding where to put it.** Operators sign in
> through your OpenID Connect provider — see [Signing operators in](#signing-operators-in) — and
> what each may do is decided by a role mapped from a claim the provider sends — by each service's
> manifest for that service, or by the configuration for every service. Until one or the other
> names a claim an operator holds, they see no service; map the Administrator role only to people
> you would give shell access on the host to, because an Administrator uploads assemblies this
> process will load and execute.

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

A deployment whose settings could not be read stays up and answers every address with a page saying
which setting is missing, at status 503 — see
[When it is not configured](memoria-web-configuration.md#when-it-is-not-configured). A host's own
error page in its place (App Service's "Application Error", say) means the process itself is not
running, and the reason is in the host's log rather than the tool's.

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

## Deploy it from the repository

The repository carries one deployment of its own: [`deploy-memoria-web.yml`](https://github.com/lucabriguglia/Memoria/blob/main/.github/workflows/deploy-memoria-web.yml)
publishes the application and pushes it to an Azure App Service. It runs only when someone with
write access starts it from the Actions tab, and it names nothing of the Azure it deploys to — the
tenant, the subscription, the identity and the app all come from the GitHub Environment picked when
it is started. Fork the repository, fill in your own Environment, and the same file deploys to your
own Azure. It is one worked example of hosting the tool, not a recommendation of where; any host
that meets [the requirements above](#what-the-host-has-to-provide) is as good.

### What Azure needs

An App Service for Linux on the .NET 10 runtime, a registration at the provider operators sign in
through, a Key Vault for the two values that are secrets, and an identity GitHub can sign in as.
No step stores a password anywhere it can be read back.

```bash
az group create --name memoria-web --location westeurope
az appservice plan create --name memoria-web --resource-group memoria-web --is-linux --sku B1
az webapp create --name <app> --resource-group memoria-web --plan memoria-web --runtime "DOTNETCORE:10.0"
```

Keep the plan at one instance — see [Run one instance](#run-one-instance). Then the settings. They
all live on the App Service, as application settings, and none of them in the repository or in
GitHub: they are what makes this deployment *this* one. Three are about the App Service; the rest
are the same ones any host gets — see [Configuration](memoria-web-configuration.md). Two of them are
secrets and are set as references to a Key Vault, [below](#where-the-secrets-live), rather than as
their values.

| Setting                                  | Value                                   | Why                                                                       |
| ---------------------------------------- | --------------------------------------- | ------------------------------------------------------------------------- |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED`    | `true`                                  | App Service terminates TLS in front of the application — see [HTTPS](#https) |
| `Extensions__Directory`                  | `/home/data/extensions`                 | `/home` is the one path App Service keeps across deployments and restarts |
| `WEBSITE_RUN_FROM_PACKAGE`               | `1`                                     | The published files are mounted read-only, so nothing can be written next to them |
| `Databases__{name}__Provider`            | `Npgsql`, `SqlServer` or `Cosmos`       | Only when the connection string of that name does not say which engine it is for |
| `Authentication__Oidc__Authority`        | your provider's issuer                  | See [Signing in through Entra ID](#signing-in-through-entra-id), or your own provider |
| `Authentication__Oidc__ClientId`         | what the tool is registered as          | Public by design; the provider shows it to every operator who signs in    |
| `Authorization__Roles__*`                | the claim values that grant each role   | Policy, not secret — see [Roles](memoria-web-configuration.md#roles)      |
| `APPLICATIONINSIGHTS_CONNECTION_STRING`  | set by connecting Application Insights  | Every line the tool logs about a write is then found in the portal — see [Who did what](#who-did-what) |
| `Authentication__Oidc__ClientSecret`     | a Key Vault reference                   | What the tool proves its registration with                                |
| `ConnectionStrings__{name}`              | a Key Vault reference, one per store    | One under each name the installed manifests read — `Memoria` for the samples — unless it carries no password — see [below](#a-connection-string-with-no-password) |

```bash
az webapp config appsettings set --name <app> --resource-group memoria-web --settings \
  ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
  Extensions__Directory=/home/data/extensions \
  WEBSITE_RUN_FROM_PACKAGE=1 \
  Authentication__Oidc__Authority=https://login.example.com/realms/memoria \
  Authentication__Oidc__ClientId=memoria-web \
  Authorization__Roles__Administrator=memoria-admins
```

Without the first, the address the sign-in asks the provider to send the operator back to is built
as `http://` and refused. Without the second, uploads land beside the application and the next
deployment removes them.

### Signing in through Entra ID

Any provider that publishes a discovery document will do — see
[Signing operators in](#signing-operators-in) — and Microsoft Entra ID is the one the subscription
already has. Registering the tool there produces the authority, the client id and the client secret
the settings above need, and the app roles that grant operators a role for every service — or that
a service's manifest names, for that service alone.

**Register the tool** as a confidential web client, with both addresses the tool sends operators
back to. Entra checks the post-sign-out address against the same list as the sign-in one, so both
go in as redirect URIs:

```bash
az ad app create --display-name memoria-web --sign-in-audience AzureADMyOrg \
  --web-redirect-uris https://<app>.azurewebsites.net/signin-oidc \
                      https://<app>.azurewebsites.net/signout-callback-oidc \
  --query appId -o tsv
```

The `appId` it prints is `Authentication__Oidc__ClientId`. `AzureADMyOrg` admits accounts from this
tenant only; a tool that reads a production store has no reason to accept anyone else's. If
operators reach the tool through a custom domain, register that domain's two addresses as well —
Entra compares character for character.

**Issue the secret**, straight into the file the vault step below reads, so it is never on the
screen or in the shell's history:

```bash
az ad app credential reset --id <appId> --display-name memoria-web --years 1 \
  --query password -o tsv > client-secret.txt
```

Entra secrets expire — two years at most — and an expired one fails every sign-in with an error
from Entra, not from the tool. Note the date; rotating it is one `credential reset` and one
`keyvault secret set`, and the tool picks the new value up on its next restart.

**The authority** is the tenant's v2.0 issuer:

```bash
az account show --query tenantId -o tsv
```

```
Authentication__Oidc__Authority=https://login.microsoftonline.com/<tenantId>/v2.0
```

**Define the roles** as app roles on the registration. Entra sends the `value` of every app role
an operator holds in the `roles` claim of the ID token, which is the claim the tool reads by
default, so no `RoleClaimType` and no extra scope is needed. The values are what the settings map:

```bash
cat > app-roles.json <<'EOF'
[
  { "allowedMemberTypes": ["User"], "displayName": "Administrator", "value": "memoria-admins",
    "description": "Installs, removes and rereads uploaded assemblies: runs code on the host.", "isEnabled": true },
  { "allowedMemberTypes": ["User"], "displayName": "Updater", "value": "memoria-updaters",
    "description": "Refreshes a snapshot that has fallen behind.", "isEnabled": true }
]
EOF
az ad app update --id <appId> --app-roles @app-roles.json
az ad sp create --id <appId>
```

```
Authorization__Roles__Administrator=memoria-admins
Authorization__Roles__Updater=memoria-updaters
```

Then **assign people to the roles**. That is done on the service principal the last command
created, in the portal: **Entra ID → Enterprise applications → memoria-web → Users and groups →
Add user/group**, pick the operator or a group they are in, pick the role. A group works as well as
a person, and is the usual choice: membership of the group is then the whole of who may upload an
assembly. A team's own role — `orders-team`, say — needs no mapping in the settings: the team's
service names it in its manifest, and Entra sends it in the same claim.

**Decide who may sign in at all.** As registered, every account in the tenant can sign in — and
sees nothing until a manifest or the settings name a role they hold. If only the assigned operators
should get that far, require an assignment:

```bash
az ad sp update --id <appId> --set appRoleAssignmentRequired=true
```

Anyone else is then turned away by Entra before the tool sees them.

The tool asks Entra for `openid profile email` by default, which is enough: the name shown in the
log lines comes from `profile`, and the roles ride along without being asked for. Sign-out ends
both sessions, the tool's cookie and Entra's, and lands on the post-sign-out address registered
above.

### Where the secrets live

The client secret and the connection strings go into a Key Vault, and the App Service reads them
from there through an identity of its own. The application is none the wiser: it still finds
`Authentication:Oidc:ClientSecret` and each `ConnectionStrings:{name}` in its configuration. What
changes is who can see the values. Anyone who can read the App Service's settings — the deploy
identity included — sees a reference, not a secret; rotation is one write to the vault; and the
vault logs every read.

```bash
az keyvault create --name <vault> --resource-group memoria-web --location westeurope \
  --enable-rbac-authorization true
az keyvault secret set --vault-name <vault> --name oidc-client-secret --file client-secret.txt
az keyvault secret set --vault-name <vault> --name memoria-connection-string --file connection-string.txt
```

`--file` rather than `--value`, so the secret is not in the shell's history; delete the files after.
Then give the App Service an identity and let it read the vault:

```bash
az webapp identity assign --name <app> --resource-group memoria-web        # prints its principalId
az role assignment create --assignee <principalId> --role "Key Vault Secrets User" \
  --scope /subscriptions/<subscription>/resourceGroups/memoria-web/providers/Microsoft.KeyVault/vaults/<vault>
```

**Key Vault Secrets User** reads secret values and nothing else — it cannot list the vault's other
contents, create, or delete. Finally, the two settings, as references:

```bash
az webapp config appsettings set --name <app> --resource-group memoria-web --settings \
  'Authentication__Oidc__ClientSecret=@Microsoft.KeyVault(VaultName=<vault>;SecretName=oidc-client-secret)' \
  'ConnectionStrings__Memoria=@Microsoft.KeyVault(VaultName=<vault>;SecretName=memoria-connection-string)'
```

A reference without a version, as above, follows the secret's current version: rotate it in the
vault and the App Service picks the new value up on its next restart, or within a day of its own
accord. The settings blade in the portal shows each reference with a green tick when the App
Service can resolve it and a red cross with the reason when it cannot — a missing role assignment,
a vault name typed wrong. Check it after the first deployment; a reference that does not resolve
reaches the application as the literal `@Microsoft.KeyVault(…)` string, which the application
reports as a provider it cannot connect to, not as a missing secret.

#### A connection string with no password

Against Azure SQL the connection string need not be a secret at all. Give the App Service's
identity — the one just created — a user in the database, read-only as
[Pointing it at production data](#pointing-it-at-production-data) recommends:

```sql
CREATE USER [<app>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<app>];
```

and let the driver obtain its own token:

```
Server=tcp:<server>.database.windows.net,1433;Database=memoria;Authentication=Active Directory Default;
```

That string holds nothing worth protecting, so it is a plain application setting rather than a
vault reference, and there is one secret in the vault instead of two. Azure Database for PostgreSQL
can authenticate the same identity, but the Npgsql driver expects the token to be handed to it as
the password and the tool does not do that today, so a Postgres connection string keeps its
password and stays in the vault.

### Who deploys

The identity GitHub signs in as is an app registration (or a user-assigned managed identity) with a
**federated credential** that trusts this repository's Environment, so GitHub proves who it is with
a token Azure checks against `repo:<owner>/Memoria:environment:<environment>` rather than with a
stored secret. Give it **Website Contributor** on the App Service — enough to deploy, not enough to
read the vault or touch anything else in the subscription.

```bash
az ad app create --display-name memoria-web-deploy
az ad app federated-credential create --id <appId> --parameters '{
  "name": "github-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/Memoria:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'
az ad sp create --id <appId>
az role assignment create --assignee <appId> --role "Website Contributor" \
  --scope /subscriptions/<subscription>/resourceGroups/memoria-web/providers/Microsoft.Web/sites/<app>
```

### What GitHub needs

An Environment — `production` is the workflow's default, and the name is what the federated
credential's subject has to match — holding:

| Name                    | Kind     | Value                                                        |
| ----------------------- | -------- | ------------------------------------------------------------ |
| `AZURE_TENANT_ID`       | secret   | The tenant the app registration lives in                     |
| `AZURE_SUBSCRIPTION_ID` | secret   | The subscription the App Service lives in                    |
| `AZURE_CLIENT_ID`       | secret   | The app registration's application (client) id               |
| `AZURE_WEBAPP_NAME`     | variable | The App Service's name                                       |

Add required reviewers to the Environment if a deployment should need a second person. Then
**Actions → Deploy Memoria Web → Run workflow**, pick the Environment, and the run publishes
`src/Memoria.Web` in Release, signs in with the federated credential, and pushes the output. The
run's summary links to the deployed address when it is done. Two runs against the same Environment
queue rather than race.

The workflow is only the deploy. Creating the App Service, its settings and its identity is the
one-off above, done by hand or by whatever provisions the rest of your Azure; the workflow's
identity deliberately cannot do it.

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

### How long a session lives

Exactly as long as the identity the provider issued: the session cookie expires when the ID token
does, and is not renewed on the quiet because more than half of it has gone by. Every page is
rendered per request and every request is authenticated afresh by that cookie — there is no
long-lived connection that could keep a session open past it.

So the provider's **ID token lifetime** is the knob. An operator you remove at the provider is out
at their first request after their current token ends; set the lifetime to minutes if that has to
be quick. An operator whose session ends mid-visit is sent to the provider to sign in and comes
back to the page they asked for — a form they were in the middle of posting is not replayed.

The tool does not yet go back to the provider mid-session to check whether the operator is still
welcome; the token lifetime is the whole of that guarantee.

### Running it open

`Authentication:Disabled=true` runs the application with nobody signed in, the way `dotnet run`
does on localhost. It is a choice that has to be written down — an application told neither this nor
a provider refuses — and every start-up while it is in force logs a warning saying so. Use
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
  database. The total under a list's title is counted once and kept for thirty seconds, so paging
  through a list costs one count; against a store being written to, that number can trail the
  store by up to that long. The rows themselves are always read fresh.

The tool creates nothing and deletes nothing. The only write it can make is refreshing a snapshot —
see [the one thing it writes](memoria-web.md#the-one-thing-it-writes).

### Indexes for a large store

The tool works against a store exactly as Memoria creates it, and on most stores that is fast
enough. Its list pages, though, sort on columns the store's own reads never sort on, and Memoria
deliberately indexes only what its own reads need — an index is paid for on every write, by every
application using the package, and a diagnostic tool's list page is not a reason to tax them.

So none of these are in the package. If a data page is slow against a large store, add the index
that serves it out of band, as a DBA would, and build it concurrently (`CREATE INDEX CONCURRENTLY`
on PostgreSQL, `WITH (ONLINE = ON)` on SQL Server editions that offer it) so the build does not
block the application's writes:

| Page | Table | Index | Why |
|---|---|---|---|
| Streamed → Events → Data, unfiltered | `DomainEvents` | `(CreatedDate)` | The log is read newest first by the date it was appended, across every stream. The column never changes once written, so this is the cheap kind of index: each append lands at the end of it. |
| Streamed → Aggregates or Projections → Data | `DomainAggregates`, `DomainProjections` | `(UpdatedDate)` or `(CreatedDate)`, whichever column the page is sorted on | Worth it only with hundreds of thousands of models: there is one row per model instance, so these tables are usually small beside the events. `UpdatedDate` changes on every save, so an index on it is rewritten on every save too — measure before adding it. |
| DCB → Aggregates or Projections → Data | `DcbSnapshots` | `(SnapshotKind, ModelType, UpdatedDate)` | The same caution, for the same reason. |

Two things not to index:

- **`DcbSnapshots.TagQuery`**. It is stored without a length, which SQL Server will not index at
  all, and the filter box matches it with a leading wildcard, which no index serves anyway. The
  DCB detail page does not need it: it reaches a snapshot by the row's own key.
- **`DcbEvents.CreatedDate`**. The DCB log is read in position order, which is the table's key,
  so it needs nothing.

The Cosmos pages have their own version of this — a composite index the store's policy leaves out
on purpose — and step down to a coarser order rather than ask for it; see
[what each store answers](memoria-web.md#what-each-store-answers).

### Who did what

Every line the tool logs about a write — an upload, a removal, a reread of the extensions, a
snapshot refresh, and each of their failures — is filed under an event of its own and names the
operator who asked for it, as the name the provider showed and the subject it keys them by:

```
info: Memoria.Web.Settings[1001]  Installed orders.zip, asked by Ada Lovelace (3f1c…).
info: Memoria.Web.Streamed[1011]  Refreshed the snapshot for Order in stream order:42 with id order-42:1, asked by Ada Lovelace (3f1c…).
```

So "who put that assembly on the host" is answered by the log the host already keeps. Running open,
the line says `nobody (running open)`. Nothing else the sign-in carried — no token, no other claim —
reaches the log.

On App Service, connect Application Insights to the app and every one of these lines is exported
to it, with the event name and each named value as a column — the operator's name and subject each
as one of their own, on every request as well as on every write — so the question is answered from
the portal rather than from the host's console — see
[Application Insights](memoria-web-configuration.md#application-insights) for the setting and a
query, and [What each write logs](memoria-web-configuration.md#what-each-write-logs) for the events.
