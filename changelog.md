# Blueprint Designer's Toolkit Private Changelog

This private changelog tracks in-progress and alpha changes for maintainers and testers. Public release notes still live in `changelog.txt` and are updated only when packaging or releasing.

## v0.10.0 [released]

- Fixed the missing `pollution_show_solid_waste` configuration parameter and localized BDT control labels and tooltips across all supported languages.
- Fixed late localization of vanilla Controls-menu keybindings by refreshing registered BDT keybind attributes after the translation bundle is applied.
- Added optional **Pre-color pipes** behavior for empty fluid and molten pipes, using connected upstream sources and equally blending multiple resolved fluids.
- Kept vanilla transport coloring authoritative after a pipe contains products, including the normal fade from the seeded color toward the active flow state.
- Refreshed affected pipe clusters after topology, recipe, source-product, and construction changes, including while the game is paused, without changing simulation state.
- Added session-only `bdt_diagnostic_level` control with tiered debug and trace diagnostics for BDT runtime troubleshooting.
- Reorganized the settings panel under **BUILD BEHAVIORS**, grouping instant build, curvy incline belts, and pipe pre-coloring together.

## v0.9.1 [released]

- Added configurable pollution heatmap glow colors (`white`, `brown`, `purple`, or `#RRGGBB`) through `config.json` and the `bdt_set_pollution_glow_color` console command; the color applies to both entity highlights and screen-space halos.
- Fixed: Legacy stacker placement now rejects terrain support configurations that would exceed its post-construction collapse threshold (fix for [Discord report](https://discordapp.com/channels/803508556325584926/1403765971327389848/1403765971327389848)).
- Added a per-stacker **Alert when full** inspector toggle, enabled by default, with an orange vanilla dumping warning and the stacker-tower debounce behavior.
- Fixed: Legacy stacker full-alert preferences now persist through the declared per-save BDT state slot.

## v0.9.0 [released]

- Updated the maximum verified Captain of Industry version to `0.8.7a`.
- Fixed: Routed Blueprint Stats and recycle-bin validation text through BDT localization.
- Removed the obsolete custom hotkey editor and legacy per-setting hotkey labels; hotkeys are managed through the vanilla Controls menu via AutoHelpers.
- Refined the **Remove products in area** footer so all action buttons expand to fill the available row width.
- Fixed: Localized the dynamic Remove, Cancel, and product-total labels in the area-removal dialog.
- Added a shared regular/quick product-removal domain for transports, lifts, connectors, sorters, balancers, and explicitly registered modded entities.
- Added vanilla-style regular removal to built-in logistics entities that previously supported only quick removal; regular orders block normal input/output until their one-shot product scope is emptied or cancelled.
- Added combined inspector removal controls with live quick-removal cost and regular-order cancellation.
- Added the **Remove products in area** tool, defaulting to `Alt+Backspace`, with native overlap selection, world highlighting, entity-type toggles, graphical product totals, and live action counts.
- Added regular, quick, and cancel batch commands with live Unity affordability checks and vanilla sandbox handling.
- Persisted active regular-removal scopes through the vanilla per-mod config cache and detached runtime clearing buffers during save serialization.
- Added `ITransportProductRemovalAdapter`, `TransportProductRemovalAdapterRegistry`, and `TransportProductRemovalUi` as the explicit integration seam for compatible modded entities.
- Added full translations for the new UI across German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese.
- Verified regular and quick removal on native transports, connectors, sorters, lifts, and balancers; verified save/quit/reload restoration and safe loading after removing the mod.
- Manual limitation: explicit third-party adapter integration remains untested because no compatible external test mod is currently available.

## v0.8.13 [released]

- Added a daily-sampled **Radiation Overlay** for unsafe radioactive inventory, using the same `Calendar.NewDay` boundary as vanilla radiation accounting.
- Added radiation settings for overlay visibility, green entity glow, and a configurable `0-360` day averaging window (default `30`; `0` disables collection).
- Added local source coverage for machines, ordinary storage, flat conveyors, trucks, connectors, lifts, and other supported transport buffers; vanilla-safe reactor and radioactive-waste-storage buffers are excluded.
- Added translationless radiation labels in the `#N.D#` format and translated all new settings strings across the supported locales.
- Fixed: Radiation and pollution overlays remain visible while the BDT Mod Settings window is open.
- Fixed: Radiation screen glow is centered on the source entity rather than its label.
- Fixed: Radiation entity glow is removed when the source entity is deleted.
- Fixed: Radiation heat-map scaling uses a fixed zero baseline and scales sources against the strongest current source.
- Fixed: Right Alt keybindings no longer capture the keyboard layout's synthetic Ctrl and AltGr aliases.
- Fixed: Lazy height routing now uses the vanilla transport modifier sound when held, released, or toggled instead of the generic button click sound.

## v0.8.12 [released]

- Added localized tooltip text for the Update blueprint action across all supported languages.
- Changed the Update blueprint control to a compact icon-only button positioned immediately after the vanilla Place button.
- Refined Update blueprint spacing to match the surrounding vanilla blueprint-book controls.

## v0.8.11 [released]

- Fixed: Dot-prefixed translation metadata is excluded from runtime localization scans and release packages.
- Fixed: Blueprint operational stats remain available when the game nests construction-cost controls inside the blueprint content summary.

## v0.8.10 [released]

- Added initial game startup options controlled via `config.json`:
  - `startNewGamePaused` (`bool`, default `true`): starts new games paused at tick 0.
  - `saveGameOnFirstDayAs` (`string`, default `"Initial"`): automatically creates an initial autosave under the configured save game name when a new game is first unpaused (blank string disables).
- Improved initial autosave handling on first unpause:
  - Replaced tick-delay logic with a two-stage state machine that confirms the initial pause state before waiting for the player to unpause, ensuring all building setup performed while paused is captured at tick 0 without losing any simulation ticks.
  - Coupled `isFirstUnpausePending` flag reset directly into `beforeSave()` after state JSON serialization completes, guaranteeing `Initial.spnt` is saved to disk with `isFirstUnpausePending: true` so reloaded initial saves automatically create a new autosave (`Initial (1)`, `Initial (2)`, etc.) when unpaused.

## v0.8.9 [released]

- Fixed ground landfill pollution overlay rate calculation and tile clustering to report steady monthly emission rates and track sub-surface landfill layers across the 4-year recovery period.

## v0.8.8 [released]

- Added Ground Pollution (Solid Waste / Landfill) overlay category to BDT's pollution tools (`Show ground pollution (landfill)` setting toggle, localized strings, config store, and map overlay filtering).
- Implemented terrain landfill tile scanning and heap clustering to render pollution glow highlights and summary labels (`[Pollution(Tiles)]`, e.g. `[24.5(21)]`) for waste dumped directly on terrain.
- Added tooltip explanation for `[Pollution(Tiles)]` label formatting in setting description.
- Restored light-grey to white color interpolation for pollution overlay labels to preserve visual contrast against green/red throughput heatmaps.
- Fixed Height Routing modifier hardcoded Alt fallback so unmapping the hotkey in Controls menu properly releases the Alt key.

## v0.8.7 [released]

* Improved: Replaced the Russian translation with the reviewed community-provided localization.
- Updated startup banner logging to execute auto console mirroring and emit version and DLL timestamp during renderer initialization so it is visible in the in-game console without a duplicated `[BDT]` prefix.
- Added Transport Height-Routing Modifier (ASAP vs. Lazy height matching) allowing players to hold Alt (or click the toolbar ramp icon to toggle) while placing belts & pipes to keep them at their starting height for as long as possible before changing elevation.

## v0.8.6 [released]

- Updated max verified game version compatibility to 0.8.6b.
- Compacted pollution rate text format to `[X.X]` to clearly distinguish pollution labels from throughput labels (`X.X / min`) without language-specific prefixes.
- Adjusted pollution text font style to bold and lightened low-pollution label color to improve text legibility on dark overlay background boxes.


## v0.8.5 [released]

- Updated for compatibility with Captain of Industry Update 4.2 (v0.8.6), introducing a game API compatibility layer.
- Updated the minimum supported game version to 0.8.5.
- Rendered both primary and secondary hotkeys using native `KeyBindUi`, separated by a localized "or" label.
- Renamed layout box tool settings and hotkey label to "Toggle layout box overlay".
- Updated the pollution glow (heat map) setting to be off by default.
- Fixed Swedish translation for "items/min" to "st/min".
- Improved the mod's short description in manifest.json.


## v0.8.4 [released]
- Fixed release ZIP entry paths to use portable forward slashes, ensuring reliable extraction on Linux.
- Replaced custom TMPro-based keybind badge markup in Settings rows with the game's native `KeyBindUi` component to perfectly match the vanilla visual style.
- Updated German, Portuguese, and Swedish layout-box headings and completed other translations.


## v0.8.3 [released]

- Localized settings dropdown option labels and hotkey description tooltips:
  - Extracted and registered translation keys for height filter and throughput heatmap options (*All*, *None*, *Relative*, and *Capacity*).
  - Wired tooltips in mod settings to append native, fully localized keybind customization reminders.
- Cleaned up obsolete hotkey default configuration settings and descriptions from `config.json`.
- Merged and integrated Russian terminology improvements from PR #2 to ensure high-quality and game-accurate localizations:
  - Fixed various semantic inaccuracies and formatting issues.
  - Adjusted dropdown and tooltip phrasing for Swedish, German, Spanish, Portuguese, Italian, and Chinese.
## v0.8.2 [released]

- Added audible feedback when activating or toggling mod tools/overlays via hotkeys:
  - Fetched and initialized the vanilla `ButtonClick.prefab` UI sound from `AudioDb`.
  - Configured height filter, throughput, pollution, layout box, area selection, transport cleanup, and undo keybinds to play the standard click sound.
- Integrated mod hotkeys into the native Captain of Industry controls customization system:
  - Centralized and registered all hotkeys as native keybindings under a custom category.
  - Renamed and formatted keybind labels using sentence case (e.g., *Transport cleanup tool*, *Toggle throughput overlay*, *Toggle pollution overlay*).
  - Clarified Throughput tool keybinding description to "Activates" rather than "Toggles" the throughput area selection tool.
- Redesigned the mod settings UI to inline keybind badges onto their respective toggles and removed redundant hotkey-only rows and headings.
- Cleaned up obsolete config loading, saving, resetting, and serialization code from `BDT.Settings.cs`.
- Synchronized translation files using `translate.py` to localize the updated keybind tooltip description across Swedish, German, Spanish, Portuguese, Russian, Italian, and Chinese.
- Fixed: Applied `AutoHelpers` localization before registering the Mod Settings tab, preventing duplicate English/localized settings tabs in localized languages.
- Translated the mod name across all supported non-English locales (German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese) and polished translation consistency.

## v0.8.1 [packaged]

- Changed default pollution settings so that both the pollution overlay and the heat map (glow) are off by default.
- Added **Throughput AoE select/deselect all** checkbox to the Throughput Area Tool window:
  - Aligns vertically with the child list item checkboxes and features a horizontal separator below.
  - Toggles the display state of all selected items/groups in bulk.
  - Updates automatically when individual rows are manually checked or unchecked.
- Added screen-space label tracking for long transports (conveyors and pipes):
  - Dynamically floats the overlay text to the nearest visible segment on screen so labels stay visible while zoomed in.
  - Automatically falls back to the center tile when all segments are off-screen or the camera distance exceeds 400 meters.
- Fixed `NullReferenceException` during shutdown/unload in `HeightFilter`:
  - Added an early-out guard to `ShowAllHidden()` to safely abort restoring hidden visuals if the renderer subsystems are already disposed, eliminating shutdown warning spam.
- Fixed `Outer enumerator finished first?` multi-threading assertion in `PollutionWorldRenderer`:
  - Cached active moving entities (vehicles, locomotives, and ships) during `OnSyncUpdate` on the main thread.
  - Read from this cached snapshot list in `OnGUI()` instead of calling `GetAllEntitiesOfType<T>()` concurrently with the simulation thread.
- Fixed event unregistration assertions and warnings:
  - Tracked `SyncUpdate` registration state in `ThroughputWorldRenderer` and `PollutionWorldRenderer` to prevent calling `RemoveNonSaveable` on already unregistered callbacks.
  - Changed `PollutionManager` event unregistration to call `RemoveNonSaveable` instead of `Remove` to match its `AddNonSaveable` registration.

## v0.8.0 [released]

- Added **Batch Placing** feature for blueprint folders ("Place all" button):
  - Spawns ghosts of all direct child blueprints side-by-side in a single cursor placement.
  - Automatically wraps blueprints into multiple rows if the total width of a row exceeds **512 tiles** (the game's standard limit).
  - Automatically crops (skips) remaining blueprints in the folder if the total height of the batch grid would exceed **512 tiles**, staying safely within placement limits.
- Added **Blueprint Spacing** setting under a new `PLACE FOLDER` section in mod settings:
  - Configurable spacing parameter ranging from `0` to `12` tiles (default `6`).
  - Limits are enforced on user input and adjust buttons.
  - Fully localized in all supported languages (including Swedish).
- Added **Pollution Labels** overlay feature:
  - Renders per-entity average pollution rates (items/min) as floating world-space labels above factories, outfalls, vehicles, locomotives, and ships.
  - Labels are color-coded on a green → orange → red gradient relative to the min/max pollution across all visible entities.
  - Includes UI occlusion checks to hide labels behind active game windows/panels.
  - Togglable via hotkey; independent toggle for label text vs. entity glow.
  - Filters by pollution source type: Air, Ground, Vehicle, and Ship, each independently togglable in settings.
- Added **Pollution Heat Map** glow feature:
  - Applies `EntityHighlighter` glow to polluting entities.
  - Scaled the glow color to a pure white gradient, fading from high opacity (for heavy emitters) to transparent (for zero/low emitters).
  - Dynamically upscales the game's global glow outline radius (blur size and passes) when the overlay is active, restoring vanilla settings upon deactivation.
  - Independent toggle from the text overlay, allowing glow-only or labels-only display.
- Added pollution tracking system (`PollutionManager`) with history-based daily recording:
  - Tracks factories, outfalls (air/ground), vehicles, locomotives, and ships.
  - Configurable averaging period (days) shared with the throughput system.
  - Reworked ship tracking from stateless prediction to history-based daily recording:
    - **Main ship (BattleShip)** pollution tracked via a `FuelStatsCollector.ReportFuelUseAndDestroy` Harmony prefix that intercepts `FuelUsedBy.BattleShip` events.
    - **Cargo ships** track real fuel consumption via a `CargoShipV2.ConsumeFuel` Harmony prefix instead of stateless route-rate prediction.
    - Docked ships' average pollution decays naturally over the configured averaging period, matching vehicle and factory behavior.
    - Ship pollution history rolls over in `OnNewDay` alongside all other entity types (removed the `PollutionType.Ship` skip).
  - Fixed: 10,000× vehicle pollution scaling bug.

## v0.7.0 [released]

- Added **Undo Place Blueprint** feature (default hotkey `Ctrl+Z`):
  - Allows reverting blueprint placement, copy-paste, and force-placement (Shift-click) actions.
  - Instantly cancels placed ghosts or starts deconstruction of fully built structures (destroys immediately in sandbox mode).
  - Restores overwritten pre-existing ghosts and entities, and reverts pasted surface designations or decals.
  - History is kept purely transient (in-memory) and is not serialized into saves, satisfying the mod's save removability constraints.
- Restricted custom keybinding registration in the settings UI to allow at most one non-modifier key (trigger key) to match vanilla constraints.
- Added product buffer content display panels to the balancer (`ZipperInspector`) using reflection to read its internal input and circular output buffers.
- Added a custom Quick Remove (trashcan) button to the balancer content panel, supporting dynamic Upoints cost calculation using `QuickDeliverCostHelper`.
- Intercepted and handled `QuickRemoveFromEntityCmd` for balancers in `EntitiesCommandsProcessor` prefix patch to cleanly empty their buffers via reflection and consume the required Upoints.
- Added a **Blueprint Recycle Bin** feature:
  - Automatically copies blueprints or folders to a special root folder (default name `"Recycle Bin"`) when they are deleted or updated.
  - Replicates the item's original parent folder path inside the Recycle Bin (e.g., deleting `/Temp/ABC/MyBlueprint` moves it to `/Recycle Bin/Temp/ABC/MyBlueprint_0`).
  - Suffixes copies with `_n`, starting with `_0` (e.g. `Name_0`), to avoid collisions.
  - Suppresses the confirmation popup for deletions occurring outside the Recycle Bin folder when enabled.
  - Deletions inside the Recycle Bin folder (or nested inside it), as well as deleting the Recycle Bin folder itself, remain permanent and prompt the default confirmation.
  - Encapsulates the Recycle Bin folder name with `<color=grey>...</color>` when active, so it is rendered in grey in the blueprint book UI.
  - Added setting toggle (`Use recycle bin`) and text input field (`Folder name`) under a new `RECYCLE BIN` section in mod settings.
  - Validates folder names (non-empty, non-whitespace, <= 60 characters) and displays an error state for invalid input.
  - Dynamically renames the Recycle Bin folder in the blueprint library when the folder name setting is modified, unless a folder with the new name already exists.
  - Fully localized settings and tooltips across all supported languages (German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese).
- Changed the settings tab icon to match the blueprint book toolbar icon.
- Fixed vertical centering of the power icon and its label in the blueprint detail panel's operational cost summary.

## v0.6.2a [released]

- Added **Layout Box Visualization Mode** (default hotkey `Alt-B`):
  - Renders building grid clearance / occupancy bounding boxes in 3D.
  - Separates side walls (transparent light blue) and roof caps (more opaque vibrant amber/yellow) to clearly highlight vertical clearance levels.
  - Custom manually-built unit cube mesh avoiding external asset reliance.
  - Optimized rendering using entity-level camera distance culling (350 meters) to keep CPU overhead and C# DrawMesh calls minimal.
  - Rebuilds cache reactively on entity addition/removal, causing zero garbage collection allocations in `Update()`.
  - Fully integrated setting toggle and `Alt-B` hotkey in `BDT.Settings.cs` and keybindings list.
  - Wired up settings state and hotkey persistence to JSON saves and global config.
  - Fully localized settings and descriptions across German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese.

## v0.6.2 [released]

- Added entity glowing effects reflecting the current heatmap color logic when the throughput overlay is active
- Added independent toggles for the heatmap glow and the text overlays
- Clamped and restricted the averaging period text input to numeric values (1-360) in the Bulk AoE Tool Window
- Updated localizations across all supported languages for the new glow toggles

## v0.6.1 | 2026-06-13

- Fixed Throughput Limiter/Monitor inspector panel disappearing when changing maps/saves without restarting the game
- Fixed negative/zero throughput display values due to incorrect patch execution order

## v0.6.0 [packaged]

- Left-aligned "Set averaging period" and right-aligned "days" unit label and numeric input controls in the Throughput AoE tool window
- Styled the averaging days input textfield in the Throughput AoE tool window to use the segmented LCD number style (display font and background)
- Updated: translated all remaining untranslated keys across German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese localization files
- Changed: set default throughput heatmap mode to Capacity

## v0.5.0a [released]


- Upgraded **Throughput Limiter** UI: the input field now uses the game's LCD display font styling
- Added `+` and `-` buttons to the Throughput Limiter UI, supporting `Shift` (5x) and `Control` (10x) step modifiers
- Restricted the Throughput Limiter UI controls to **sandbox mode only** (with a tooltip explaining this in standard games)
- Merged the Throughput Limiter and Throughput Monitor into a consolidated **Throughput** inspector panel
- Added **Throughput heat-map** settings with `Relative` and `Capacity` modes
- Added **Colorblind-friendly colors** option (Blue-Yellow-Red) with a descriptive tooltip
- Added **Show throughput as percent** option to render overlays as a percentage of maximum capacity
- Implemented **UI occlusion checks** using raycasts to hide throughput labels rendered behind active UI windows/panels
- Optimized occlusion check performance by caching visual element panels once per frame, preventing Factory rendering lag
- Added a pulsing neon red-orange **bottleneck glow border** behind overlay text for entities running at >98% capacity
- Extended throughput monitoring and inspector UI to sandbox sources and sinks
- Implemented **Throughput Area Tool** (defaults to `LeftShift + LeftAlt + T`) to click/drag and select an area of entities
- Added a **Simplified Bulk Configuration Window** for the AoE tool:
  - Collapses multiple tiers of selected entities (e.g. Flat lift I, II, III) into single, summarized checklist entries
  - List checkboxes directly toggle throughput display visibility for all entities in that category instantly
  - Inline Set Days controls and a non-closing "Apply" button allow configuring averaging days iteratively
- Mitigated hotkey conflicts by requiring exclusive modifiers for key detection, preventing `Shift-Alt-T` from triggering the `Alt-T` display toggle
- Automatically activates the throughput overlay display when the AoE tool window is used
- Reordered throughput inspector fields to right-align numeric input controls flush with the right edge
- Localized all new strings and fully updated Russian translations (`translations/ru.json`)

## v0.5.0 [released]

- Added **Throughput Limiter** feature: allows setting a custom max throughput (items/min) on any transport, source, or sink via the entity inspector

- Added **Legacy Belt Configurations** setting: when enabled, bypasses the pathfinder and construction-helper constraints that normally prevent a belt from both turning and changing height on the same tile
  - Harmony transpiler on `TransportPathFinder.tryGetStepCost`: replaces `RelTile2i.IsParallelTo` calls with a hook that returns `true` when the setting is on, suppressing the "no turn while ramping" IL guards
  - Prefix on `TransportPathFinder.InitPathFinding`: strips `StartMustBeFlat` / `GoalMustBeFlat` / `BanStartRampsInX` / `BanStartRampsInY` flags and clears `BannedStartDirections`, so the pathfinder allows ramp steps in any direction at the start and goal nodes
  - Postfix on `TransportPathFinder.InitPathFinding` and `ChangeGoal`: force-clears `m_startMustBeFlat`, `m_goalMustBeFlat`, `m_startMustNotHavePerpendicularRamp`, and `m_goalMustNotHavePerpendicularRamp` after the port-scanning loop sets them
  - Transpiler on `TransportsConstructionHelper.CanChangeDirectionOf`: intercepts `ldfld Tile3i.Z` instructions (not `call get_Z` — Z is a public field) and inserts a hook returning 0 when enabled, suppressing the "only reverse is allowed when extending a ramp" guard
- Confirmed working in-game: single-tile belts can now both turn and ramp simultaneously when the setting is enabled
- Renamed feature from `AllowShortCurvyBelts` to `LegacyBeltConfigurations` (file, class, property, config key, state-blob key, and loc strings)


## v0.4.0 [released]

- Added height filter rendering and controls for transports, transport pillars, and layout entities (such as sorters, zippers, mini-zippers, and lifts) to filter visibility from levels 0 (underground only) to 6 (all)
- Added default hotkeys `PageUp` (increase visibility level) and `PageDown` (decrease visibility level)
- Fixed height filter system to safely run inside SyncUpdate instead of InputUpdate, resolving race conditions and "Dictionary changed during iteration" exceptions in the simulation thread
- Fixed pillar rendering filtering by deferring the initial height filter application by 5 rendering frames, ensuring the game's TransportPillarsRenderer has initialized RendererData on first load
- Refined pillar show/hide logic to handle cases where game updates regenerate transport pillars
- Synced updated translation keys and English fallbacks into all language files, with Swedish translations added for the new height filter settings
- Removed unused/stale translation keys from `en.json`, `sv.json`, and other locale files
- Added player and developer documentation for the height filter feature


## v0.3.1 [released]

- Removed obsolete area upgrade hotkeys from config.json
- Synced updated translation keys and English fallbacks into all language files
- Fixed settings save default failure ("Save failed: Invalid path") by using Manifest.RootDirectoryPath instead of Assembly.Location for config.json resolution
- Fixed settings save default validation logic (Regex.IsMatch) to successfully write configuration file even if values match currently stored defaults
- Updated and translated the "Restore defaults" tooltip note across all 8 language files to refer to the "Save as config" button and config.json

## v0.3.0 [packaged]

- Renamed the mod identity around Blueprint Designer's Toolkit (BDT)
- Added a BDT-native instant build mode based on Mori's Utilities++ source
- Moved imported Utilities++ source to `imports/UtilitiesPlusPlusImport` as non-compiled reference material
- Added `instant_build_mode` config default and per-save BDT settings-state persistence
- Added a BDT settings toggle for Instant build mode under INSTANT BUILD
- Added a non-saveable sim-loop subscriber that scans static entities and finishes in-progress construction/deconstruction when instant build mode is enabled
- Kept the implementation typed against current CoI construction APIs instead of reflecting over `ConstructionManager` private dictionaries
- Added insta-build suppression when instant build mode is enabled, matching the Utilities++ behavior
- Added dev and player docs for the instant build mode integration
- Added translation keys for the new settings UI, with English fallback strings in non-English locales
- Removed custom Area Upgrade Tool and integrated its instant-finish logic into `InstantBuildMode` to natively support the game's upgrade tool
- Deferred translations for the area upgrade/downgrade tool until the English interaction is polished
- Added a transport cleanup tool with default hotkey (`Alt+Del`), red drag/highlight preview, and sim-loop removal of disconnected belt/pipe segments
- Deferred translations for the transport cleanup tool until the English interaction is polished
- Moved tool hotkeys into BDT config/mod settings, with vanilla-style primary/secondary keybinding fields and the previous shortcuts kept as defaults
- Widened the BDT settings layout and shortened keybinding labels so primary/secondary hotkey fields fit cleanly
- Updated the shared Mod Settings window to reopen on the last active mod tab during the current runtime session
- Corrected boolean config defaults to use documented `true`/`false` values instead of integer flags
- Verified `dotnet build DesignerToolkit.sln -c Debug --no-restore` succeeds; normal restore remains blocked locally by a NuGet temp lock
- Rephrased the sandbox warning for Instant Build Mode to a British derogatory expression

## v0.2.0a [packaged]

- Added **Restore defaults** and **Save as global** buttons to Designer Toolkit's Mod Settings tab
- Added **Markdown number format** setting with Auto, English separators, and Local separators modes
- Fixed Markdown **Both** language mode so it does not duplicate English output when the current game language is English
- Fixed Markdown export so products and entities with identical display names no longer have their stats merged internally
- Rebuilt Designer Toolkit with the updated AutoHelpers Mod Settings icon handoff so tab icons render through the shared settings window
- Changed Markdown export to use vanilla game translations for workers, electricity, and computing, removing duplicate Designer Toolkit translation entries
