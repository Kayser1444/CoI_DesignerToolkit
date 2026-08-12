# 06 — Add AoE regular remove

**What to build:** Add the AoE regular-removal tool using the shared area selection and target-validation seams. Releasing a selection schedules independent per-entity toggles, matching the entity-level regular-removal interaction and preserving native Transport semantics.

**Blocked by:** #05 — Add AoE quick remove.

**Status:** ready-for-agent

- [ ] The regular-removal tool appears adjacent to the quick-removal tool with the agreed trash icon treatment and configurable default hotkey.
- [ ] Selection uses the same whole-entity normalization, source/sink exclusion, capability validation, and stable-ID deduplication as AoE quick removal.
- [ ] Selection release schedules one serializable batch input command and performs no direct simulation mutation from the UI callback.
- [ ] The command processor re-resolves and deduplicates targets, then toggles each entity independently; mixed active and inactive selections are supported.
- [ ] For Transports, the command delegates to native removal-in-progress, request, and cancel state; BDT never caches native Transport regular orders.
- [ ] For non-native entities, starting an order creates a persisted BDT regular-removal order only when removable work actually exists; cancellation and completion update persisted intent correctly.
- [ ] Empty, invalid, unsupported, and adapter-failing entities are silently skipped from the player’s perspective and never remain gated.
- [ ] Integration tests cover mixed selections, repeated selection of the same entity, native Transport delegation, persistence of newly started orders, cancellation, and no-op targets.

