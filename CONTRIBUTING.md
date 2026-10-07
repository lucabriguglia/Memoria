# Contributing to Memoria

Thank you for being here. Memoria is a small project, and a good bug report is worth as much to it
as a pull request.

Everything here assumes you have read the [Code of Conduct](CODE_OF_CONDUCT.md), which applies
everywhere the project has a presence.

## Where to put what

| You want to… | Go to |
|--------------|-------|
| Report a bug | [Open an issue](https://github.com/lucabriguglia/Memoria/issues/new), with the version, the store provider and the smallest code that reproduces it |
| Ask how something works | [Open a discussion](https://github.com/lucabriguglia/Memoria/discussions) — the [documentation](https://lucabriguglia.github.io/Memoria/) may answer it first |
| Suggest a feature | Open an issue describing the problem you hit, before the solution you have in mind |
| Fix a typo or improve the docs | A pull request straight to `docs/` is welcome, no issue needed |
| Change code | **Open an issue first** — see below |
| Report a security problem | Email the maintainer rather than opening a public issue |

## Open an issue before you write code

Please agree the shape of a code change in an issue before you build it. This is not ceremony: it
saves you writing something that does not fit before you have spent your evening on it.

Documentation and typo fixes are the exception — send those directly.

## Nothing to sign

Everything in this repository is under the Apache License 2.0, and section 5 of that licence already
says your contribution comes in under the same terms the project goes out under. Send the pull
request; there is no contributor agreement.

[StateLens](https://statelens.dev/), the browser tool that reads a store, is a separate commercial
product developed in its own private repository. Nothing of it is here, so nothing here is affected
by its licence.

## Building and testing

Memoria targets .NET 10.0. The solution file is `Memoria.slnx`.

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --filter "Category!=Container&Category!=Emulator"
```

That last filter is what CI runs, and it excludes two sets of tests that need something running:

| Category | Needs | What happens without it |
|----------|-------|-------------------------|
| `Container` | Docker, for real SQL Server and PostgreSQL | The tests skip themselves |
| `Emulator` | The Azure Cosmos DB emulator on `https://localhost:8081` | The tests **fail**, they do not skip |

No CI job runs the `Emulator` tests, so they are a local gate. **If you change anything under
`src/Memoria.EventSourcing.Store.Cosmos`, start the emulator and run them before you open the pull
request:**

```bash
dotnet test tests/Memoria.EventSourcing.Store.Cosmos.Tests
```

To run one project or one class:

```bash
dotnet test tests/Memoria.Tests
dotnet test --filter "FullyQualifiedName~CommandResponseTests"
```

## How the code is written

Read the code around what you are changing and follow it. The conventions that are not obvious from
a single file:

- **Nullable reference types and implicit usings are on everywhere.** Do not turn either off for a
  project.
- **Handlers return `Result` and `Result<T>`, not exceptions.** A failure is a value the caller can
  see in the type. Follow the [result pattern](https://lucabriguglia.github.io/Memoria/concepts/result-pattern.html)
  rather than throwing for an outcome a caller should handle.
- **Public types carry XML documentation**, including the `<example>` blocks that the reference docs
  and IntelliSense show.
- **Test projects mirror source projects.** `Memoria.Caching.Memory` is tested by
  `Memoria.Caching.Memory.Tests`; tests within a project live under `Features/` and `Models/`.
- **The test stack is xUnit, [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions)
  and NSubstitute.** Assertions read `result.Should().BeOfType<Success>()`. Do not add another
  assertion or mocking library.
- **A behaviour change comes with a test that would have failed before it.** A bug fix without a
  test that pins the bug is not finished.
- **A provider is a package.** New stores, message buses, caches and validators go in their own
  project behind the existing abstraction rather than into the core.

## Documentation lives with the change

`docs/` is the source of the [documentation site](https://lucabriguglia.github.io/Memoria/). If your
change alters behaviour, a configuration key, or anything the docs describe, update the docs in the
same pull request.

A new page needs front matter, or it will not appear in the sidebar. Copy the shape from a page
beside it:

```yaml
---
title: Tune the Cosmos DB container
parent: Guides
nav_order: 14
---
```

`title` is what the sidebar shows, `parent` is the exact title of the section page it belongs under,
and `nav_order` is its position within that section. A page three levels deep also needs
`grand_parent`. Link to other pages by their path with the `.md` extension — the site rewrites those
to `.html` when it builds.

### Previewing the site

The docs are Jekyll, themed with [Just the Docs](https://just-the-docs.com/) and served by GitHub
Pages. `docs/Gemfile` pins the same `github-pages` gem GitHub itself runs, so what you see locally is
what deploys.

You need Ruby with the DevKit — on Windows, [RubyInstaller](https://rubyinstaller.org/) — then:

```bash
cd docs
bundle install
bundle exec jekyll serve --livereload
```

That serves the site at `http://127.0.0.1:4000/Memoria/`. The trailing `/Memoria/` matters: it is the
`baseurl`, and without it you get a 404.

## Opening the pull request

- Branch from `main` and target `main`.
- Write the commit subject as what the change does, in plain words and without a prefix or a trailing
  full stop — `Publish the aggregate version alongside the event sequence`, not `feat: version`. Look
  at `git log` for the tone.
- Keep the pull request to one change. Two unrelated fixes are two pull requests.
- Say in the description what the change does and why, and link the issue it came from.
- **Do not change the version number** in `Directory.Build.props`. Releases are cut by the
  maintainer.
- CI must be green: restore, build, and the test run above.

## What to expect

This is a project maintained by one person alongside other work, so a response may take a few days.
An issue that goes quiet has not been dismissed. If a pull request is not merged, you will be told
why.
