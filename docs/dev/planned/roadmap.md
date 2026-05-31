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

## Planned

### Blueprint management
* Blueprint editor?

### Blueprint information
* Add stuff like workers, electricity, computing, etc.
* Export information to .md? sync to a library?

### Entities
* Buildable U1 transports
* Sources and sinks rate limiters
* Throughput limiter on transports

### Other
* AoE normalize/align entities (normalize rotate, flip to same alignment)
* Blueprint terrain designations?
* Normalize direction (rotate BP)