# Update 4.3 buffer contracts

Build DesignerToolkit in Debug first. This runner uses the actual game mixed
pile implementation and the private removal adapter from the compiled mod. It
creates isolated, unspawned entity fixtures; no game or save is opened.

```powershell
dotnet run --project tools/Update43Contracts -- '<game Managed directory>' '<absolute path to bin/Debug/net48/DesignerToolkit.dll>'
```

Checks selected-member removal, retained unit order, removal scope limits,
plain products, transient-buffer transfer quantities and regular/quick removal
for zippers, lifts, mini-zippers and sorters. The quick-removal fixtures verify
that returned products match the remaining inventory and pending input is empty.

On builds with the b624 mini-zipper reserve, also checks complete inventory and
order snapshots, reserve-sensitive display hashes, selective removal limits and
unit order, cached totals, quick-removal conservation/idempotence, and promotion
of a retained reserve when removal empties its input slot. Plain piles are tested
as well as mixed members. On b622 the reserve cases are skipped explicitly and
the input-only mini-zipper contracts still run.

To check an alternate 0.8.8 assembly set, build the mod against that set first
and pass `-p:ManagedPath='<alternate Managed directory>'` to this runner as well
as its runtime Managed argument. A pre-4.3 build requires the separate legacy
implementation and binary-reference checks; this runner uses mixed-pile APIs.
