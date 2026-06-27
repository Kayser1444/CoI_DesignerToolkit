# 🧰 Kayser's Blueprint Designer's Toolkit v0.8.0

Kayser's Blueprint Designer's Toolkit (BDT) version 0.8.0 is a major update focusing on batch workflow automation and advanced environmental overlay tracking.

This release introduces **Batch Placing** for folders, the **Pollution Overlay & Heat Map**, and consolidates the recent **Undo place blueprint**, **Blueprint recycle bin**, and **Layout box mode** features into a unified suite for Captain of Industry designers.

As always, BDT is built around the core rule: **designer-only, consumer-free**. Blueprints created or tested with BDT remain 100% vanilla-compatible, and players who download your designs do not need this mod installed.

---

## 📤 Batch Placing

BDT 0.8.0 introduces a **Place all** button inside the blueprint folder detail panel, letting you place all blueprints in a folder side-by-side in a single action.

![batch-placing.png](placeholder-batch-placing.png)

- **Automatic Wrapping & Cropping**: Instantly tiles blueprints in a grid. If a row exceeds the game's **512-tile** limit, BDT automatically wraps to a new row. If the overall height exceeds 512 tiles, it crops remaining blueprints to stay safely within engine limits.
- **Configurable Spacing**: Set custom spacing (from `0` to `12` tiles, default `6`) between placement ghosts via a slider in Mod Settings.

---

## 🏭 Pollution Overlay & Heat Map

Visualize and trace exhaust rates across your island with real-time tracking, overlays, and color-coded glows.

![pollution-overlay.png](placeholder-pollution-overlay.png)

- **Floating World-Space Labels**: Displays average emissions (items/min) above factories, waste outfalls, vehicles, locomotives, and ships. 
- **Dynamic 2D Screen-Space Glow**: Bypasses vanilla LOD culling by drawing zoom-dependent 2D halos. Opposed to flat highlights, glow size and opacity scale up as you zoom out so polluters remain highly visible.
- **Proportional Heat Map scaling**: Low emitters (like a 2.5/min vehicle) get small, faint, low-opacity glows. Major emitters (like a 75/min smokestack) stand out as large, high-opacity halos.
- **Smart 3D Highlight Threshold**: Low-polluting entities (relative pollution $t < 0.1$, e.g. the main ship at 0.1/min) skip the 3D outline shader entirely. This prevents huge meshes from accumulating blur and glowing unproportionally bright on screen.
- **Common Comparison Pool**: Pollution values are scaled linearly against a shared global pool of all currently active categories. This lets you directly compare air, water, ship, and vehicle emissions. Hiding categories in settings recalculates the comparison pool.
- **History-Based Averaging**: Customize average calculations (default 360 days) and toggle Air, Ground/Water, Vehicle, or Ship overlays independently.

---

## ↩️ Undo Place Blueprint (Ctrl+Z)

BDT adds a transient, in-memory undo stack for blueprint placement, copy-paste, and force-placement actions.

![undo-place-blueprint.png](placeholder-undo-place-blueprint.png)

- Instantly cancels placed ghosts or triggers immediate demolition of built structures.
- Restores overwritten pre-existing ghosts and entities.
- Reverts pasted surface designations and decals.
- History is kept purely in-memory and is never serialized into saves, satisfying the mod's save removability constraints.

---

## ♻️ Blueprint Recycle Bin

Delete and update blueprints with complete peace of mind.

![blueprint-recycle-bin.png](placeholder-blueprint-recycle-bin.png)

- Automatically copies deleted or updated blueprints/folders to a configurable root-level folder (default `"Recycle Bin"`).
- Replicates the item's original folder path and appends numeric suffixes (`_0`, `_1`) to avoid name collisions.
- Suppresses confirmation popups for deletions outside the recycle bin. Permanent deletions inside the recycle bin still prompt the standard confirmation.

---

## 📦 Layout Box Mode (Alt+B)

Renders building grid footprint and vertical clearance bounding boxes in 3D.

![layout-box-mode.png](placeholder-layout-box-mode.png)

- Makes it easy to plan elevated pipe stacks, layered belt paths, or dense logistics over existing structures.
- Uses custom meshes and camera-distance culling (350 meters) to keep CPU overhead minimal.

---

## 🚦 Throughput Tools

Test and inspect transport flow with live overlays, heat maps, and bulk area configuration.

![throughput-monitoring.png](/content-images/02b14618ac2ad76734545c73a4c53120522fd11e2810bbb3de95aa0870f324ce/image.png)

- **Throughput Overlay**: Displays live items/min flow rates or percent of maximum capacity directly over transports, sources, sinks, and ports.
- **Heat Map Glow**: Belts, pipes, and connectors glow based on utilization or maximum capacity.
- **Throughput Area Tool (Shift+Alt+T)**: Drag-select a region to quickly configure display settings or apply shared averaging periods in bulk.
- **Sandbox Limiters**: Enforce custom maximum throughput limits on transports, sources, and sinks to test bottleneck scenarios.

---

## 📦 Compatibility and Installation

BDT is compatible with vanilla saves and can be added to or removed from existing saves safely.

- **Requirements**: Captain of Industry version `0.8.2` or newer.
- **Installation**: Extract the `DesignerToolkit-0.8.0.zip` folder into your Captain of Industry mods directory (`%AppData%\Captain of Industry\Mods`), and enable it in the mod menu.
