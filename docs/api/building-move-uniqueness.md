# Building Move Uniqueness API

Mods can report whether BDT should wait for the original building to disappear before ordering construction at the destination. Use the exact runtime value of the building prototype's `Id.Value`.

```csharp
using CoIDesignerToolkit;

BuildingMoveApi.SetUniqueForMove("OtherMod.Building", isUnique: true);
BuildingMoveApi.SetUniqueForMove("OtherMod.Building", isUnique: false);
```

`true` applies BDT's unique move behavior. `false` withdraws the report and returns to the prototype's vanilla uniqueness rule. Report the current state on each world load and whenever the effective limit changes, such as when research is unlocked. BDT rechecks the rule when cutting the building, confirming the destination, and processing a queued move. Withdrawing a report makes BDT retry confirmed moves for that prototype on the next simulation pass. If the prototype is now ordinary and the destination passes normal placement checks, construction can be ordered while the original is still being demolished.

If your mod supplies a validator that counts existing buildings, use `BuildingMoveApi.TryGetUniqueMoveSource(proto.Id.Value, out EntityId source)` inside that validator. If it returns `true`, exclude only that source entity from the uniqueness count. The scope exists only during BDT's preflight, while the original still stands. During actual construction the query returns `false` and your normal rules apply.
