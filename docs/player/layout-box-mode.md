# Layout Box Mode

Layout Box Mode provides a 3D visualization overlay that helps designers see the exact grid footprint and vertical clearance of buildings. This makes it easy to align pipe or belt layouts above existing factory structures.

## Controls

- `Alt + B` (default): Toggles Layout Box Mode on and off.
- The mode can also be toggled via the Mod Settings window (under **LAYOUT**).

These hotkeys and options can be customized in BDT's mod settings or configured in `config.json`.

## Visual Elements

When activated, static buildings are enveloped by semi-transparent colored boxes mapping to their voxel-based layout boundaries:

- **Side Walls (Body)**: Rendered in transparent light blue. This shows the horizontal tile footprint of the building.
- **Roof Caps (Ceiling)**: Rendered in a more opaque, vibrant yellow/amber. This highlights the exact vertical height clearance level. You can safely build pipes, belts, or other elevated transports on any level above this yellow ceiling.

## Dynamic Performance

To keep frame rates smooth, the visualization automatically applies **camera distance culling** (rendering boxes only for buildings within 350 meters of the camera). The box cache is built dynamically when buildings are added or removed, causing zero performance impact during gameplay.

## Excluded Entities

- **Transports**: Belts, pipes, chutes, lifts, and power lines do not get layout boxes since they are spline-based links rather than rigid buildings.
- **Vehicles & Environment**: Trucks, excavators, harvesters, trees, and resources on the ground do not receive layout boxes.
