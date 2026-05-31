# Roadmap

Planned and candidate improvements for CoI Designer Toolkit.

## Core principle

**Designer-only, consumer-free.** This mod targets blueprint *creators*. Blueprint *consumers* (players who place blueprints made by others) must never need this mod. Every feature — including any entity manipulation — must produce fully vanilla-compatible blueprint data. If a feature would require the consumer to also have the mod installed, it is out of scope.

## Done

### Blueprint management
* ~~Update blueprint button~~
    - As a user, I want a convenient method to update an existing blueprint in my blueprint book, so that I can correct minor mistakes easily.

### Other
* ~~Persist the current open folder (in the blueprint book) to our config.json~~

### Blueprint information
* ~~Add info about max workers, electricity, computing (TF), maintenance (1 vs 2 vs 3).~~
    - Summary row injected into the Detail panel; shows workers, electricity, computing, and maintenance (grouped by tier). Only non-zero stats are displayed.

* ~~Export single BP details to clipboard as Markdown~~
    - "Copy as Markdown" button injected into the blueprint Detail panel. Copies paste-ready Markdown: description, Components table, Construction cost table, and Operational stats table. Suitable for CoI Hub posts.

* ~~Export BP folder's all BPs details to clipboard~~
    - "Copy as Markdown" button injected into the blueprint folder Detail panel. Copies a wide Markdown table of all blueprints in the folder tree (recursive), with one row per blueprint, a Folder column for the relative sub-folder path, and dynamically discovered stat/cost columns sorted A-Z.

## Planned

### Blueprint management
* Blueprint editor?

### Blueprint information

### Entities
* Buildable U1 transports
* Sources and sinks rate limiters
* Throughput limiter on transports

### Other
* AoE normalize/align entities (normalize rotate, flip to same alignment)
* Blueprint terrain designations?
* Normalize direction (rotate BP)
* Paste all BPs in folder at the same time
* Global settings: locale for markdown output
* 'Recycle Bin' for deleted and/or updated blueprints - potentially with retention policy
* Translation