# Normalize Symmetric Entities — Implementation Plan

## Problem

Rotationally-symmetric entities (Zipper/balancer, MiniZipper/connector) are
functionally identical in all orientations. When a player captures a blueprint
with a balancer at rotation 2 and another player has one at rotation 0, the
game treats these as different placements and refuses to paste one over the
other — a vanilla bug that the designer can work around by pre-normalizing.

Flip state has the same issue: a balancer is identical reflected vs. not, but
the stored `IsReflected` flag blocks paste-over.

Original Issue report: https://discord.com/channels/803508556325584926/1405800905646805093

## Goal

Automatically normalize the rotation and reflection of every
rotationally-symmetric entity in a blueprint to `(Rotation=0, IsReflected=false)`
at capture time, while preserving the semantics of the `PrioritizedPorts` array
by remapping port indices to match the canonical orientation.

## Approach: Harmony-patch `Blueprint` after construction, before serialization

Patch the point where the game produces a `Blueprint` object (e.g. after
`Blueprint.CreateFromEntities`) and walk every `EntityConfigData`. No live
world state is touched. The game serializes normally afterward, producing a
fully vanilla-compatible BP string.

A console command `dtk_normalize_bp` can apply the same pass to an already-
stored blueprint for retroactive fixes.

---

## Key types

| Type | Location | Used for |
|---|---|---|
| `TileTransform` | `Mafi.Core.TileTransform` | Stores `Position`, `Rotation90`, `IsReflected` |
| `Matrix2i` | `Mafi.Matrix2i` | 2×2 int transform; has `.Inverted()` and `.Transform(Vector2i)` |
| `Matrix2i.FromRotationFlip(rot, reflect)` | same | Builds the combined rot+flip matrix |
| `IoPortTemplate` | `Mafi.Core.Ports.Io.IoPortTemplate` | Has `RelativePosition: RelTile3i` |
| `LayoutEntityProto.Ports` | `Mafi.Core.Entities.Static.Layout` | Canonical ordered port list |
| `EntityConfigData.Transform` | `Mafi.Core.Entities.EntityConfigData` | Get/set via `TileTransform?` property |
| `ZipperConfigExtensions.GetPrioritizedPorts` | `Mafi.Core.Factory.Zippers` | Returns `ImmutableArray<bool>?` |
| `ZipperConfigExtensions.SetPrioritizedPorts` | same | Writes remapped array back |

---

## Symmetry detection (runtime, no hardcoded proto list)

