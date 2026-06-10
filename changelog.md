# Blueprint Designer's Toolkit Private Changelog

This private changelog tracks in-progress and alpha changes for maintainers and testers. Public release notes still live in `changelog.txt` and are updated only when packaging or releasing.

## v0.4.1a [unreleased]

- 

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
