# Normalize Symmetric Entities — Design Reference

> Status: in-progress  
> Verified against game version: EA 0.6.x (May 2026)  
> Port data sourced from: live `[PORTDUMP]` log lines

---

## Problem statement

When a player blueprints an entity and pastes it over an existing entity of the same type, CoI compares the stored `(proto_id, rotation, reflected)` tuple. For entities that are functionally equivalent at multiple orientations (balancers, connectors), a mismatch in the stored rotation causes the paste-over to be treated as incompatible, even when the entity is the same type at the same world location.

The fix is to normalize blueprinted entities to a canonical `(rotation=0, reflected=false)` form at blueprint-capture time, remapping `PrioritizedPorts` indices so the player's intent is preserved.

---

## Entity categories

| Category | Examples | Action |
|---|---|---|
| 1 — Canonical | Any entity already at (0°, false) | No-op |
| 2 — Fully symmetric | Connector, Lift | Any orientation → canonical; no port remap needed (all rotations produce same port-position multiset) |
| 3 — Normalizable with remap | Balancer (all 4 vanilla types) | Any of 8 orientations → canonical with hard-coded port remap |

**Category 2 detection:** runtime — `GetSortedPortXYZDs(ports, R, F) == GetSortedPortXYZDs(ports, 0°, false)`. If true, all orientations are equivalent and no remap is needed.

**Category 3 detection:** hard-coded proto ID whitelist (see §Implementation). General programmatic remap for arbitrary entities is roadmap.

---

## Vanilla balancer — canonical port layout

All four vanilla balancer types share the **same** port layout:

- `Zipper_IoPortShape_FlatConveyor`
- `Zipper_IoPortShape_LooseMaterialConveyor`
- `Zipper_IoPortShape_Pipe`
- `Zipper_IoPortShape_MoltenMetalChannel`

**Footprint:** 2 × 2 tiles. Origin at canonical SW corner.

```
              north (+Y)
         ↑D      ↑C
    E← ┌──────────┐ →B
       │  (0,1)  (1,1) │
       │              │
       │  (0,0)  (1,0) │
    F← └──────────┘ →A
         ↓G      ↓H
              south (-Y)
```

**Port table (canonical, rot=0, reflected=false):**

| idx | name | tile (X,Y) | Z | direction | dir_idx |
|-----|------|------------|---|-----------|---------|
| 0   | D    | (0, 1)     | 0 | +Y north  | 1       |
| 1   | C    | (1, 1)     | 0 | +Y north  | 1       |
| 2   | E    | (0, 1)     | 0 | −X west   | 2       |
| 3   | B    | (1, 1)     | 0 | +X east   | 0       |
| 4   | F    | (0, 0)     | 0 | −X west   | 2       |
| 5   | A    | (1, 0)     | 0 | +X east   | 0       |
| 6   | G    | (0, 0)     | 0 | −Y south  | 3       |
| 7   | H    | (1, 0)     | 0 | −Y south  | 3       |

Each corner tile hosts 2 ports, one facing each of its two outward cardinal directions. All 8 ports face away from the entity centre — none face inward.

Corner → port pair:
- SW (0,0): F faces −X (west), G faces −Y (south)
- SE (1,0): A faces +X (east), H faces −Y (south)
- NW (0,1): D faces +Y (north), E faces −X (west)
- NE (1,1): C faces +Y (north), B faces +X (east)

---

## Rotation matrix convention

`Matrix2i.FromRotationFlip(rotation, reflectX)` — from decompiled source:

