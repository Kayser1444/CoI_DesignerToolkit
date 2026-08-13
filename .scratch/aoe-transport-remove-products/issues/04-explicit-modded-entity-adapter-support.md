# 04 — Add explicit modded-entity adapter support

**What to build:** Provide a guarded adapter registration seam for compatible modded entities. A modded entity is eligible only when an explicit adapter safely supports both removal modes and the combined entity-level inspector interaction; unsupported or invalid registrations fail closed.

**Blocked by:** #03 — Complete built-in entity-level removal coverage.

**Status:** ready-for-agent

- [ ] Modded adapters register against a stable prototype identity and are created for the current world through an explicit factory.
- [ ] The adapter contract covers complete product enumeration, validity and eligibility, normal-port gating, regular clearing buffers, vanilla-equivalent quick cost, vanilla product accounting, and idempotent cleanup.
- [ ] The adapter contract includes a safe insertion point for the combined regular/quick inspector control.
- [ ] Registration and preflight require both regular and quick support; entities supporting only one mode are excluded from both removal tools.
- [ ] Registration failures, unsupported prototypes, and adapter failures leave entities ungated and unpersisted, and write diagnostics containing entity ID, prototype ID, and adapter kind.
- [ ] Fake-adapter tests verify successful registration, both removal modes, inspector eligibility, malformed registration rejection, cleanup, and safe exclusion.
- [ ] The documented contract clearly identifies the behaviors that require validation against a real third-party mod; no untestable mod-specific assumptions are encoded.

## Comments

- 2026-08-13: Implemented the prototype-keyed `TransportProductRemovalAdapterRegistry`, disposable runtime registrations, complete regular/quick/gating adapter contract, validation and fail-closed diagnostics, external quick-command handling, and the public `TransportProductRemovalUi.AddCombinedRemovalControl(...)` inspector seam. Built-in adapters remain authoritative and are checked before external registrations. `dotnet build DesignerToolkit.sln -c Debug` passes with zero warnings. Runtime validation against a real third-party entity remains outstanding; this workspace has no game integration-test harness.