For a given `LayoutEntityProto`, compute the XY multiset of canonical port
positions (ignoring Z — it's unaffected by rotation/reflection):

```csharp
// Get canonical XY port positions as a sorted multiset
IEnumerable<(int x, int y)> CanonicalPortXYs(LayoutEntityProto proto) =>
    proto.Ports
         .Select(p => (p.RelativePosition.X, p.RelativePosition.Y))
         .OrderBy(p => p.X).ThenBy(p => p.Y);
```

For each candidate normalization transform T (the 8 distinct rot/flip
combinations: 4 rotations × 2 reflections), compute what T does to each port
position and compare the resulting multiset to the canonical one. If **at least
two distinct non-identity** transforms produce the same multiset as identity →
the entity is symmetric and should be normalized.

```csharp
static bool IsSymmetric(LayoutEntityProto proto)
{
    var canonical = CanonicalPortXYs(proto).ToList();
    int matches = 0;
    for (int rot = 0; rot < 4; rot++)
    for (int flip = 0; flip < 2; flip++)
    {
        if (rot == 0 && flip == 0) continue; // skip identity
        var m = Matrix2i.FromRotationFlip(new Rotation90(rot), flip == 1);
        var transformed = proto.Ports
            .Select(p => {
                var v = m.Transform(new Vector2i(p.RelativePosition.X, p.RelativePosition.Y));
                return (v.X, v.Y);
            })
            .OrderBy(p => p.X).ThenBy(p => p.Y)
            .ToList();
        if (transformed.SequenceEqual(canonical)) matches++;
    }
    return matches > 0;
}
```

This automatically handles:
- 4-fold symmetric (balancer): 7 non-identity transforms all match → symmetric
- 2-fold symmetric (180° only): 3 matches → symmetric  
- Asymmetric: 0 matches → skip

---

## Port remapping algorithm

Given a symmetric entity with stored transform `(Rotation=R, IsReflected=F)` and
a `PrioritizedPorts` bool array indexed by canonical port order:

1. Build `M_stored = Matrix2i.FromRotationFlip(R, F)` — the matrix that was
   applied when the entity was placed.
2. Build `M_inv = M_stored.Inverted()` — maps physical positions back to
   canonical.
3. For each canonical port index `i`, transform its position forward through
   `M_stored`, then find which canonical port `j` has that same XY after
   applying `M_inv` back. This gives the permutation `perm[i] = j`.
4. Apply: `newPrioritized[perm[i]] = oldPrioritized[i]` for all `i`.

In practice this simplifies: for each stored port index `i`, the physical
position of port `i` under `M_stored` equals the physical position of canonical
port `perm[i]` under identity. So:

```csharp
static int[] BuildPortPermutation(LayoutEntityProto proto, Matrix2i mStored)
{
    var ports = proto.Ports;
    var canonicalPositions = ports.Select(p =>
        new Vector2i(p.RelativePosition.X, p.RelativePosition.Y)).ToArray();

    int[] perm = new int[ports.Length];
    for (int i = 0; i < ports.Length; i++)
    {
        // Physical position of canonical port i after stored transform
        var physPos = mStored.Transform(canonicalPositions[i]);
        // Find which canonical index j matches that physical position
        int j = Array.FindIndex(canonicalPositions, p => p == physPos);
        perm[i] = j; // canonical port i maps to canonical port j after normalization
    }
    return perm;
}
```

Then: `newPrioritized[perm[i]] = oldPrioritized[i]`.

---

## Normalization pass (pseudocode)

```csharp
void NormalizeEntity(EntityConfigData config, ProtosDb protosDb)
{
    var tf = config.Transform;
    if (tf == null) return;
    if (tf.Value.Rotation.AngleIndex == 0 && !tf.Value.IsReflected) return; // already canonical

    if (!protosDb.TryGetProto(config.ProtoId, out LayoutEntityProto proto)) return;
    if (!IsSymmetric(proto)) return;

    // Remap PrioritizedPorts before clearing transform
    var pp = config.GetPrioritizedPorts();
    if (pp.HasValue)
    {
        var mStored = Matrix2i.FromRotationFlip(tf.Value.Rotation, tf.Value.IsReflected);
        var perm = BuildPortPermutation(proto, mStored);
        var remapped = new bool[pp.Value.Length];
        for (int i = 0; i < pp.Value.Length; i++)
            remapped[perm[i]] = pp.Value[i];
        config.SetPrioritizedPorts(remapped.ToImmutableArray());
    }

    // Reset transform to canonical (position preserved, rot+flip zeroed)
    config.Transform = new TileTransform(tf.Value.Position, Rotation90.Deg0, false);
}
```

---

## Edge cases

- **Port count mismatch**: if `PrioritizedPorts.Length != proto.Ports.Length`,
  skip remapping (corrupted or from a different proto version). Leave ports
  unchanged, still normalize transform.
- **Z-offset ports**: `RelativePosition.Z` is ignored for the symmetry check
  and permutation (rotation/reflection only affects XY).
- **Permutation collision**: if two canonical ports share the same XY (rare),
  the `Array.FindIndex` above would match the first. Log a warning and skip
  port remapping for that entity.
- **Transport entities**: these have `Trajectory` not `Transform` — `tf` will
  be null, returns early. No action needed.
- **Non-layout entities**: `protosDb.TryGetProto` returns false → returns early.

---

## Scope

- Applies only on blueprint capture, not on blueprint paste/placement
- Does not modify live world entities
- Produces output identical to what the game would produce if the player had
  placed the entity at rotation 0 from the start
- Safe for BP books with mixed mod setups — unknown protos are skipped silently
