# Memoria Web: configuration

Everything [Memoria Web](memoria-web.md) needs is configuration, and only two things are required:
the store to open, and how operators sign in. The rest have defaults that are right for a store
installed under Memoria's own default names.

Settings are read the way ASP.NET Core reads any of them — `appsettings.json`,
`appsettings.{Environment}.json`, environment variables, then command-line arguments — so a setting
can be overridden without editing a file.

## Every setting

| Setting                          | Required                        | Default                               | What it is                                       |
| -------------------------------- | ------------------------------- | ------------------------------------- | ------------------------------------------------ |
| `ConnectionStrings:Memoria`      | Yes                             | —                                     | The store to open                                |
| `Database:Provider`              | Only when the string is unclear | Read off the connection string        | `Npgsql`, `SqlServer`, `Sqlite` or `Cosmos`      |
| `Database:Cosmos:DatabaseName`   | No                              | `Memoria`                             | Cosmos only: the database the container is in    |
| `Database:Cosmos:ContainerName`  | No                              | `Domain`                              | Cosmos only: the container the store writes into |
| `Extensions:Directory`           | No                              | `<content root>/App_Data/extensions`  | Where uploaded archives and assemblies are kept  |
| `Authentication:Oidc:Authority`  | Unless running open             | —                                     | The OpenID Connect provider operators sign in through |
| `Authentication:Oidc:ClientId`   | Unless running open             | —                                     | What the tool is registered as at that provider  |
| `Authentication:Oidc:ClientSecret` | Unless running open           | —                                     | What the tool proves that registration with      |
| `Authentication:Oidc:Scopes`     | No                              | `openid profile email`                | What is asked of the provider, space-separated   |
| `Authentication:Disabled`        | Unless signing in               | —                                     | `true` runs the tool open, with nobody signed in |
| `Authorization:RoleClaimType`    | No                              | `roles`                               | The claim the provider puts its groups or roles in |
| `Authorization:Roles:Administrator` | No                           | —                                     | Claim values that make an operator an Administrator, comma-separated |
| `Authorization:Roles:Updater`    | No                              | —                                     | Claim values that make an operator an Updater, comma-separated |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No                       | —                                     | Sends the log to Application Insights — see [Logging and hosting](#logging-and-hosting) |

As environment variables, replace each `:` with a double underscore:
`ConnectionStrings__Memoria`, `Database__Provider`, `Extensions__Directory`,
`Authentication__Oidc__ClientSecret`, `Authorization__Roles__Administrator`.

## When it is not configured

A tool told nothing about its store, or nothing about how operators sign in, does not serve its
pages. It answers every address with one page instead — at status 503, so a health check or a
monitor reads it as a deployment that is not up — saying which settings would have let it start
and linking back here. Nothing else is mapped while it does: not a page, not the upload form. The
same is said in the log, as an error, for whoever is looking at the host rather than the browser.
The messages quoted below are what that page and that log say.

## The connection string

```json
{
  "ConnectionStrings": {
    "Memoria": "Host=localhost;Port=5432;Database=memoria_samples;Username=postgres;Password=password"
  }
}
```

Without it, the tool answers only [the page that says so](#when-it-is-not-configured):

> Connection string 'Memoria' is not configured in appsettings.json.

The tool opens a store somebody else created. It creates nothing — no database, no container, no
table — so the store has to exist and carry the 1.9.0 schema already. See
[Install the store schema](../guides/install-the-store-schema.md).

### Which engine it is

The engine is read off the connection string. Most strings say plainly which one they are for,
because each provider takes keywords the others do not:

| Engine     | Recognised by                                                                            | `Database:Provider` |
| ---------- | ---------------------------------------------------------------------------------------- | ------------------- |
| PostgreSQL | `Host=`, `Port=`, `Username=`, `SslMode=`, …                                             | `Npgsql`            |
| SQL Server | `Initial Catalog=`, `Trusted_Connection=`, `(localdb)`, `tcp:`, `.database.windows.net`   | `SqlServer`         |
| SQLite     | `Data Source=` naming a `.db`/`.sqlite` file, `Mode=`, `Cache=`                           | `Sqlite`            |
| Cosmos DB  | `AccountEndpoint=`, `AccountKey=`                                                        | `Cosmos`            |

Keywords all of them take — `Database`, `Server`, `User Id`, `Password` — settle nothing and are
ignored for this purpose.

Set `Database:Provider` when the string carries signals for more than one engine, or for none. The
tool refuses to guess in either case, and says which it met:

> The provider for connection string 'Memoria' could not be read off it: it carries keywords for
> more than one provider. Set Database:Provider to Npgsql, SqlServer, Sqlite or Cosmos.

The setting is not checked against the string. It is the way out of a string the tool cannot read, so
second-guessing it would close the door it opens. The names are matched case-insensitively and
without spaces, hyphens or underscores, so `SQL Server`, `sql_server` and `sqlserver` are one answer;
`postgres`, `postgresql` and `npgsql` are another; `cosmos`, `cosmosdb` and `azurecosmosdb` a third.

Which provider was chosen is logged at start-up, because a store that answers nothing is the first
thing anyone suspects the connection string of:

```
info: Memoria.Web[0]  Store opened with Npgsql.
```

### In-memory SQLite is refused

```
Data Source=:memory:
```

is rejected at start-up rather than opening an empty store. Such a database lives only as long as the
connection that opened it, and the tool opens a connection per unit of work, so it would find nothing
whatever was seeded. Point it at a file.

## Cosmos DB

A Cosmos connection string names an account and nothing more, so where the documents are is asked for
separately:

```json
{
  "ConnectionStrings": {
    "Memoria": "AccountEndpoint=https://localhost:8081/;AccountKey=<key>"
  },
  "Database": {
    "Cosmos": {
      "DatabaseName": "memoria_samples",
      "ContainerName": "Domain"
    }
  }
}
```

Both default to what `CosmosOptions` itself defaults to — `Memoria` and `Domain` — so an account
installed under those names needs neither setting. Set them to the same values the application that
wrote the store uses, or the tool opens a container nothing has written to.

The client is built in `Gateway` connection mode. The tool asks most of its questions across
partitions, and gateway mode is the one that works from wherever an operator happens to be running
it, including from behind a corporate proxy.

A Cosmos store carries the streamed model only. The site is laid out for that model alone and the
DCB addresses answer 404 — see [what each store answers](memoria-web.md#what-each-store-answers).

## Signing operators in

Operators sign in through an OpenID Connect provider, and the tool answers nothing but
[the page saying so](#when-it-is-not-configured) until it is told which one — or told, in so many
words, to run open. There is no default. The settings page
takes an assembly and runs it, so "nobody said" cannot mean "anybody may".

```json
{
  "Authentication": {
    "Oidc": {
      "Authority": "https://login.example.com/realms/memoria",
      "ClientId": "memoria-web",
      "ClientSecret": "<from the provider>"
    }
  }
}
```

`Authority` is the issuer: the address the provider's discovery document is read from, at
`<Authority>/.well-known/openid-configuration`. Any provider that publishes one will do — Microsoft
Entra ID, Amazon Cognito, Google, Auth0, Okta, Keycloak, Zitadel, Authentik — and the tool never
learns which. Whoever deploys it chooses the provider, and with it the cloud, rather than the tool
choosing for them.

`ClientId` and `ClientSecret` are what the provider issued when the tool was registered there as a
confidential web client. The secret is a secret: put it in an environment variable
(`Authentication__Oidc__ClientSecret`) or the host's secret store, not in the file.

`Scopes` is what the sign-in asks the provider for, space-separated. The default asks for the
identity, the name to show, and the email address. Add whatever scope your provider puts its groups
or roles under when the next release starts reading them.

What the tool does with these: the authorization code flow with PKCE, tokens exchanged on the back
channel and never handed to the browser, a session cookie that lasts as long as the identity the
provider issued. The provider has to be told where to send the operator back to — see
[Deployment](memoria-web-deployment.md#signing-operators-in) for the address to register.

A setting missing from the three is refused by name:

> Authentication:Oidc:ClientSecret is not configured. The provider needs
> Authentication:Oidc:Authority, Authentication:Oidc:ClientId and Authentication:Oidc:ClientSecret
> all set.

Which provider was chosen is logged at start-up, next to which store:

```
info: Memoria.Web[0]  Operators sign in through https://login.example.com/realms/memoria.
```

### Roles

Signed in, an operator holds one of three roles, each including the one before it:

| Role            | May                                                                        |
| --------------- | -------------------------------------------------------------------------- |
| Reader          | Read every page                                                            |
| Updater         | Also press **Update** on a model's detail page, which writes a snapshot    |
| Administrator   | Also install, remove and reread uploaded assemblies on the Settings page — running code on the host |

Every signed-in operator is a Reader. The other two are granted by mapping the values of a claim
the provider sends:

```json
{
  "Authorization": {
    "RoleClaimType": "roles",
    "Roles": {
      "Administrator": "memoria-admins",
      "Updater": "memoria-updaters, memoria-support"
    }
  }
}
```

`RoleClaimType` names the claim the provider puts its groups or roles in. Every provider does this
differently — Entra ID sends app roles under `roles` and group ids under `groups`, Cognito sends
`cognito:groups`, Keycloak sends realm roles nested under `realm_access` unless a mapper flattens
them into a claim of their own — so the tool asks rather than guesses. The default is `roles`.

Each of the two lists is comma-separated, so it fits in one environment variable:

```bash
Authorization__RoleClaimType=cognito:groups
Authorization__Roles__Administrator=memoria-admins
```

An operator whose claim carries a mapped value holds that role; one whose claims match nothing is
a Reader. A group the provider happens to call `Administrator` grants nothing until it is mapped
here. A Reader does not see the Settings link at all, and on a model's detail page sees the
**Update** tab but, in place of the button, a note saying the tab needs the Updater role. An
operator who types an address they may not use is told which role it needed and where they were
going, on a page that says so. The log lines at start-up say what was mapped:

```
info: Memoria.Web[0]  Roles are read off the roles claim: Administrator for memoria-admins, Updater for memoria-updaters, memoria-support.
```

With no `Authorization` section at all, every signed-in operator is a Reader — nobody can update a
snapshot or use Settings — and start-up says so:

```
info: Memoria.Web[0]  No roles are mapped: every signed-in operator is a Reader, and nobody can update
      a snapshot or use Settings. Set Authorization:Roles:Administrator and Authorization:Roles:Updater
      to the claim values that grant them.
```

Running open, roles do not apply: there is nobody to hold one, and every page and button answers.

### Running open

```json
{
  "Authentication": {
    "Disabled": true
  }
}
```

runs the tool with nobody signed in and every page answering anyone who can reach it — including
the upload form. It is how the repository's `appsettings.Development.json` runs `dotnet run` on
localhost, and it is a choice that has to be written down: the tool told neither this nor a provider
refuses,

> Authentication is not configured. Set Authentication:Oidc:Authority, Authentication:Oidc:ClientId
> and Authentication:Oidc:ClientSecret to sign operators in through an OpenID Connect provider.

and a tool told both refuses too, rather than guessing which was meant. The refusal names the
provider settings and not this flag, on purpose: it is shown to whoever asks the tool, and the way
to run it open is written here rather than advertised there. While open, every start-up
says so:

```
warn: Memoria.Web[0]  Running open: nobody is signed in and every page, including the upload form,
      answers anyone who can reach it, because Authentication:Disabled is true.
```

## Extensions

Uploaded archives and the assemblies taken out of them are kept on disk:

```
<Extensions:Directory>/
  zips/    the archives, exactly as uploaded
  lib/     the assemblies, one per name, loaded at start-up
```

The default is `App_Data/extensions` under the content root. Point `Extensions:Directory` somewhere
else to give an instance its own uploads — a scratch directory for a second instance, or a mounted
volume so a container keeps what was uploaded across restarts:

```bash
Extensions__Directory=/var/lib/memoria-web/extensions dotnet Memoria.Web.dll
```

Two behaviours worth knowing:

- **Assemblies are stored by file name alone.** An entry at `bin/Release/Contoso.dll` and one at
  `../../Contoso.dll` both land on `lib/Contoso.dll`, which is what stops a crafted archive writing
  outside the store. An assembly of the same name from an earlier upload is replaced.
- **Removing an archive re-extracts every other one.** Which assembly came from which archive is not
  recorded, so `lib/` is emptied and the archives that remain are unpacked into it again. That keeps
  two archives carrying the same assembly correct — and it means deleting one archive can bring back
  an assembly you had removed by hand.

Everything in `lib/` is loaded at start-up, so what was uploaded survives a restart as long as the
directory does.

### What to put in a zip

Three things: a manifest, the assemblies holding your domain types, and any dependency of theirs
that the tool does not already carry — a validation library, say. The loader resolves those from
`lib/`.

**The manifest is required.** A file called `memoria.json` at the root of the archive — not in a
folder — declaring the services the zip brings. A zip without one is refused, and so is one whose
manifest breaks a rule below; the Settings page says which.

```json
{
  "services": [
    {
      "name": "orders",
      "assemblies": ["Contoso.Orders.Domain.dll", "Contoso.Orders.Contracts.dll"],
      "connectionString": "Orders",
      "roles": {
        "read": ["orders-team"],
        "update": ["orders-leads"]
      }
    }
  ]
}
```

| Key | Required | What it is |
| --- | --- | --- |
| `services` | Yes, at least one | The services the archive declares. One archive may carry several |
| `name` | Yes | The service's name: letters, digits and hyphens, matched without regard to case, unique across every installed archive. It is the address the service is browsed under |
| `assemblies` | Yes, at least one | The assembly files the service's domain types are read from, by file name. Each must be in the zip. **Only these are scanned**; every other assembly in the zip is loaded as a dependency and registers nothing, whatever it carries |
| `connectionString` | Yes | The **name** of an entry under `ConnectionStrings` in the tool's configuration — never the string itself, which stays with the deployment. Not checked at upload, since the configuration may be filled in afterwards; the archive's sheet on the Settings page says whether it is configured and which engine opens it |
| `roles` | No | `read` and `update` are lists of claim values, read from the claim `Authorization:RoleClaimType` names, the same way the values under `Authorization:Roles:*` are. Update includes read. Absent, only the [global roles](#roles) reach the service |

Keys the manifest carries that the tool does not read are ignored, so a later version may add to the
shape without an older tool refusing what it wrote.

An archive already in the directory without a manifest — from before one was required — stays
listed, marked **No manifest**, registers nothing, and its sheet says why. Add a manifest to the zip
and upload it again.

**Never include a `Memoria*` assembly.** Uploaded types must bind to the ones the process already
loaded, or nothing they declare satisfies `IEvent` or `IAggregateRoot`. An assembly compiled against
a different Memoria version loads and then fails to yield types at all; the Settings page reports it:

> Contoso.Domain.dll: Could not load file or assembly 'Memoria, Version=…'

Rebuild against the version the tool was built from and upload again.

## Logging and hosting

Standard ASP.NET Core settings apply. The defaults in `appsettings.json` are:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Two loggers of the tool's own are worth raising or quieting by name:
`Memoria.Web.Settings` (uploads, removals and refreshes) and `Memoria.Web.Streamed` /
`Memoria.Web.Dcb` (snapshot refreshes).

### What each write logs

Every write an operator can make is logged under an event of its own, with a fixed id and name,
so it can be found by the name rather than by its wording:

| Event                  | Id   | Level       | When                                                        |
| ---------------------- | ---- | ----------- | ----------------------------------------------------------- |
| `ExtensionInstalled`   | 1001 | Information | A zip was uploaded and unpacked                             |
| `ExtensionNotInstalled`| 1002 | Error       | An upload could not be unpacked; carries the exception      |
| `ExtensionRemoved`     | 1003 | Information | A zip and its assemblies were deleted                       |
| `ExtensionNotRemoved`  | 1004 | Error       | A removal failed; carries the exception                     |
| `ExtensionsReread`     | 1005 | Information | **Refresh** was pressed on the Types tab                    |
| `SnapshotRefreshed`    | 1011 | Information | **Update** wrote a snapshot                                  |
| `SnapshotUpToDate`     | 1012 | Information | **Update** found no snapshot and no events to fold           |
| `SnapshotNotRefreshed` | 1013 | Warning     | The store refused the update; carries its reason            |
| `TypesRegistered`      | 1021 | Information | What a reload of the extensions came back with, after each of the above and at start-up |
| `ExtensionProblem`     | 1022 | Warning     | One assembly a reload could not read                        |
| `TelemetrySent`        | 1031 | Information | At start-up: the log is exported to Application Insights     |
| `TelemetryKept`        | 1032 | Information | At start-up: it is not, and which setting would make it so   |

Each line names the operator who asked, as the name the provider showed and the subject it keys
them by, and says what it was about: the file, or the model and the instance — a streamed model by
its stream and id, a DCB model by its identifier's type and the values it was built from.

The operator is three columns: `Operator` is the two together as the wording says them, and
`OperatorName` and `OperatorSubject` are each apart, so everything one subject did can be asked for
without matching text, and is still found after a rename. Running open, `Operator` says
`nobody (running open)` and the other two are empty.

### Application Insights

Set `APPLICATIONINSIGHTS_CONNECTION_STRING` — the setting App Service sets when Application
Insights is connected to it, so a deployment there has it already — and every line above is
exported through OpenTelemetry to that resource, along with the request it was written in. In the
portal, each is a row in the `traces` table: the wording in `message`, and the named values —
`FileName`, `Model`, `Instance`, `Operator`, `OperatorName`, `OperatorSubject`, `Error` — with the
event's `EventId` and `EventName` in `customDimensions`, so a query filters on the name rather than
the wording:

```kusto
traces
| where customDimensions.EventName in ("SnapshotRefreshed", "ExtensionInstalled", "ExtensionRemoved", "ExtensionsReread")
| project timestamp,
    event    = tostring(customDimensions.EventName),
    subject  = tostring(customDimensions.OperatorSubject),
    operator = tostring(customDimensions.OperatorName),
    model    = tostring(customDimensions.Model),
    instance = tostring(customDimensions.Instance),
    file     = tostring(customDimensions.FileName)
| order by timestamp desc
```

Everything one person did is `| where subject == "3f1c…"`, whatever the provider showed as their
name at the time.

The requests themselves, in the `requests` table, carry the operator too: the subject as
`user_AuthenticatedId`, which the portal's own views filter and chart by, and `OperatorName` and
`OperatorSubject` in `customDimensions` under the same names as the write lines. So the request a
write was made in, and every page the same person opened, answer to the same clause. A request made
running open carries none of the three.

Left unset, nothing is exported and the start-up log says so. The `Logging` levels above apply to
what is exported as much as to the console, so a logger quieted there is quiet in the portal too.

Addresses come from `ASPNETCORE_URLS`, or from the launch profile in development — see
[Deployment](memoria-web-deployment.md).
