// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Ports.Io;
using Mafi.Core.Prototypes;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Normalizes the orientation of rotationally-symmetric entities (e.g. Zippers,
/// MiniZippers) in blueprints to (Rotation=0, IsReflected=false) at capture time.
/// This prevents paste-over mismatches caused by the game treating different
/// orientations of a functionally-identical entity as incompatible.
///
/// Symmetry is detected at runtime from port positions — no hardcoded proto list
/// is required, so mod-added variants are handled automatically.
///
/// PrioritizedPorts indices are remapped to match the canonical orientation so
/// the priority intent is preserved exactly.
/// </summary>
internal static class NormalizeSymmetric
{
    private static readonly ModLogger s_log = new ModLogger("DTK.NormSym");

    // Cache symmetry test results per (proto, rotation index, reflected) to avoid re-testing.
    private static readonly Dictionary<(Proto.ID, int, bool), bool> s_symmetryCache =
        new Dictionary<(Proto.ID, int, bool), bool>();

    // Vanilla balancer proto IDs — all share the same 2×2 8-port layout.
    private static readonly HashSet<string> s_balancerProtoIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "Zipper_IoPortShape_FlatConveyor",
        "Zipper_IoPortShape_LooseMaterialConveyor",
        "Zipper_IoPortShape_Pipe",
        "Zipper_IoPortShape_MoltenMetalChannel",
    };

    // Hard-coded port remap tables for the vanilla balancer.
    // Index: angleIndex * 2 + (reflected ? 1 : 0).
    // Usage: newPP[j] = oldPP[table[j]]
    // Derived from Matrix2i.FromRotationFlip + tile offset + port lookup.
    // Verified against [PORTDUMP] log data (2026-05-31).
    private static readonly int[][] s_balancerRemapTables = new int[][]
    {
        new[] { 0,1,2,3,4,5,6,7 }, // [0] rot=0,   ref=F — identity (unreachable after early-return)
        new[] { 1,0,3,2,5,4,7,6 }, // [1] rot=0,   ref=T
        new[] { 3,5,1,7,0,6,2,4 }, // [2] rot=90,  ref=F
        new[] { 2,4,0,6,1,7,3,5 }, // [3] rot=90,  ref=T
        new[] { 7,6,5,4,3,2,1,0 }, // [4] rot=180, ref=F
        new[] { 6,7,4,5,2,3,0,1 }, // [5] rot=180, ref=T
        new[] { 4,2,6,0,7,1,5,3 }, // [6] rot=270, ref=F
        new[] { 5,3,7,1,6,0,4,2 }, // [7] rot=270, ref=T
    };

    // Diagnostic probe: log canonical port layout once per proto, then never again.
    private static readonly HashSet<Proto.ID> s_portsDumped = new HashSet<Proto.ID>();
    private static void DumpPortsOnce(LayoutEntityProto proto)
    {
        if (!s_portsDumped.Add(proto.Id)) return;
        var sb = new System.Text.StringBuilder();
        sb.Append($"[PORTDUMP] {proto.Id}  ports={proto.Ports.Length}");
        for (int i = 0; i < proto.Ports.Length; i++)
        {
            var p = proto.Ports[i];
            sb.Append($"  [{i}] pos=({p.RelativePosition.X},{p.RelativePosition.Y},{p.RelativePosition.Z}) dir={p.RelativeDirection.DirectionIndex}({DirName(p.RelativeDirection.DirectionIndex)}) type={p.Type} name={p.Name}");
        }
        s_log.Info(sb.ToString());
    }
    private static string DirName(int d) => d == 0 ? "+X" : d == 1 ? "+Y" : d == 2 ? "-X" : "-Y";

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var method = typeof(BlueprintsLibrary).GetMethod(
                "TryCreateBlueprint",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                s_log.Warning("TryCreateBlueprint not found — skipping NormalizeSymmetric.");
                return;
            }
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(NormalizeSymmetric), nameof(TryCreateBlueprintPrefix)));
            s_log.Info("Patched BlueprintsLibrary.TryCreateBlueprint for symmetric normalization.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "NormalizeSymmetric.ApplyPatches");
        }
    }

    // Harmony prefix — runs before TryCreateBlueprint.
    // EntityConfigData is a mutable class; mutating instances in-place is safe.
    private static void TryCreateBlueprintPrefix(ImmutableArray<EntityConfigData> items)
    {
        foreach (var config in items)
        {
            TryNormalize(config);
        }
    }

    private static void TryNormalize(EntityConfigData config)
    {
        var tf = config.Transform;
        if (tf == null) return;

        if (!(config.Prototype.ValueOrNull is LayoutEntityProto proto)) return;
        DumpPortsOnce(proto);

        // Already canonical — nothing to do.
        if (tf.Value.Rotation == Rotation90.Deg0 && !tf.Value.IsReflected) return;

        string rotLabel = $"rot={tf.Value.Rotation.AngleIndex * 90}° reflected={tf.Value.IsReflected}";

        // Category 3: vanilla balancers — always normalizable via hard-coded remap table.
        if (s_balancerProtoIds.Contains(proto.Id.ToString()))
        {
            TryNormalizeBalancer(config, proto, tf.Value, rotLabel);
            return;
        }

        // Category 4: lifts — reflection-symmetric only; just clear the flip, keep rotation.
        // Covers all tiers/variants via prefix match. No port remap needed.
        if (proto.Id.ToString().StartsWith("LiftIoPortShape_", StringComparison.Ordinal))
        {
            if (tf.Value.IsReflected)
            {
                config.Transform = new TileTransform(tf.Value.Position, tf.Value.Rotation, false);
                s_log.Info($"[NormSym] {proto.Id} @ {rotLabel}: lift — cleared reflection, rotation unchanged.");
            }
            return;
        }

        // Category 2: only normalize if this specific transform is a port-layout symmetry.
        // A 2-fold-only entity at 90° rotation is a genuinely different orientation.
        if (!IsStoredTransformASymmetry(proto, tf.Value.Rotation, tf.Value.IsReflected))
        {
            s_log.Info($"[NormSym] {proto.Id} @ {rotLabel}: stored transform is NOT a symmetry — skipping.");
            return;
        }

        var mStored = Matrix2i.FromRotationFlip(tf.Value.Rotation, tf.Value.IsReflected);

        // Log canonical port positions before remap.
        var ports = proto.Ports;
        var portPosLog = new System.Text.StringBuilder();
        for (int i = 0; i < ports.Length; i++)
        {
            var p = ports[i].RelativePosition;
            portPosLog.Append($"  port[{i}]=({p.X},{p.Y},{p.Z})");
        }
        s_log.Info($"[NormSym] {proto.Id} @ {rotLabel}: canonical port positions:{portPosLog}");

        // Remap PrioritizedPorts before clearing the transform.
        var pp = config.GetPrioritizedPorts();
        if (pp.HasValue)
        {
            if (pp.Value.Length != proto.Ports.Length)
            {
                s_log.Warning(
                    $"[NormSym] {proto.Id}: PrioritizedPorts length {pp.Value.Length} " +
                    $"!= Ports.Length {proto.Ports.Length} — skipping normalization.");
                return;
            }

            string oldPp = PortArrayToString(pp.Value);
            var remapped = RemapPrioritizedPorts(proto, mStored, pp.Value);
            if (!remapped.HasValue)
                return; // Remap logged a warning; abort to avoid corrupting port priorities.

            string newPp = PortArrayToString(remapped.Value);
            s_log.Info($"[NormSym] {proto.Id} @ {rotLabel}: ports remapped  old={oldPp}  new={newPp}");
            config.SetPrioritizedPorts(remapped.Value);
        }
        else
        {
            s_log.Info($"[NormSym] {proto.Id} @ {rotLabel}: no PrioritizedPorts — only clearing rotation.");
        }

        // Reset orientation to canonical.
        config.Transform = new TileTransform(tf.Value.Position, Rotation90.Deg0, false);
        s_log.Info($"[NormSym] {proto.Id}: normalized to rot=0° reflected=false.");
    }

    private static string PortArrayToString(ImmutableArray<bool> arr)
    {
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < arr.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(arr[i] ? "★" : "·");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static void TryNormalizeBalancer(
        EntityConfigData config,
        LayoutEntityProto proto,
        TileTransform tf,
        string rotLabel)
    {
        int tableIdx = tf.Rotation.AngleIndex * 2 + (tf.IsReflected ? 1 : 0);
        int[] table = s_balancerRemapTables[tableIdx];

        var pp = config.GetPrioritizedPorts();
        if (pp.HasValue)
        {
            if (pp.Value.Length != table.Length)
            {
                s_log.Warning(
                    $"[NormSym] {proto.Id} @ {rotLabel}: PrioritizedPorts length {pp.Value.Length} " +
                    $"!= expected {table.Length} — skipping balancer normalization.");
                return;
            }
            var old = pp.Value;
            var newArr = new bool[table.Length];
            for (int j = 0; j < table.Length; j++)
                newArr[j] = old[table[j]];
            s_log.Info(
                $"[NormSym] {proto.Id} @ {rotLabel}: balancer remap " +
                $"old={PortArrayToString(old)}  new={PortArrayToString(ImmutableArray.Create(newArr))}");
            config.SetPrioritizedPorts(ImmutableArray.Create(newArr));
        }

        config.Transform = new TileTransform(tf.Position, Rotation90.Deg0, false);
        s_log.Info($"[NormSym] {proto.Id}: normalized to rot=0° reflected=false.");
    }

    /// <summary>
    /// Returns true if the specific (rotation, reflected) transform is a symmetry of
    /// this proto's port layout — i.e., applying it to the canonical port XY multiset
    /// yields the same multiset.  Only then can we safely normalize the stored rotation
    /// to (0, false) with a port remap.
    /// </summary>
    private static bool IsStoredTransformASymmetry(LayoutEntityProto proto, Rotation90 rotation, bool reflected)
    {
        var key = (proto.Id, rotation.AngleIndex, reflected);
        if (s_symmetryCache.TryGetValue(key, out bool cached))
            return cached;

        var ports = proto.Ports;
        if (ports.IsEmpty)
        {
            s_symmetryCache[key] = false;
            return false;
        }

        var canonical = GetSortedPortXYZDs(ports, Rotation90.Deg0, reflected: false);
        var transformed = GetSortedPortXYZDs(ports, rotation, reflected);
        bool isSymmetry = PortListsEqual(transformed, canonical);

        s_symmetryCache[key] = isSymmetry;
        return isSymmetry;
    }

    // Maps an axis-aligned unit vector (result of Matrix2i.Transform on a direction) back
    // to a Direction90.DirectionIndex value (0=+X, 1=+Y, 2=-X, 3=-Y).
    private static int VecToDirectionIndex(Vector2i v)
    {
        if (v.X > 0) return 0;
        if (v.Y > 0) return 1;
        if (v.X < 0) return 2;
        return 3;
    }

    // Sorted list of (transformedX, transformedY, Z, transformedDirIndex) for all ports.
    // Rotation/reflection transforms XY and direction but leaves Z unchanged.
    private static List<(int x, int y, int z, int d)> GetSortedPortXYZDs(
        ImmutableArray<IoPortTemplate> ports,
        Rotation90 rotation,
        bool reflected)
    {
        var m = Matrix2i.FromRotationFlip(rotation, reflected);
        var result = new List<(int, int, int, int)>(ports.Length);
        for (int i = 0; i < ports.Length; i++)
        {
            var pos = ports[i].RelativePosition;
            var v = m.Transform(new Vector2i(pos.X, pos.Y));
            var d = VecToDirectionIndex(m.Transform(ports[i].RelativeDirection.DirectionVector));
            result.Add((v.X, v.Y, pos.Z, d));
        }
        result.Sort((a, b) =>
            a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) :
            a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2) :
            a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3) :
            a.Item4.CompareTo(b.Item4));
        return result;
    }

    private static bool PortListsEqual(List<(int x, int y, int z, int d)> a, List<(int x, int y, int z, int d)> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Item1 != b[i].Item1 || a[i].Item2 != b[i].Item2 || a[i].Item3 != b[i].Item3 || a[i].Item4 != b[i].Item4) return false;
        return true;
    }

    /// <summary>
    /// Remaps the PrioritizedPorts array from the stored orientation to the
    /// canonical (rot=0, reflected=false) orientation.
    ///
    /// For each stored port i, transform its canonical position forward through
    /// M_stored to find the physical XY it occupies at the stored rotation.
    /// That physical XY equals the canonical position of the port that should
    /// carry this priority at rot=0, so newPrioritized[j] = oldPrioritized[i].
    ///
    /// Note: we deliberately avoid Matrix2i.Inverted() — the game's decompiled
    /// implementation has M01/M10 swapped, returning the wrong matrix for
    /// rotation inputs (it gives the same rotation again instead of its inverse).
    /// </summary>
    private static ImmutableArray<bool>? RemapPrioritizedPorts(
        LayoutEntityProto proto,
        Matrix2i mStored,
        ImmutableArray<bool> old)
    {
        var ports = proto.Ports;
        int n = ports.Length;

        // Build canonical-position+direction → port-index lookup.
        // Key is (X, Y, Z, directionIndex) — direction disambiguates ports sharing the same tile.
        var posToIndex = new Dictionary<(int, int, int, int), int>(n);
        for (int i = 0; i < n; i++)
        {
            var pos = ports[i].RelativePosition;
            var key = (pos.X, pos.Y, pos.Z, ports[i].RelativeDirection.DirectionIndex);
            if (posToIndex.ContainsKey(key))
            {
                s_log.Warning(
                    $"Proto {proto.Id}: duplicate port XYZD ({pos.X},{pos.Y},{pos.Z},dir={ports[i].RelativeDirection.DirectionIndex}) — " +
                    "skipping PrioritizedPorts remap.");
                return null;
            }
            posToIndex[key] = i;
        }

        var newArr = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var canonPos = ports[i].RelativePosition;
            var canonDir = ports[i].RelativeDirection.DirectionVector;
            // Transform both position XY and direction forward through M_stored.
            // Z is unaffected by rotation/reflection.
            var physXY = mStored.Transform(new Vector2i(canonPos.X, canonPos.Y));
            var physDirIdx = VecToDirectionIndex(mStored.Transform(canonDir));
            var lookupKey = (physXY.X, physXY.Y, canonPos.Z, physDirIdx);
            if (!posToIndex.TryGetValue(lookupKey, out int j))
            {
                s_log.Warning(
                    $"Proto {proto.Id}: no canonical port found at " +
                    $"({physXY.X},{physXY.Y},{canonPos.Z},dir={physDirIdx}) during remap — aborting.");
                return null;
            }
            s_log.Info($"[NormSym]   port[{i}] canon({canonPos.X},{canonPos.Y},{canonPos.Z},dir={ports[i].RelativeDirection.DirectionIndex}) -> phys({physXY.X},{physXY.Y},{canonPos.Z},dir={physDirIdx}) -> canonical port[{j}]  priority={old[i]}");
            newArr[j] = old[i];
        }

        return ImmutableArray.Create(newArr);
    }
}
