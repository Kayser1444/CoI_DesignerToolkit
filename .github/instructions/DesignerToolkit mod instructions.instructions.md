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