```
rot=0,  ref=F: [[  1,  0], [ 0,  1]]   transform: (x,y) → ( x,  y)
rot=90, ref=F: [[  0, -1], [ 1,  0]]   transform: (x,y) → (-y,  x)
rot=180,ref=F: [[ -1,  0], [ 0, -1]]   transform: (x,y) → (-x, -y)
rot=270,ref=F: [[  0,  1], [-1,  0]]   transform: (x,y) → ( y, -x)

rot=0,  ref=T: [[ -1,  0], [ 0,  1]]   transform: (x,y) → (-x,  y)
rot=90, ref=T: [[  0, -1], [-1,  0]]   transform: (x,y) → (-y, -x)
rot=180,ref=T: [[  1,  0], [ 0, -1]]   transform: (x,y) → ( x, -y)
rot=270,ref=T: [[  0,  1], [ 1,  0]]   transform: (x,y) → ( y,  x)
```

Direction vectors: +X=(1,0) dir_idx=0, +Y=(0,1) dir_idx=1, −X=(−1,0) dir_idx=2, −Y=(0,−1) dir_idx=3.

---

## Tile-coordinate offset for a 2×2 entity

After applying matrix M to the 4 canonical tile offsets {(0,0),(1,0),(0,1),(1,1)}, the results may include negative coordinates. The **offset** is the negation of the minimum transformed coordinate in each axis, and must be added to the transformed port positions before matching against canonical positions.

| orientation     | offset (Ox, Oy) |
|-----------------|-----------------|
| rot=0,   ref=F  | (0, 0)          |
| rot=90,  ref=F  | (1, 0)          |
| rot=180, ref=F  | (1, 1)          |
| rot=270, ref=F  | (0, 1)          |
| rot=0,   ref=T  | (1, 0)          |
| rot=90,  ref=T  | (1, 1)          |
| rot=180, ref=T  | (0, 1)          |
| rot=270, ref=T  | (0, 0)          |

The general remap algorithm: for stored orientation M with offset O:

1. For each canonical port `i` at `(cx, cy, cz, cd_vec)`:
   - `phys_xy = M.Transform((cx, cy)) + O`
   - `phys_dir_idx = VecToDirectionIndex(M.Transform(cd_vec))`
2. Look up canonical port `j` where `ports[j]` has position `(phys_xy.X, phys_xy.Y, cz)` and direction index `phys_dir_idx`
3. `newPP[j] = oldPP[i]`

For **hard-coded tables** (Category 3 below) the runtime lookup is replaced by a precomputed array.

---

## Remap tables — vanilla balancer

`newPP[i] = oldPP[ table[i] ]` — read across each row.

> Verified by manual derivation from port-dump log data and `Matrix2i` source.

| Orientation     | [0] | [1] | [2] | [3] | [4] | [5] | [6] | [7] |
|-----------------|-----|-----|-----|-----|-----|-----|-----|-----|
| rot=0,   ref=F  |  0  |  1  |  2  |  3  |  4  |  5  |  6  |  7  | ← identity (no-op) |
| rot=90,  ref=F  |  3  |  5  |  1  |  7  |  0  |  6  |  2  |  4  |
| rot=180, ref=F  |  7  |  6  |  5  |  4  |  3  |  2  |  1  |  0  | ← full reverse |
| rot=270, ref=F  |  4  |  2  |  6  |  0  |  7  |  1  |  5  |  3  |
| rot=0,   ref=T  |  1  |  0  |  3  |  2  |  5  |  4  |  7  |  6  | ← swap adjacent pairs |
| rot=90,  ref=T  |  2  |  4  |  0  |  6  |  1  |  7  |  3  |  5  |
| rot=180, ref=T  |  6  |  7  |  4  |  5  |  2  |  3  |  0  |  1  |
| rot=270, ref=T  |  5  |  3  |  7  |  1  |  6  |  0  |  4  |  2  |

**Notable patterns:**
- `rot=180, ref=F`: full reversal — each port swaps with its diagonally opposite port (D↔H, C↔G, E↔A, B↔F).
- `rot=0, ref=T`: adjacent pairs swap — left-column ports swap with right-column counterparts (D↔C, E↔B, F↔A, G↔H).
- `rot=90` and `rot=270` are inverses of each other (applying 90° remap then 270° remap yields identity).
- `rot=270, ref=T` has zero offset, so it's the only case where the current runtime lookup (without offset) would work correctly if it weren't blocked by the symmetry check.

