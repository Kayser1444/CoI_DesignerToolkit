# 05 — Add the AoE removal tool, dialog, and quick action

**What to build:** Add the single AoE removal tool for the complete supported entity set. A whole-entity area selection opens a vanilla-AoE-upgrade-style dialog with entity-type filters and paired regular/quick removal actions; this ticket implements the shared dialog and quick-removal path together with #06.

**Blocked by:** #04 — Add explicit modded-entity adapter support.

**Status:** implemented with #06

- [x] One AoE removal tool appears in the toolbar with the vanilla trash icon treatment and one configurable default hotkey.
- [x] Rectangle selection includes entities whose footprints intersect the area and normalizes partial transports to their whole original Transport, merging and deduplicating by stable entity ID.
- [x] Sources and sinks are excluded, and only entities with validated regular and quick capabilities are targetable.
- [x] Completing a selection opens one dialog modeled on vanilla AoE upgrade; selection itself performs no removal.
- [x] The selected ID set is fixed at drag release while quantities, target validity, total cost, and affordability remain live in the dialog.
- [x] The dialog provides per-entity-type toggles, enabled by default for selected types, which filter counts, products, cost, and both removal actions without mutating simulation state.
- [x] The dialog presents paired regular/quick action controls using the same icon and Unity-cost language as entity inspectors.
- [x] The quick action schedules one serializable batch input command for enabled target IDs; the simulation thread re-resolves IDs, revalidates adapters, recomputes per-entity costs, and performs the final affordability check.
- [x] If the total is unaffordable, no regular order is cancelled, no product is cleared, and no Unity is consumed.
- [x] With sandbox Ignore lack of Unity enabled, the quick action follows vanilla CanConsume and ConsumeExactly behavior and leaves the balance at zero when needed.
- [x] Demolished or invalid entities are silently omitted when the action executes; unforeseen per-entity failures are logged, do not roll back independent successes, and charge only successful removals.
- [x] The manual integration test script covers partial-transport normalization, deduplication, type filtering, fixed selection, mixed entity types, live invalidation, per-entity rounding, atomic affordability, sandbox behavior, and independent failure handling.

## Implementation note

Implemented in the shared AoE slice with #06. The runtime seams are `TransportProductRemovalAoETool`, `TransportProductRemovalAoEWindow`, `TransportProductRemovalBatchCmd`, and `TransportProductRemovalBatchCommandsProcessor`. The combined manual coverage is in `../05-06-integration-test-script.md`; execution remains pending. Build verification passes with zero warnings and zero errors.
