# Read Modes

When you load an aggregate with `IDomainService.GetAggregate`, you choose how it should be reconstructed. Memoria offers four read modes that trade off freshness against I/O and reconstruction cost.

## The four modes

| ReadMode                          | Reads snapshot | Applies new events after the snapshot | Reconstructs from events if no snapshot | Returns null when…                           |
|-----------------------------------|:--------------:|:-------------------------------------:|:---------------------------------------:|----------------------------------------------|
| `SnapshotOnly` *(default)*        |      yes       |                  no                   |                   no                    | no snapshot exists                           |
| `SnapshotWithNewEvents`           |      yes       |                  yes                  |                   no                    | no snapshot exists                           |
| `SnapshotOrCreate`                |      yes       |                  no                   |                   yes                   | no snapshot and no events                    |
| `SnapshotWithNewEventsOrCreate`   |      yes       |                  yes                  |                   yes                   | no snapshot and no events                    |

## Which one should I pick?

Walk down the questions:

1. **Are you reading right after a write that already snapshotted?** Use `SnapshotOnly`. Fastest path; one record fetched.
2. **Could other writers have appended events since your last snapshot?** Use `SnapshotWithNewEvents`. Reads snapshot + the tail of events to apply.
3. **Are you bootstrapping or replaying after a schema change where the snapshot may not exist yet?** Use `SnapshotOrCreate`. If a snapshot is missing, the aggregate is reconstructed from events and a fresh snapshot is stored.
4. **All of the above at once?** Use `SnapshotWithNewEventsOrCreate`. Safe default for "I don't care how it got here, just give me the latest aggregate state."

## Behaviour when no snapshot exists

`SnapshotOrCreate` and `SnapshotWithNewEventsOrCreate` will write a snapshot automatically the first time they reconstruct an aggregate from events. This is the supported way to introduce a new aggregate type, or to migrate to a changed aggregate structure — increase the aggregate version to force snapshot recreation.

`SnapshotOnly` and `SnapshotWithNewEvents` never write — they return null if the snapshot is missing, even when applicable events exist in the stream.

## Related

- [Domain Service](../reference/domain-service.md) — full `GetAggregate` API
- [Aggregates and Streams](aggregates-and-streams.md) — what gets reconstructed
