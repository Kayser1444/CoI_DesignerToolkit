# 07 — Remove a specific product

**What to build:** Extend the shared entity-level and AoE transport-removal
workflow so the player can remove one selected buffered product instead of all
products. The AoE dialog adds a **Remove only** product dropdown whose default
is **All products**.

**Blocked by:** #05 and #06 — the unified AoE quick/regular removal dialog.

**Status:** future

- [ ] The AoE dialog offers one live product dropdown populated from the fixed
  selection and currently enabled entity types, defaulting to **All products**.
- [ ] The selected product filters displayed quantity, regular removal, quick
  removal, quick cost, and affordability consistently.
- [ ] The serialized batch command carries an optional stable product ID and
  revalidates it on the simulation thread.
- [ ] Regular removal creates and persists only the selected product scope;
  disappearance before execution is a silent no-op.
- [ ] Quick removal clears and accounts for only the selected product, charging
  the vanilla-equivalent per-entity rounded cost only after successful clearing.
- [ ] Native Transport selective removal preserves queue invariants without
  delegating to vanilla's whole-entity clear operation.
- [ ] Built-in and explicitly registered modded adapters expose safe selective
  quick-removal accounting in addition to their existing regular product seam.
- [ ] Entity-level regular and quick removal can target a specific product using
  the same domain behavior as AoE removal.
- [ ] Tests cover mixed products, disappearing products, unsupported products,
  save/load of filtered regular orders, per-entity rounding, affordability, and
  independent failures.

## Implementation note

This is a domain extension, not only a dropdown. In filtered mode native
Transport and adapter quick removal need selective clearing and quick-accounting
paths; whole-entity mode remains the default and preserves current behavior.
