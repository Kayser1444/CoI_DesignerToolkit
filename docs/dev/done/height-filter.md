# Height Filter

Status: implemented. The height filter rendering system allows stepping down the rendering of transports, pillars, and layout entities from level 0 (underground only) to level 6 (all).

## Implementation Details

The height filter system is implemented in `src/BDT.HeightFilter.cs` and integrates with the game's simulation and rendering pipelines:

- **Sync Update Loop**: To avoid threading issues with the main simulation, updates to entity renderers (like `InstancedChunkBasedTransportsRenderer`, `TransportPillarsRenderer`, `IoPortsRenderer`, and `InstancedChunkBasedLayoutEntitiesRenderer`) are run inside the thread-safe `SyncUpdate` loop rather than `InputUpdate` or simulation ticks.
- **Rendering Delay**: The initial application of the height filter is deferred by 5 rendering frames at startup (`RenderUpdateEnd`). This ensures that the game's `TransportPillarsRenderer` has finished initializing `RendererData` before BDT queries or manipulates it.
- **Selection Suppression**: Harmony patches on `Transport.IsSelected`, `StaticEntity.IsSelected`, and `IoPortsRenderer.PortsChunkStandard.ShowPort` prevent selection and port rendering for hidden entities, ensuring they are protected from accidental player interaction.
- **Save removability**: The height filter is entirely visual and does not serialize any custom states into saves, maintaining BDT's compatibility with the save add/remove constraints.

## Controls and Configuration

- Default hotkeys: `PageUp` (increase visibility level) and `PageDown` (decrease visibility level).
- Configurable settings stored globally in `config.json` via keys:
  - `height_filter_show_layer_hotkey_primary` (default: `"PageUp"`)
  - `height_filter_hide_layer_hotkey_primary` (default: `"PageDown"`)
