---
title: Upgrade to 2.0.0
description: "The licence is unchanged, where a RabbitMQ connection failure now surfaces, and the optional type bindings a store can carry."
parent: Upgrading
nav_order: 1
---

# Upgrade to 2.0.0
{: .no_toc }

## On this page
{: .no_toc .text-delta }

1. TOC
{:toc}

The major version steps to 2 for two breaking changes below, not for the licence and not for the
shape of the API. No table, document or serialised payload changes, so there is no data migration.

<a name="choose-a-licence"></a>
## 1. The licence: nothing to do

**The framework is under the Apache License 2.0, as it was throughout 1.x.** There is no decision to
make, nothing to assess and nothing to sign. Build what you like with it and licence that however
you like.

If you read this page between 17 and 21 September 2026, it said something else. Version 2.0.0-beta
was published offering the packages under either the Reciprocal Public License 1.5 or a commercial
licence. That was withdrawn four days later at 2.0.0-beta.2, and the framework returned to Apache
2.0 for good. A version is licensed under the terms it shipped with, so anyone who took 2.0.0-beta
under the earlier terms keeps them — and Apache 2.0 grants strictly more than either of them did, so
upgrading needs no action from anybody.

The packages went back with it: an SPDX `Apache-2.0` expression in place of the packed `LICENSE.md`,
and no licence acceptance prompt on install.

**[The tool](../tools/index.md) is the exception**, and always was a separate question. Memoria
Web then, [StateLens](https://statelens.dev/) now, it is a commercial product developed in its own
repository, free over a single service and [paid above that](https://statelens.dev/pricing). If you
only use the packages, none of that reaches you.

<a name="rabbitmq"></a>
## 2. If you publish to RabbitMQ, a connection failure now surfaces later

`Memoria.Messaging.RabbitMq` moves to RabbitMQ.Client 7, whose API is asynchronous throughout. A
constructor cannot await, so **the provider opens its connection on the first send rather than when
it is constructed**.

The practical difference is where a bad connection string shows up:

| | 1.9.x | 2.0.0 |
|---|---|---|
| Broker unreachable | Throws where the provider is resolved, typically at start-up | Returned as that send's `Failure`, like any other send error |

If you were relying on the old timing to fail fast at start-up — a health check that resolved
`IMessagingProvider`, or a smoke test that expected the host to refuse to start against a bad broker
— it no longer fails there. The failure is real either way; it arrives at the send instead. Check
the `MessageResults` on the `SendAndPublish` response, which you should be doing regardless:

```csharp
var response = await dispatcher.SendAndPublish(new PlaceOrder(orderId, amount));

if (response.MessageResults.Any(m => m.IsNotSuccess))
{
    // the command succeeded; the message did not reach the broker
}
```

A custom `IConnection` handed to the provider through its second constructor is used exactly as
before.

<a name="redis"></a>
## 3. If you cache in Redis, nothing to do

`Memoria.Caching.Redis` moves to StackExchange.Redis 3, which speaks RESP3 to a server that offers
it and RESP2 to one that does not. Nothing in the provider's use of the client changes, and nothing
in yours has to.

<a name="type-bindings"></a>
## 4. Optional: give a store its own type bindings

Until now, event and aggregate keys — `OrderPlaced:1`, `Order:1` — resolved to CLR types through one
set of bindings per process, the static maps on `TypeBindings` and `DcbTypeBindings`. A host reading
two bounded contexts that both declare an `OrderPlaced` at version 1 could not bind both.

Those maps now live on `TypeBindingSet`, and the statics are views over the process-wide
`TypeBindingSet.Default`. **An application that never mentions `TypeBindingSet` binds exactly as it
did before** — this is an addition, not a change. Reach for it only when one process genuinely has to
read more than one context's store.

## Related

- [Licence](../license.md) — Apache 2.0 for the framework, and where the tool's terms are
- [Release notes](../release-notes.md) — everything in 2.0.0-beta, not only what needs action
- [Publish to RabbitMQ](publish-to-rabbitmq.md)
- [StateLens](../tools/index.md) — the separate tool, and where it went
