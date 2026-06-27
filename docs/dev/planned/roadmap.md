# Roadmap

Planned and candidate improvements for Blueprint Designer's Toolkit.

## Planned Features

### Blueprint & File Management
- [ ] **Blueprint editor**: Inspect and edit blueprints directly in-game.
- [ ] **Blueprint terrain designations**: Support copying, pasting, and blueprinting of terrain designations.
- [ ] **Batch pasting**: Paste all blueprints inside a folder simultaneously.

### Entity & Transport Overlays
- [ ] **Ground Pollution (Solid Waste)**: Create a new overlay category for ground pollution caused by solid waste dumping (garbage, toxic waste, slag, etc.), separating it from water pollution.

### Markdown & Hub Integration
- [ ] **Auto-publish**: Automatically publish blueprints directly from the game to the hub.

---

## Completed Features

- [x] **Pollution Overlay & Heat Map** (v0.8.0) — Visual 3D overlay and entity highlight glow displaying current pollution/exhaust output, with separate sub-toggles for air, water, ship, and vehicle emissions.
- [x] **Rename Mod & Branding** (v0.3.0) — Renamed the mod to "Blueprint Designer's Toolkit" (BDT), updating the settings tab, thumbnail, and metadata.
- [x] **Settings Icon Upgrade** (v0.2.0a) — Integrated with the shared settings window using the AutoHelpers icon handoff so tab icons render properly.
- [x] **Buildable U1 Transports** (v0.3.0) — Added support for building U1 transports.
- [x] **Throughput Limiter on Transports** (v0.5.0) — Added custom max throughput limits on transports via the entity inspector.
- [x] **Throughput Display / Heat-map** (v0.5.0a) — Consolidated throughput inspector panel, overlay, bottleneck highlight, and heat-map.
- [x] **Sources & Sinks Rate Limiters** (v0.5.0a) — Extended throughput limiter and monitoring capabilities to sandbox sources and sinks.
- [x] **Mori's Upgrade Hook** (v0.3.0) — Replaced the custom AoE upgrade/downgrade tool with a native hook on the 'i' tool combined with instant build.
- [x] **Sandbox-Restricted Instant Actions** (v0.5.0a) — Enforced that instant build, upgrade, and downgrade are active only in Sandbox mode.
- [x] **Content Display on Balancers** (v0.7.0) — Added product buffer content display panels to the balancer (`ZipperInspector`) using reflection to read its internal input and circular output buffers.
- [x] **'Recycle Bin'** (v0.7.0) — Automatically copies deleted/updated blueprints/folders to a recycle bin folder with a configurable name/toggle, suppressing confirmation popups outside of it.
- [x] **Undo Place Blueprint** (v0.7.0) — Transient, in-memory undo stack (Ctrl+Z) to revert blueprint placements, copy-pasting, and force-placements.

---

## Abandoned / Deferred

- [x] **Allow building transports parallel to terrain ramps** — *Abandoned*. Required highly complex modifications to pathfinder and construction helper constraints. Deferred in favor of the more robust `LegacyBeltConfigurations` settings. See architectural notes in [ramp-slope-mode.md](../abandoned/ramp-slope-mode.md).
