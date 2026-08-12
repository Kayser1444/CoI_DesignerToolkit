# 05 — Add AoE quick remove

**What to build:** Add the AoE quick-removal tool for the complete supported entity set, with vanilla-style toolbar and hotkey integration, whole-entity selection, live preview, and an atomic simulation-thread affordability decision.

**Blocked by:** #04 — Add explicit modded-entity adapter support.

**Status:** ready-for-agent

- [ ] The quick-removal tool appears adjacent to the regular-removal tool with the agreed trash icon treatment and configurable default hotkey.
- [ ] Rectangle selection includes entities whose footprints intersect the area and normalizes partial transports to their whole original Transport, merging and deduplicating by stable entity ID.
- [ ] Sources and sinks are excluded, and only entities with validated regular and quick capabilities are targetable.
- [ ] The selected ID set is fixed at drag release while quantities, target validity, total cost, and affordability remain live during confirmation.
- [ ] Confirmation schedules one serializable batch input command; the simulation thread re-resolves IDs, revalidates adapters, recomputes per-entity costs, and performs the final affordability check.
- [ ] If the total is unaffordable, no regular order is cancelled, no product is cleared, and no Unity is consumed.
- [ ] With sandbox Ignore lack of Unity enabled, confirmation follows vanilla CanConsume and ConsumeExactly behavior and leaves the balance at zero when needed.
- [ ] Demolished or invalid entities are silently omitted at confirmation; unforeseen per-entity failures are logged, do not roll back independent successes, and charge only successful removals.
- [ ] Tests cover partial-transport normalization, deduplication, mixed entity types, live invalidation, per-entity rounding, atomic affordability, sandbox behavior, and independent failure handling.

