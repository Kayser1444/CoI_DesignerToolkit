# Groundwater initialization contracts

Run from the DesignerToolkit repository:

```powershell
dotnet run --project tools/GroundwaterContracts -- '<game Managed directory>'
```

Links the production GroundwaterStatsManager source and uses the installed game
resource manager, prototype database and dependency resolver. Calendar and
lifecycle interfaces are controlled proxies; the logger captures warnings and
the state store supplies cached reservoir history. No game or save is opened.

Checks the previously failing new-world state (the resource index does not yet
exist), a loaded world's populated index, and an empty resource map. Discovery
must wait for InitState, emit no warnings, then restore history to the discovered
reservoir. Before the fix, the new-world fixture emits the reported null-reference
warning during Init. This verifies the initialization contract, not a live game.
