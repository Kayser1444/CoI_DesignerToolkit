# Designer Toolkit

Kayser's Designer Toolkit (BDT) is a quality-of-life mod for Captain of Industry blueprint creators.

It is built around one rule: **designer-only, consumer-free**. Players who download and use your blueprints do **not** need this mod installed. BDT helps with creating, updating, inspecting, and cleaning up blueprints, but the output remains normal vanilla-compatible blueprint data.

Download the latest release from the Captain of Industry Hub: https://coigame.com/Mods/Search?author=Kayser

## Features

### Update blueprint

![Update blueprint button](docs/assets/update-blueprint.png)

Select a blueprint in your blueprint book and click **Update** to replace its contents with a fresh area selection.

BDT keeps the blueprint's existing:

- name
- description
- overlap settings
- position in the current folder

This is meant for the usual blueprint-authoring loop: find a small mistake, fix it in-world, update the existing blueprint, and keep the book organized.

### Remembered blueprint folder

![Remembered blueprint folder](docs/assets/remembered-blueprint-folder.png)

BDT remembers the last blueprint book folder you opened and restores it the next time the blueprint window is created.

The folder path is stored in `config.json`. If a folder is renamed or removed, BDT gracefully falls back to the deepest folder it can still find.

### Blueprint operational stats

![Blueprint operational stats](docs/assets/blueprint-operational-stats.png)

The blueprint detail panel now separates **Construction cost** from **Operational cost**.

When a selected blueprint contains relevant entities, BDT adds a compact operational summary row showing:

- workers
- electricity
- computing
- maintenance by product

Only non-zero stats are shown, so small blueprints stay clean and large builds get the extra planning information where it belongs.

### Copy as Markdown

![Copy as Markdown button](docs/assets/copy-as-markdown.png)

BDT adds a **Copy as Markdown** button to both the blueprint detail panel and the blueprint folder detail panel.

**Single blueprint** - clicking the button copies a Markdown-formatted summary to the clipboard:

- Blueprint heading and description
- **Components** table - all major entity types and their counts, sorted A-Z
- **Construction** table - all required products and quantities, sorted A-Z
- **Operational** table - entities, workers, electricity, computing, and maintenance products per month

**Blueprint folder** - clicking the button copies a wide Markdown table listing every blueprint in the folder, including blueprints in sub-folders. Each blueprint is a row. Columns include Blueprint name, Folder (relative path within the exported root), Entities, and any workers / electricity / computing / maintenance / construction product columns present across the folder, sorted A-Z. Rows are sorted by folder path, then by blueprint name within each folder.

Markdown export settings let you choose English, local, bilingual, or hybrid names, and separately choose automatic, English, or local number separators.

Example output:

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

The output is ready to paste directly into a Hub post or wiki page.

### Symmetric entity normalization

Mitigation/Fix for: https://discord.com/channels/803508556325584926/1405800905646805093/1405800905646805093

![Symmetric normalization result](docs/assets/symmetric-normalization-result.png)

BDT normalizes rotationally-symmetric entities in captured blueprints, such as balancers/zippers and mini-zippers/connectors.

Captain of Industry can treat a functionally identical balancer at rotation 0 and rotation 2 as different, which can block paste-over updates. BDT fixes that at blueprint capture time by resetting symmetric entity rotation and reflection to a canonical orientation.

The normalization pass focuses on the known paste-over problem cases:

- resets supported symmetric entities to a consistent stored orientation
- keeps their blueprint position unchanged
- preserves balancer priority settings where BDT can safely remap them
- skips entities that do not match the supported symmetric layouts

The result is still normal blueprint data. This does not patch blueprint placement and does not require blueprint users to install BDT.

## Notes

- Compatible with vanilla saves.
- Can be added to or removed from existing saves.
- Requires Captain of Industry `0.8.2` or newer.
- Blueprint consumers do not need this mod installed.

## Installation

- Download the latest version of the mod from the Captain of Industry Hub.
- Extract the mod folder into your Captain of Industry mods directory (`%AppData%\Captain of Industry\Mods`).
- Enable the mod when loading or starting a new game.

## Build from source

- Install the .NET SDK with .NET Framework 4.8 targeting support.
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory.
- Run `./build.ps1 -Configuration Release`.
- The release zip is created in the project root.

## License

MIT. See [LICENSE](LICENSE).

## Attribution and trademarks

Designer Toolkit is an unofficial, community-made mod for Captain of Industry.

Captain of Industry, MaFi Games, and related names, trademarks, game code, and assets are the property of MaFi Games. This mod is not affiliated with, endorsed by, or sponsored by MaFi Games.

This repository is intended to contain only original mod code and configuration, licensed under the MIT License. It does not intentionally include Captain of Industry game code, game assets, or other MaFi Games intellectual property. If any such material is found to have been included by mistake, I intend to correct it promptly upon discovery or notice.
