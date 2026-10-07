---
title: Tools
description: "StateLens, the browser tool that reads an event sourced store through your own domain assemblies and a manifest, is a separate product with a site of its own. What it is and where it lives."
nav_order: 6
redirect_from:
  - /tools/memoria-web.html
  - /tools/memoria-web-configuration.html
  - /tools/memoria-web-deployment.html
  - /tools/memoria-web-samples.html
---

# Tools

## StateLens

[StateLens](https://statelens.dev/) is a browser tool for reading an event sourced store: the
events appended to it, the aggregates and projections snapshotted from them, and the types both
were written through. Point it at a store, upload a zip of your own domain assemblies with a
`statelens.json` at its root, and it folds an aggregate from its events so you can see the state a
snapshot stands at and how far behind its history it is. It creates nothing and deletes nothing.

It began in this repository, as Memoria Web, a reader of Memoria stores. It reads any framework's
store now: a service's manifest tells the tool which of the uploaded types are the streams, events,
aggregates and projections, how they are named and folded, and where in the store the events and
snapshots are. Memoria's own stores are described the same way as anyone else's; the tool knows no
framework by name.

It lives in its own private repository, and everything about it is on its site:

- [What it shows](https://statelens.dev/docs/statelens), [configuration](https://statelens.dev/docs/configuration),
  [deployment](https://statelens.dev/docs/deployment) and [the sample data](https://statelens.dev/docs/samples)
- [Pricing](https://statelens.dev/pricing): a commercial product, separate from the framework, free
  over a single service and paid above that, under the [StateLens Licence](https://statelens.dev/docs/licence)
- A hosted instance at [demo.statelens.dev](https://demo.statelens.dev), behind its sign-in, so access
  is by invitation: ask through the site's [contact form](https://statelens.dev/contact)

The framework packages are Apache 2.0 regardless: nothing in them depends on the tool, and nothing
in the tool changes their licence.

## Related

- [Licence](../license.md) — Apache 2.0 for the framework, and where the tool's terms are
- [Release notes](../release-notes.md) — the versions of the framework the tool was developed alongside
