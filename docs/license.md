---
title: Licence
description: "The Memoria framework is free and open source under the Apache License 2.0. StateLens, the separate browser tool that reads a store, is a commercial product with a licence of its own on its site. Which applies to you."
nav_order: 11
redirect_from:
  - /licence.html
  - /licensing.html
---

# Licence

Memoria is two things under two licences, and which one applies depends on which of them you are using.

The **framework** — every package published under the `Memoria` NuGet prefix — is free and open source under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Use it in anything, commercial or not, closed source or open, at any scale, without asking and without paying. There is no edition, no threshold and no key.

**[StateLens](tools/index.md)** — the browser tool that reads a store through your own domain assemblies and a manifest, which began here as Memoria Web — is a separate product, developed in its own private repository, and a commercial one under the [StateLens Licence Agreement](https://statelens.dev/docs/licence) on its own site. Running it is what that licence governs. It is **free for one service**, and paid above that; the editions and their prices are on its [pricing page](https://statelens.dev/pricing).

## Which licence do you need?

| You are…                                                                              | Licence                              | Cost                                                                            |
|---------------------------------------------------------------------------------------|--------------------------------------|---------------------------------------------------------------------------------|
| Building software with the Memoria packages, however you licence what you build        | Apache License 2.0                   | Free                                                                             |
| Running StateLens over a single service                                                | StateLens Licence, **Community**     | Free, always                                                                     |
| Running StateLens over more than one service                                           | StateLens Licence, **Standard**, **Professional** or **Enterprise** | See [statelens.dev/pricing](https://statelens.dev/pricing) |

A **service** is one entry in the `services` list of a `statelens.json` manifest installed in the tool: a named set of domain assemblies read over one connection string, browsed at its own address. StateLens counts them itself, so nothing rests on your own assessment of your revenue, your headcount or your size.

Versions of the framework up to and including 1.9.1 were released under the Apache License 2.0 and remain under it. Version 2.0.0-beta, released on 17/09/2026, was briefly offered under the Reciprocal Public License 1.5 or a commercial licence covering the packages; that arrangement was withdrawn four days later at 2.0.0-beta.2 and the framework returned to Apache 2.0. Anyone who took 2.0.0-beta under either of those licences keeps them — a version is licensed under the terms it was released with — and Apache 2.0 grants strictly more, so there is nothing to do about it.

### Why the framework is free and the tool is not

A framework earns its keep by being adopted, and a licence that sends an evaluating engineer to their legal department before they can prototype is a tax on exactly the thing it needs most. Apache 2.0 removes that entirely and for good.

The tool is a different purchase. It is opened by a team already running event-sourced systems in production, to answer questions — what does this aggregate fold to, is its snapshot behind, which events produced this state — that otherwise cost an afternoon of hand-written queries each. One service covers every evaluation, every side project and most single-context shops, free and permanently. Beyond that, an organisation reading several stores through it is getting the kind of value that pays for the work.

### Where the tool's terms are

The StateLens Licence Agreement, in full, is on the tool's site at [statelens.dev/docs/licence](https://statelens.dev/docs/licence), and its fees on the [pricing page](https://statelens.dev/pricing) there. The agreement this page carried while the tool was Memoria Web, up to 27 September 2026, is superseded by it; a version of the tool released under the earlier terms keeps them, as the agreement itself says.

---

Copyright © Luca Cammarata Briguglia. All rights reserved.

## Related

- [StateLens](tools/index.md) — what the tool is, and where it lives
- [Release notes](release-notes.md)
