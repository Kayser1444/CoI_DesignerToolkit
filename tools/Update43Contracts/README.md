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
