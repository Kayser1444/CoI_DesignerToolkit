# 🧰 Kayser's Blueprint Designer's Toolkit

*Formerly known as **Kayser's Designer's Toolkit**.*

Kayser's Blueprint Designer's Toolkit (BDT) is a quality-of-life mod for Captain of Industry blueprint creators.

It is built around one rule: **designer-only, consumer-free**. Players who download and use your blueprints do **not** need this mod installed. BDT helps with creating, updating, inspecting, and cleaning up blueprints, but the output remains normal vanilla-compatible blueprint data.

## ✨ Feature List

- [🔁 Update blueprint]
- [📂 Remembered blueprint folder]
- [📊 Blueprint operational stats]
- [📝 Copy as Markdown]
- [🧩 Symmetric entity normalization]
- [⚡ Instant build mode]
- [🧹 Transport cleanup tool]
- [👁️ Height filter]
- [⚙️ Mod settings]

### 🔁 Update blueprint

![update-blueprint.png](/content-images/b5ef0dc2ec2d4688e5e6ba65c08dcf67d831bc3e8b92312554fb003ec55bad2a/update-blueprint.png)

Select a blueprint in your blueprint book and click **Update** to replace its contents with a fresh area selection.

BDT keeps the blueprint's existing:

- name
- description
- overlap settings
- position in the current folder

This is meant for the usual blueprint-authoring loop: find a small mistake, fix it in-world, update the existing blueprint, and keep the book organized.

### 📂 Remembered blueprint folder

![remembered-blueprint-folder.png](/content-images/bd11e3f67062aa6a456a334fd50decd89667a29c4f4eda166bfff902f968fc91/remembered-blueprint-folder.png)

BDT remembers the last blueprint book folder you opened and restores it the next time the blueprint window is created. The folder path is stored in the vanilla save file. This allows you to store different currently open blueprint book page in different save games.

### 📊 Blueprint operational stats

The blueprint detail panel now also shows **Operational cost**.

![blueprint-operational-stats.png](/content-images/d476e2ab7976bb3130040d62a6d987f3bc9d95ddd0e116b5f20daf867f3e5190/blueprint-operational-stats.png)

When a selected blueprint contains relevant entities, BDT adds a compact operational summary row showing:

- workers
- electricity
- computing
- maintenance by tier

Only non-zero stats are shown, so small blueprints stay clean and large builds get the extra planning information where it belongs. Operational costs assume 100% utilization on all entities.

### 📝 Copy as Markdown

![image.png](/content-images/786bf604956dbf2eb157e4db3e6e7bc0f4c816bc95439d6a39fa5b6bf437c086/image.png)

BDT adds a **Copy as Markdown** button to both the blueprint detail panel and the blueprint folder detail panel.

The language used for table headers and product/entity names is controlled by the **Markdown table language** setting in Mod Settings (see below). The default is **English**.

**Single blueprint** - clicking the button copies a Markdown-formatted summary to the clipboard:

- Blueprint heading and description
- **Components** table - all major entity types and their counts, sorted A-Z
- **Construction** table - all required products and quantities, sorted A-Z
- **Operational** table - entities, workers, electricity, computing, and maintenance products per month

**Blueprint folder** - clicking the button copies a wide Markdown table listing every blueprint in the folder, including blueprints in sub-folders. Each blueprint is a row. Columns include Blueprint name, Folder (relative path within the exported root), Entities, and any workers / electricity / computing / maintenance / construction product columns present across the folder, sorted A-Z. Rows are sorted by folder path, then by blueprint name within each folder.

Example output (folder):

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

Rendered in markdown as:

## Kayser's Compact Concrete

Kayser's Compact Concrete The Compactest Concrete

https://hub.coigame.com/Blueprint/Detail/590

| Blueprint | Folder | Entities | Workers | Electricity | Maintenance I / mo | Concrete slab | Construction Parts | Construction Parts II |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Big Concrete (example) | . | 258 | 282 | 13.0 MW | 312 | 200 | 96 | 882 |
| Concrete Slab Stages (chart) | . | 579 | 504 | 17.4 MW | 549 | 280 | 1,3k | 2,1k |
| 1: Double T1 Mixer (24x) | Concrete Slabs | 33 | 16 | 550 kW | 20 | \- | 198 | 136 |

The output is ready to paste directly into a Hub post or wiki page.

### 🧩 Symmetric entity normalization

![symmetric-normalization-result.png](/content-images/b668e393f133cabe73642be461496f2eb16923445f79b6bafbd990aac5d67fbd/symmetric-normalization-result.png)

Mitigation/Fix for: https://discord.com/channels/803508556325584926/1405800905646805093/1405800905646805093

