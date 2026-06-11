# Instant Build Mode Import

Status: implemented (v0.3.0). The instant-build-style construction functionality from Mori's Utilities++ has been absorbed and integrated natively within BDT's architecture.

## Intent

BDT is absorbing the instant-build-style construction functionality from Mori's Utilities++ source while keeping the implementation native to BDT's architecture. The imported files remain under `imports/UtilitiesPlusPlusImport` as reference material only and are excluded from compilation.

## Integration Principles

- Keep BDT removable from saves. Runtime subscriptions must be non-saveable, and per-save BDT preferences must live in the existing `dtkSettingsStateJson` config-backed state.
- Prefer typed game APIs over private reflection where current Captain of Industry APIs allow it.
- Use BDT naming, logging, localization, settings UI, and documentation patterns.
- Integrate one feature slice at a time. Instant build mode came first, followed by area upgrade/downgrade and transport cleanup.

## First Slice: Instant Build Mode

The first implemented slice should:

- Add a per-save BDT setting for instant build mode.
- Expose the setting in the BDT settings UI.
- Subscribe to `ISimLoopEvents.UpdateAfterCmdProc` with `AddNonSaveable`.
- When enabled, scan `EntitiesManager.GetAllEntitiesOfType<IStaticEntity>()`.
- Immediately finish entities in `ConstructionState.InConstruction` via `IConstructionManager.MarkConstructed`.
- Immediately finish entities in `ConstructionState.InDeconstruction` via `IConstructionManager.MarkDeconstructed`.
- Disable the game's insta-build toggle when BDT instant build mode is enabled, matching the Utilities++ behavior but keeping it explicit and logged.

This slice intentionally avoids the Utilities++ reflection over `ConstructionManager.m_ongoingConstructions` and `m_ongoingDeconstructions`. The typed scan is less brittle and compiles against the currently verified game API.

## Later Slices

- Evaluate whether drag-upgrade and drag-downgrade tools belong in BDT's player-facing workflow.
- Initial area upgrade/downgrade support is implemented with configurable hotkeys and a BDT-native rectangle overlay. Localization is deferred until the English interaction is polished.
- Initial transport cleanup support is implemented with a configurable hotkey and a BDT-native red rectangle overlay. It targets disconnected belt and pipe segments, preserves the red entity highlight preview, and applies removal work on the sim loop.

## Verification Notes

- Build BDT with `dotnet build DesignerToolkit.sln -c Debug`.
- Runtime-check that enabling the mode completes construction and deconstruction.
- Runtime-check that disabling the mode leaves normal construction behavior untouched.
- Runtime-check that transport cleanup highlights only disconnected belts/pipes and removes the highlighted segments on mouse release.
- Confirm save/load and mod removal remain safe because no mod-owned game state is serialized.
