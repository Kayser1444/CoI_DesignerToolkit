---
description: Load when working on DesignerToolkit mod code, making changes, or investigating bugs.
applyTo: 'src/**/*.cs'
---

# DTK-specific instructions

Read the shared workspace instructions first:

- `../../../.github/instructions/coi-maintained-mods.instructions.md`

# Build verification

For DTK-only changes, run:

```powershell
dotnet build DesignerToolkit.sln -c Debug
```

# Settings file

DTK uses `config.json` for user-configurable settings.
