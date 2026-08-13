# 03 — Complete built-in entity-level removal coverage

**What to build:** Extend the shared removal domain to lifts, mini-zippers, and sorters so the supported non-Transport entity set has matching regular and quick behavior and one vanilla-style combined inspector interaction. Native Transport removal remains delegated to vanilla.

**Blocked by:** #02 — Persist BDT regular-removal orders across saves.

**Status:** ready-for-agent

- [ ] Lifts, mini-zippers, and sorters support regular removal across all relevant internal buffers with the same one-shot, gating, truck-job, cancellation, completion, and persistence semantics as the Zipper slice.
- [ ] Lifts, mini-zippers, and sorters support quick removal across all relevant buffers with vanilla-equivalent cost and product accounting.
- [ ] Existing standalone quick-removal controls are replaced by one combined regular/quick control for each supported non-Transport inspector.
- [ ] The combined control reflects active regular state, current removable contents, quick cost, and affordability without duplicating controls or adding persistent world-space markers.
- [ ] Native Transport inspector and command state remain untouched and authoritative; BDT does not create persisted regular-order entries for Transports.
- [ ] Partial entity shapes and entities that cannot be safely adapted are excluded without leaving gates or stale persisted state.
- [ ] Integration tests verify every built-in adapter drains pending/input/output buffers while preserving queue order and cached quantities.
- [ ] Tests verify that each supported inspector contributes exactly one combined removal interaction.

## Comments

- 2026-08-12: Implemented explicit Lift, MiniZipper, and Sorter buffer adapters, shared regular-removal lifecycle/persistence, simulation input/output gating, native quick-removal supersession, and combined inspector controls. `dotnet build DesignerToolkit.sln -c Debug` passes with zero warnings. Runtime validation remains required because this repository has no game integration-test harness.
- 2026-08-12: Runtime follow-up found that skipped Lift simulation left its previous animation running; gated Lift updates now explicitly pause the animation provider. Fertilizer II instant removal on pipe entities was verified against vanilla `Transport.RequestProductsRemoval`: discardable non-waste products are intentionally cleared before considering truck loadability. A reported loose-product pickup delay did not reproduce; trace confirmed successful output-buffer registration and subsequent truck pickup.
