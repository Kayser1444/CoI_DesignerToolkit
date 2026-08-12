# 02 — Persist BDT regular-removal orders across saves

**What to build:** Preserve active BDT-owned regular-removal intent across manual saves, autosaves, failed saves, and load cycles. Persist only stable entity and per-product scope data; reconstruct transient jobs, reservations, gates, and clearing buffers at runtime.

**Blocked by:** #01 — Entity-level removal vertical slice for Zippers.

**Status:** ready-for-agent

- [ ] Active BDT regular-removal orders are represented by a versioned persisted state in the vanilla save’s mod JSON configuration.
- [ ] Persisted records contain stable entity identity, stable prototype identity, and remaining per-product removal scope, but no runtime jobs, vehicles, reservations, buffer references, or adapter instances.
- [ ] Save preparation serializes current intent and detaches runtime buffer attachments without completing, cancelling, or ungating active orders.
- [ ] Save completion reattaches only the attachments detached for that save, including after a failed save, and lifecycle hooks are correctly unsubscribed during disposal.
- [ ] Load rebuilds orders from current buffers using the per-product minimum of current quantity and persisted remaining scope; missing quantities are treated as fulfilled and future products are not added.
- [ ] Missing entities are pruned with one info log containing the total and at most the first ten IDs; malformed or unknown-version state fails closed with error diagnostics and no gated entities.
- [ ] Unknown prototypes and adapter failures leave entities ungated and unpersisted, with the agreed error diagnostics.
- [ ] Destruction, transformation, world termination, and mod disposal clean up runtime state without player-facing notifications.
- [ ] Manual-save, autosave, failed-save, load, and reinstall/removal scenarios are covered by integration tests.

