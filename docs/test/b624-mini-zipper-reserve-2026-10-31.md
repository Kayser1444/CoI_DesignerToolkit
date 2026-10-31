# b624 mini-zipper reserve verification — 2026-10-31

Target: Captain of Industry 0.8.8 experimental b624. This record covers the
reserve-removal repair, not a general compatibility certification.

## Regression and implementation

The pre-fix compiled adapter failed the permanent `Update43Contracts` reserve
fixture: its inventory omitted the native three-unit reserve. After the fix,
the fixture passes mixed-member/plain inventory, display hashes, order snapshots,
selective-removal limits and retained unit order, cached totals, direct quick
removal, repeated quick removal, and promotion of a retained reserve into an input
emptied by removal. The original zipper/lift/mini-zipper/sorter contracts also pass,
including new plain-pile cases for all four adapters.

The optional b624 fields are discovered by reflection in the Update 4.3 branch;
b622 keeps the input-only path. Pre-4.3 code is unchanged. This run does not
retest complete b622/0.8.7d assembly sets.

Run from DesignerToolkit:

```powershell
dotnet build DesignerToolkit.sln -c Debug
dotnet run --project tools/Update43Contracts -- '<game Managed>' '<absolute bin/Debug/net48/DesignerToolkit.dll>'
```

## Live fixture

Used a separately named `Codex-b624-reserve-verification.save`, based on the
owner's Strategic Marina save. The original save was backed up and never
overwritten. Autosaving was disabled only in the disposable test sessions.

A temporary `BdtReserveProbe` was compiled through a command-line-only MSBuild
import from the private workspace's ignored `.local/b624-verification/probe/`.
It creates a validated native connector and seeds native buffers/statistics;
it introduces no mod prototypes or saved fixture state. Assertions call the
actual compiled adapter and native game methods. These are controlled state
tests, not extended organic logistics playtesting.

Confirmed observations for connector 56:

| Operation | Input | Reserve | Native/adapter total | A/B members |
|---|---:|---:|---:|---|
| Seed and save | 3 | 3 | 6/6 | 4/2 |
| Fresh process reload | 3 | 3 | 6/6 | 4/2 |
| Remove three A units | 1 | 2 | 3/3 | 1/2 |
| Remove the last input B unit | 2 | 0 | 2/2 | 1/1 |
| Quick clear | 0 | 0 | 0/0 | 0/0 |
| Repeat quick clear | 0 | 0 | 0/0 | 0/0 |
| Native receive after clear | 1 | 0 | 1/1 | 1/0 |

Reserve port ownership was reset to -1 on promotion/clear. A freshly seeded
six-unit connector (105) also cleared directly to zero and remained empty on
a repeated clear. Offline store-proxy contracts additionally assert exact
returned quantities and prevent duplication.

The initial temporary harness used the visual name rather than the native
prototype ID; this was corrected to `MiniZip_IoPortShape_LooseMaterialConveyor`.
An initial sending attempt lacked an incoming port connection, so native
SimUpdate correctly left that input untouched. Those harness failures were
not production-removal failures.

Native onward transfer also passed. In log
`26-10-31_11-27-57_5277.log`,
the promoted two-unit input was connected to native source and sink entities.
The sink's separate source/sink-cheat flag was enabled in the disposable world;
sandbox conversion alone had left it disabled. Native `MiniZipper.SimUpdate`
sent both units: `moved=2 consumed=2`, leaving input/reserve/native/adapter totals
at zero. Both source and sink were enabled, with one connected input and output.

## Final build and live loading

The ordinary Debug build (without the temporary MSBuild import) succeeded with
zero warnings/errors. All removal contracts and both installed-member and
version-selector payload reference checks passed. ILSpy class listing confirmed
the final DLL contains no `BdtReserveProbe`. The built, installed and selected
update-4.3 DLLs have identical SHA-256
`CA2F661772AA735F72334104A0C7D3173E72E052BB15C36C542A2D25BAD3FCE2`.

A fresh final process loaded the native reserve save with the diagnostic type
absent. Log `26-10-31_11-33-03_9001.log` confirms BDT v0.11.6 with DLL timestamp
`2026-10-31 11:31:49`; its ordinary removal command was registered. This proves
the test save did not require the temporary probe type.

An in-process reload excluding BDT completed and its removal command was absent.
A nonfatal settings-panel resolver assertion occurred during that transition;
this is not an assertion-free reload certification. An attempted fresh-process
launch with BDT's manifest temporarily held aside was rejected by vanilla's
`--launch-last-game` loader: it requires the save's listed mod to be available
and failed before deserialization (`Mod 'DesignerToolkit' was not found`). This
does not establish a save incompatibility; the supported explicit mod-list
override did load. The exact manifest was restored in a finally block, checked
by hash. No serializer,
prototype, manifest-version or verified-version metadata was changed by this fix.

After testing, the game was closed normally and the separately named test save
was archived into the private verification folder, leaving the owner's original
save as the newest ordinary save. Its SHA-256 matches the pre-test backup. The
installed DLL is the final ordinary build, with no temporary diagnostic type.

Detailed raw observations remain in the private workspace
`.local/b624-verification/` and the named game logs.