BDT normalizes rotationally-symmetric entities in captured blueprints, such as balancers/zippers and mini-zippers/connectors.

Captain of Industry can treat a functionally identical connector at e.g. rotation 0° and rotation 90° as different, which can block paste-over updates. BDT fixes that at blueprint capture time by resetting symmetric entities' rotation and flip-state to a canonical orientation.

The normalization pass focuses on the known paste-over problem cases:

- resets supported symmetric entities to a consistent stored orientation
- keeps their blueprint position unchanged
- preserves balancer priority settings
- skips entities that do not match the supported symmetric layouts

The result is still normal blueprint data with all (normalized) entities at rotation 0° and non-flipped. This does not patch blueprint placement and does *not* require blueprint users to install BDT.

This is a passive feature that allows you to freely place connectors, balancers, and lifts in multi-tier blueprints without worrying about their orientation.

### ⚡ Instant build mode

BDT includes an Instant Build mode (configurable in Mod Settings) that automatically and instantly completes construction, deconstruction, upgrades, and downgrades without consuming materials, workers, or unity. The benefit over the vanilla insta-build toggle is that you don't need to unpause the game and risk having products 

Enabling this feature turns off the game's built-in insta-build toggle.

Migrated from Moriarty's Utilities++, with permission. (Thanks!)

### 🧹 Transport cleanup tool

BDT adds a transport cleanup tool with a default hotkey of `Alt+Del`. This tool allows you to check for and demolish useless disconnected belts and pipes via an area selection drag, before capturing your blueprint or layout, or as a standalone tool.

The hotkey can be changed in BDT's mod settings under **TRANSPORT CLEANUP**.

Migrated from Moriarty's Utilities++, with permission. (Thanks!)

### 👁️ Height filter

BDT features a Height Filter rendering system that allows players to filter the visibility of transports, transport pillars, and layout entities (such as sorters, zippers, mini-zippers, and lifts) in the world. 

Adjusting the visible height levels makes it significantly easier to inspect and manage multi-tier pipe stacks, layered belt paths, or dense underground logistics without visual clutter.

- `PageUp`: increases the maximum visible level (up to level 6, which shows all heights).
- `PageDown`: decreases the maximum visible level (down to level 0, which shows underground entities only).

These hotkeys can be customized in BDT's mod settings under **HEIGHT FILTER**. Hidden entities are protected from selection to prevent accidental demolition or interaction.

Freely adopted from Moriarty's Utilities++ mod, with permission. (Thanks!)

### ⚙️ Mod settings

BDT adds a **Mod Settings** panel, accessible from the top-right **M** button in the mod menu or with the keyboard shortcut `Alt+M`.

![image.png](/content-images/d8738776e313704bf3ee98b162eef95068ce9f34d4ec0e6130cc5c36fc192984/image.png)

**Markdown table language** controls which language is used for table headers and product/entity names when copying Markdown:

| Mode | Behavior |
| --- | --- |
| **English** | English headers and names (default) |
| **Local** | Current game language |
| **Both** | English tables first, followed by local-language tables |
| **Hybrid** | Local text first, with English in parentheses where strings differ |

**Markdown number format** controls decimal and thousands separators in copied Markdown:

| Mode | Behavior |
|---|---|
| **Auto** | English tables use English separators; local and hybrid tables use the current game locale |
| **English separators** | Force English separators everywhere |
| **Local separators** | Force current-language separators everywhere |

Settings are stored per save file. The `markdown_table_language` and `markdown_number_format` keys in `config.json` set the initial values for saves that have no stored settings yet.

## 📌 Notes

- Compatible with vanilla saves.
- Can be added to or removed from existing saves.
- Requires Captain of Industry `0.8.2` or newer.
- Blueprint consumers do not need this mod installed.
- UI translations included for English, German, Spanish, Italian, Portuguese, Russian, Swedish, and Chinese.

## 📦 Installation

- Download the latest version of the mod from the Captain of Industry Hub.
- Extract the mod folder into your Captain of Industry mods directory (`%AppData%\Captain of Industry\Mods`).
- Enable the mod when loading or starting a new game.

## 📜 License

MIT. See [LICENSE](LICENSE).

## ⚖️ Attribution and trademarks

Designer Toolkit is an unofficial, community-made mod for Captain of Industry.

Captain of Industry, MaFi Games, and related names, trademarks, game code, and assets are the property of MaFi Games. This mod is not affiliated with, endorsed by, or sponsored by MaFi Games.

This repository is intended to contain only original mod code and configuration, licensed under the MIT License. It does not intentionally include Captain of Industry game code, game assets, or other MaFi Games intellectual property.
