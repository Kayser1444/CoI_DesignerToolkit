# 06 — Add regular removal to the AoE removal dialog

**What to build:** Enable explicit regular-removal and cancellation actions in the shared AoE removal dialog. They use the dialog's fixed selection and entity-type filters while preserving native Transport semantics.

**Blocked by:** #05 — Add the AoE removal tool, dialog, and quick action.

**Status:** implemented with #05

- [x] No second toolbar entry, hotkey, selection controller, or dialog is added; regular removal uses the tool and dialog from #05.
- [x] **Remove** uses the same enabled entity-type filters and current target set shown by the shared dialog.
- [x] Clicking **Remove** schedules one serializable batch input command and performs no direct simulation mutation from the UI callback.
- [x] The command processor re-resolves and deduplicates targets, always reissues removal independently from current contents, and never interprets an existing active order as a cancellation.
- [x] **Cancel remove** cancels all active orders among enabled targets and is hidden when none are active.
- [x] For Transports, the command delegates to native removal-in-progress, request, and cancel state; BDT never caches native Transport regular orders.
- [x] For non-native entities, starting an order creates a persisted BDT regular-removal order only when removable work actually exists; cancellation and completion update persisted intent correctly.
- [x] Empty, invalid, unsupported, and adapter-failing entities are silently skipped from the player’s perspective and never remain gated.
- [x] The manual integration test script covers mixed selections, repeated selection of the same entity, native Transport delegation, persistence of newly started orders, cancellation, and no-op targets.

## Implementation note

Implemented in the shared AoE slice with #05. Remove, Cancel remove, and quick removal use the same serialized batch seam with distinct action flags; native `Transport` targets remain authoritative and all other targets use the BDT manager. The combined manual coverage is in `../05-06-integration-test-script.md`; execution remains pending. Build verification passes with zero warnings and zero errors.
