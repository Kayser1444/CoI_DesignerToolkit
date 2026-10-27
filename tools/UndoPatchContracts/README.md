# Undo hook contracts

Installs each of BDT's four undo hook groups against the actual game assemblies.
Calls Harmony's class processor directly so the production error handler cannot
hide a missing target. This reproduced Update 4.3's added `keepPendingRefund`
argument on `ConstructionManager.StartDeconstruction` before the fix.

Run from the mod root:

```powershell
dotnet run --project tools/UndoPatchContracts -- . 'C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry\Captain of Industry_Data\Managed'
```

Uses .NET Framework 4.8 for the game's Harmony runtime. It does not run the game
or exercise an undo operation; it verifies hook installation.
