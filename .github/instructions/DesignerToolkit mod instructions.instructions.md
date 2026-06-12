---
description: Load when working on DesignerToolkit mod code, making changes, or investigating bugs.
applyTo: 'src/**/*.cs'
---

# BDT-specific instructions

Read the shared workspace instructions first:

- `../../../.github/instructions/coi-maintained-mods.instructions.md`

# Build verification

For BDT-only changes, run:

```powershell
dotnet build DesignerToolkit.sln -c Debug
```

# Settings file

BDT uses `config.json` for user-configurable settings.

# Releases

For public releases, review `DesignerToolkit\docs\other\Hub page.md` to incorporate any new player-facing features.

# Core Principle

**Designer-only, consumer-free.** This mod targets blueprint *creators*. Blueprint *consumers* (players who place blueprints made by others) must never need this mod. Every feature — including any entity manipulation — must produce fully vanilla-compatible blueprint data. If a feature would require the consumer to also have the mod installed, it is out of scope.

# Features & Cheats

For every feature, scope of availability must be considered. If a feature provides an unfair advantage or could be considered a "cheat" in standard gameplay, it should be confined to **sandbox mode**.

**Important:** Sandbox mode can be enabled (but not disabled) from the console mid-session. Therefore, features must evaluate sandbox availability dynamically (e.g. when opening a panel) rather than caching the value only at mod initialization.