---

## Visual layouts at each stored orientation

Each diagram shows which port name appears at each physical side of the entity as stored.

**rot=90, ref=F** — entity rotated 90° CCW:
```
         ←E  ←D      (west side)
    G↓  ┌──────┐  ↑B
    H↓  └──────┘  ↑A
         →F  →C      (east side — but dirs are now south/north)
```
More precisely: physical north ports are B,A; physical south are G,H; physical west are D,C (both facing -X); physical east are F,E... 

Actually cleaner to just describe port-side mapping:

| Physical side | Ports visible | Port names |
|---|---|---|
| North (+Y)  | canonical RIGHT  | A(5), B(3) |
| South (−Y)  | canonical LEFT   | G(6), H(7) → wait |

> TODO: draw ASCII diagrams for all 7 remaining orientations.

---

## Position normalization

For a 2×2 entity:
- Rotating the entity in-place keeps the same 4 world tiles covered.
- The game adjusts the stored `TileTransform.Position` when the player rotates, so that `position + M.Transform((0,0))` always equals the world position of canonical tile (0,0) of the entity. Since `M.Transform((0,0)) = (0,0)` for any linear matrix, the stored position always equals the world position of canonical tile (0,0).
- Therefore, **no position adjustment is needed** when normalizing rotation to (0°, false) — the stored position already represents the entity's canonical origin in both the stored and normalized forms.

> ⚠️ This assumption needs game testing: blueprint a 2×2 balancer at multiple rotations, compare the stored positions in the blueprint file to confirm they refer to the same world tile.

---

## Implementation plan

### Phase 1 (current): symmetry-based normalization (Category 2 only)

Current code in `DTK.NormalizeSymmetric.cs`:
- `IsStoredTransformASymmetry` → correct for Cat. 2; blocks Cat. 3
- `RemapPrioritizedPorts` → works for symmetries (offset=0); fails for non-symmetric orientations due to missing tile-coordinate offset

### Phase 2 (next): hard-coded balancer normalization (Category 3)

1. Add balancer proto ID set:
   ```csharp
   private static readonly HashSet<string> s_balancerProtoIds = new HashSet<string>
   {
       "Zipper_IoPortShape_FlatConveyor",
       "Zipper_IoPortShape_LooseMaterialConveyor",
       "Zipper_IoPortShape_Pipe",
       "Zipper_IoPortShape_MoltenMetalChannel",
   };
   ```

2. In `TryNormalize`: after the Cat. 2 check fails, check if proto is in the balancer set. If yes, look up the hard-coded remap table for `(rotation.AngleIndex, isReflected)` and apply.

3. Encode the 8 remap tables as `int[][]` indexed by `[angleIndex * 2 + (reflected ? 1 : 0)]`.

4. Remove the `IsStoredTransformASymmetry` gate for whitelisted protos (they're always normalizable; Cat. 2 entities are a special case where the identity remap is fine for any rotation, handled first).

### Phase 3 (roadmap): modder API

Provide a registration method:
```csharp
NormalizeSymmetric.RegisterRemapTable(Proto.ID protoId, int[][] remapByOrientation);
```
Where `remapByOrientation[angleIndex * 2 + (reflected?1:0)]` is the 8-element remap array.

The algorithm derivation (matrix + offset) can also be exposed as a utility for modders who want to compute the table programmatically from their entity's port layout.

---

## Open questions

1. **Position normalization (see above):** needs game test to confirm stored positions are orientation-invariant for 2×2 entities.
2. **Non-square entities:** a 2×3 entity has a different footprint at rot=90 vs rot=0. If such an entity were symmetric, the position WOULD need adjustment. Out of scope for vanilla balancers.
3. **Larger balancers / mod entities:** may have different port layouts. The modder API (Phase 3) handles this.
4. **Verify remap tables in-game:** test all 7 non-canonical orientations, check that PrioritizedPorts survive round-trip correctly.
