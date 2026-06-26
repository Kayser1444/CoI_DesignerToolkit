# Throughput Tools

BDT adds a unified throughput workflow for inspecting and testing transport-heavy blueprint designs.

## Inspector Panel

The **Throughput** inspector panel appears for transport-style entities such as belts, pipes, channels, sources, sinks, ports, lifts, balancers, sorters, and connectors.

The panel combines live monitoring with sandbox-only limiting controls.

## Monitoring

Throughput monitoring can display averaged flow rates in the world as either:

- absolute throughput in items/min
- percent of the entity's maximum capacity

Each monitored entity can use a configurable averaging period. Longer periods smooth out short bursts; shorter periods react faster to current behavior.

## Heat Map and Glow

The in-world overlay can glow selected entities using throughput coloring.

- **Capacity** mode colors by the entity's maximum transport capacity.
- **Relative** mode colors by how heavily the entity is being used.
- A colorblind-friendly blue/yellow/red palette is available in settings.
- Near-saturated entities can receive a stronger bottleneck glow.

## Area Tool

The **Throughput Area Tool** lets you drag-select a region and configure throughput display settings in bulk.

- `Shift+Alt+T`: arm the Throughput Area Tool by default
- `Alt+T`: toggle the global throughput overlay by default

The bulk window groups selected entities by type, allows display toggles per group, and can apply a shared averaging period without closing the window.

## Sandbox Limiting

Throughput limiting is available in sandbox mode. It can set custom maximum capacities on transports, sources, and sinks to test constrained versions of a design.

Limits are saved in the current save's BDT state but are not preserved in exported blueprints.
