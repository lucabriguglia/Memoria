# Memoria.Web

A browser tool for reading a Memoria store. Point it at a database, upload a zip of your own domain
assemblies on the Settings page, and it shows you the events that were appended, the aggregates and
projections snapshotted from them, and the types both were written through.

It is not a sample and not a package: it lives here, and you build and run it yourself.

```bash
dotnet run --project src/Memoria.Web
```

That serves on `http://localhost:5159` in the Development environment. Point it at a store first —
`ConnectionStrings:Memoria` in [`appsettings.json`](appsettings.json) — or fill one with
[Memoria.Web.Samples](../Memoria.Web.Samples), which carries a sample domain in both consistency
models and writes data through it.

> **Use the launch profile.** Started with `--no-launch-profile` the environment is Production, the
> development static-asset handler looks for a bundle that only exists in publish output, and the
> scoped-CSS bundle answers 500 — the application renders unstyled. The same applies to anything run
> out of `bin/` rather than out of `dotnet publish` output.

## Signing in

Operators sign in through an OpenID Connect provider — any that publishes a discovery document —
and nothing answers anyone who has not, form posts included. The tool refuses to start until it is
told which provider, or told in so many words to run open, which is what
[`appsettings.Development.json`](appsettings.Development.json) does for `dotnet run` on localhost.

Signed in, an operator is a Reader, an Updater or an Administrator, each including the one before:
read every page; also press **Update**; also upload a `.dll` that this process will load and
execute. Every operator is a Reader until a claim the provider sends is mapped to one of the other
two, so map Administrator only to the people you would give shell access on the host to. See
[Configuration](https://lucabriguglia.github.io/Memoria/tools/memoria-web-configuration.html#signing-operators-in)
for the settings and
[Deployment](https://lucabriguglia.github.io/Memoria/tools/memoria-web-deployment.html#signing-operators-in)
for what to register at the provider.

## Configuration

| Setting                            | Required                        | Default                              |
| ---------------------------------- | ------------------------------- | ------------------------------------ |
| `ConnectionStrings:Memoria`        | Yes                             | —                                    |
| `Database:Provider`                | Only when the string is unclear | Read off the connection string       |
| `Database:Cosmos:DatabaseName`     | No                              | `Memoria`                            |
| `Database:Cosmos:ContainerName`    | No                              | `Domain`                             |
| `Extensions:Directory`             | No                              | `<content root>/App_Data/extensions` |
| `Authentication:Oidc:Authority`    | Unless running open             | —                                    |
| `Authentication:Oidc:ClientId`     | Unless running open             | —                                    |
| `Authentication:Oidc:ClientSecret` | Unless running open             | —                                    |
| `Authentication:Oidc:Scopes`       | No                              | `openid profile email`               |
| `Authentication:Disabled`          | Unless signing in               | —                                    |
| `Authorization:RoleClaimType`      | No                              | `roles`                              |
| `Authorization:Roles:Administrator` | No                             | —                                    |
| `Authorization:Roles:Updater`      | No                              | —                                    |

PostgreSQL, SQL Server and SQLite are read through Entity Framework Core and carry both consistency
models. Cosmos DB is read through its own SDK and carries the streamed model only — there is no
Cosmos store for dynamic consistency boundaries, so those pages are neither linked nor found.

## What it writes

One thing: **Update** on a model's detail page refreshes its stored snapshot, applying the events it
has not applied yet. Nothing is appended, nothing is deleted, and no schema is created — the store
has to exist already.

## Where things are

| Path             | What is in it                                                            |
| ---------------- | ------------------------------------------------------------------------ |
| `Components/`    | The pages: `Streamed/`, `Dcb/`, settings, and the layout around them      |
| `Data/`          | Reading the connection string, and wiring whichever store it named        |
| `Extensibility/` | Uploads, assembly loading, type scanning, and the queries the pages ask   |
| `Endpoints/`     | The handful of form posts the statically rendered pages send              |
| `Security/`      | Reading how operators sign in, and wiring the provider, the policy and sign-out |
| `App_Data/`      | Uploaded archives and the assemblies taken out of them (local state)      |

## Documentation

- [Memoria Web](https://lucabriguglia.github.io/Memoria/tools/memoria-web.html) — what it is and how to use it
- [Configuration](https://lucabriguglia.github.io/Memoria/tools/memoria-web-configuration.html)
- [Deployment](https://lucabriguglia.github.io/Memoria/tools/memoria-web-deployment.html)
- [Try it with sample data](https://lucabriguglia.github.io/Memoria/tools/memoria-web-samples.html)
