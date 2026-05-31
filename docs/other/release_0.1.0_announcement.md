# Kayser's Designer Toolkit v0.1.0

This is the first public release of Kayser's Designer Toolkit, a toolbox for Captain of Industry blueprint creators who spend too much time carefully arranging things and then discover one tiny mistake after the blueprint is already in the book.

The short version: DTK lets you update an existing blueprint from a new area selection, remembers where you were in the blueprint book in each save, adds operational cost stats to the detail panel to help with documentation, normalizes symmetric entities so paste-over behaves better, and includes a standalone blueprint inspector for digging into blueprint strings.

## Designer-Only, Consumer-Free

DTK is built for blueprint authors, not blueprint users.

Players who download and use blueprints made with DTK do **not** need this mod installed. The mod only helps during creation and editing. The saved blueprint data remains standard Captain of Industry blueprint data.

That makes DTK safe to use for public blueprint packs, shared designs, and normal workshop-style authoring workflows.

## Update Existing Blueprints

The blueprint window now has an **Update** button.

Select an existing blueprint, click **Update**, choose a new area, and DTK replaces the blueprint contents while preserving the parts you usually wanted to keep:

- name
- description
- overlap settings
- position in the current folder

This is the main quality-of-life feature for v0.1.0. It turns "I need to remake and refile this blueprint" into "fix it in-world and update the existing entry."

## Blueprint Book Memory

DTK remembers the last blueprint book folder you opened and restores it when the blueprint window is created again.

The path is saved in `config.json`. If a folder was renamed or removed, DTK falls back to the deepest matching folder it can still find, so it should fail quietly instead of getting in the way.

## Operational Cost Summary

The blueprint detail panel now splits vanilla construction cost from an added **Operational cost** summary.

When the selected blueprint contains relevant entities, DTK shows compact tiles for:

- workers
- electricity
- computing
- maintenance by product

Only non-zero values appear. Small blueprints stay uncluttered, and large factory blocks get a better at-a-glance read before you stamp them into a real build.

## Symmetric Entity Normalization

DTK fixes a particularly annoying blueprint authoring edge case around rotationally-symmetric entities.

Some entities, such as balancers/zippers and mini-zippers/connectors, are functionally identical in multiple rotations or reflection states. Captain of Industry can still store those orientations differently in blueprint data, which may prevent paste-over updates even when the placed entity is effectively the same.

DTK normalizes those symmetric entities at blueprint capture time:

- rotation is reset to canonical orientation
- reflection is cleared
- symmetry is detected from port positions at runtime
- prioritized port flags are remapped so the priority intent survives
- asymmetric entities are left alone

The important bit: this changes only the captured blueprint data. It does not modify live world entities, does not patch blueprint placement, and does not create a dependency for players using the blueprint later.

## Blueprint Inspector Tool

The v0.1.0 package also includes a standalone browser inspector at:

```text
tools/blueprint-decoder.html
```

Paste a Captain of Industry blueprint string into it and it will decode the payload into a more readable view, including metadata, entity rows, transforms, trajectories, prioritized ports, extracted strings, and a hex dump.

This is mostly for authors and modders who want to see what is really inside a blueprint string. It is not required in-game, but it is very handy when a blueprint is behaving strangely and you want more than vibes.

## Compatibility

DTK is compatible with vanilla saves and can be added to or removed from existing saves safely.

Blueprints created or updated with DTK remain normal Captain of Industry blueprints. Blueprint consumers do not need DTK installed.

v0.1.0 is the starting point for a broader blueprint-authoring toolbox. The goal is to keep adding focused designer-side helpers that make building, documenting, inspecting, and sharing blueprints smoother without ever making blueprint users carry extra mod dependencies.
