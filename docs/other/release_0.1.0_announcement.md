# 🧰 Kayser's Designer Toolkit v0.1.0

This is the first public release of Kayser's Designer Toolkit (BDT), a toolbox for Captain of Industry blueprint creators who spend too much time carefully arranging things and then discover one tiny mistake after the blueprint is already in the book.

The short version: BDT lets you update an existing blueprint from a new area selection, remembers where you were in the blueprint book in each save, adds operational cost stats and Markdown export for documentation, and normalizes symmetric entities so paste-over behaves better.

## 🧭 Designer-Only, Consumer-Free

BDT is built for blueprint authors, not blueprint users.

Players who download and use blueprints made with BDT do **not** need this mod installed. The mod only helps during creation and editing. The saved blueprint data remains standard Captain of Industry blueprint data.

That makes BDT safe to use for public blueprint packs, shared designs, and normal workshop-style authoring workflows.

## 🔁 Update Existing Blueprints

The blueprint window now has an **Update** button.

Select an existing blueprint, click **Update**, choose a new area, and BDT replaces the blueprint contents while preserving the parts you usually wanted to keep:

- name
- description
- overlap settings
- position in the current folder

This is the main quality-of-life feature for v0.1.0. It turns "I need to remake and refile this blueprint" into "fix it in-world and update the existing entry."

## 📂 Blueprint Book Memory

BDT remembers the last blueprint book folder you opened and restores it when the blueprint window is created again.

The path is saved in `config.json`. If a folder was renamed or removed, BDT falls back to the deepest matching folder it can still find, so it should fail quietly instead of getting in the way.

## 📊 Operational Cost Summary

The blueprint detail panel now splits vanilla construction cost from an added **Operational cost** summary.

When the selected blueprint contains relevant entities, BDT shows compact tiles for:

- workers
- electricity
- computing
- maintenance by product

Only non-zero values appear. Small blueprints stay uncluttered, and large factory blocks get a better at-a-glance read before you stamp them into a real build.

## 📝 Copy as Markdown

BDT adds **Copy as Markdown** buttons to the blueprint detail panel and the blueprint folder detail panel.

For a single blueprint, the copied text includes the blueprint heading and description, followed by Markdown tables for:

- components
- construction products
- operational stats

For a folder, BDT copies a recursive table of every blueprint under that folder. Each blueprint gets one row, with a relative folder path and dynamically discovered columns for workers, electricity, computing, maintenance, and construction products.

For example, a folder export can start like this:

```markdown
## Kayser's Compact Concrete

Kayser's Compact Concrete
The Compactest Concrete

https://hub.coigame.com/Blueprint/Detail/590

| Blueprint | Folder | Entities | Workers | Electricity | Maintenance I / mo | Concrete slab | Construction Parts | Construction Parts II |
|---|---|---|---|---|---|---|---|---|
| Big Concrete (example) | . | 258 | 282 | 13.0 MW | 312 | 200 | 96 | 882 |
| Concrete Slab Stages (chart) | . | 579 | 504 | 17.4 MW | 549 | 280 | 1,3k | 2,1k |
| 1: Double T1 Mixer (24x) | Concrete Slabs | 33 | 16 | 550 kW | 20 | - | 198 | 136 |
```

The output is meant to be pasted directly into Hub posts, wiki pages, release notes, or planning notes without hand-building the same tables again.

## 🧩 Symmetric Entity Normalization

BDT fixes a particularly annoying blueprint authoring edge case around rotationally-symmetric entities.

Some entities, such as balancers/zippers and mini-zippers/connectors, are functionally identical in multiple rotations or reflection states. Captain of Industry can still store those orientations differently in blueprint data, which may prevent paste-over updates even when the placed entity is effectively the same.

BDT normalizes those symmetric entities at blueprint capture time:

- supported symmetric entities are saved with a consistent orientation
- blueprint positions stay unchanged
- balancer priority settings are preserved where BDT can safely remap them
- unsupported or asymmetric entities are skipped

The important bit: this changes only the captured blueprint data. It does not modify live world entities, does not patch blueprint placement, and does not create a dependency for players using the blueprint later.

## 📦 Compatibility

BDT is compatible with vanilla saves and can be added to or removed from existing saves safely.

Blueprints created or updated with BDT remain normal Captain of Industry blueprints. Blueprint consumers do not need BDT installed.

v0.1.0 is the starting point for a broader blueprint-authoring toolbox. The goal is to keep adding focused designer-side helpers that make building, documenting, inspecting, and sharing blueprints smoother without ever making blueprint users carry extra mod dependencies.
