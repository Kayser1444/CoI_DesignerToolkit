// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Input;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;

namespace CoIDesignerToolkit;

internal sealed partial class BuildingMoveOrders
{
    public void CollectSurfaces(RectangleTerrainArea2i area, bool surfaces, bool decals,
        Lyst<TileSurfaceCopyPasteData> selectedSurfaces, Lyst<TileSurfaceCopyPasteData> selectedDecals)
    {
        if (!surfaces && !decals)
            return;

        foreach (Tile2iAndIndex tile in area.EnumerateTilesAndIndices(m_terrain))
        {
            TileSurfaceData placed = m_terrain.GetTileSurface(tile.Index);
            if (surfaces)
            {
                if (m_surfaces.TryGetDesignationAt(tile.TileCoord, out var designation) && designation.IsPlacing
                    && !designation.IsFulfilledAt(tile.TileCoord))
                {
                    var pending = TileSurfaceData.CreateFlat(HeightTilesI.Zero, designation.GetSurfaceAt(tile.TileCoord), Direction90.PlusX);
                    selectedSurfaces.Add(new TileSurfaceCopyPasteData(pending, tile.TileCoord, 1, 1));
                }
                else if (placed.IsValid && !placed.IsAutoPlaced && placed.SurfaceSlimId.IsNotPhantom
                    && (designation == null || designation.IsPlacing))
                {
                    selectedSurfaces.Add(new TileSurfaceCopyPasteData(placed.WithoutDecal(), tile.TileCoord, 1, 1));
                }
            }
            if (decals && placed.DecalSlimId.IsNotPhantom)
            {
                var decal = TileSurfaceData.CreateWithDecalOnly(placed.DecalSlimId, placed.IsDecalFlipped,
                    placed.DecalRotation.ToRotation(), placed.ColorKey);
                selectedDecals.Add(new TileSurfaceCopyPasteData(decal, tile.TileCoord, 1, 1));
            }
        }
    }

    private void CutTerrain(Order order)
    {
        var pending = new Lyst<TileSurfaceCopyPasteData>();
        foreach (TileSurfaceCopyPasteData tile in ExpandTiles(order.SourceSurfaces))
        {
            if (TryGetPlacing(tile.Position, out var designation) && !designation.IsFulfilledAt(tile.Position)
                && designation.GetSurfaceAt(tile.Position) == tile.SurfaceData.SurfaceSlimId)
            {
                pending.Add(tile);
                continue;
            }

            TileSurfaceData placed = GetSurface(tile.Position);
            if (placed.IsValid && !placed.IsAutoPlaced && placed.SurfaceSlimId == tile.SurfaceData.SurfaceSlimId)
                order.FinishedPavement.Add(tile.Position);
        }

        RemoveDecals(order.SourceDecals);
        RemovePendingPavement(pending);
        DesignateClears(order.FinishedPavement);
    }

    private void DesignateClears(IEnumerable<Tile2i> positions)
    {
        var proto = m_protos.GetOrThrow<SurfaceDesignationProto>(IdsCore.TerrainDesignators.ClearSurfaceDesignator);
        foreach (var group in positions.GroupBy(SurfaceDesignation.GetOrigin))
        {
            uint unassigned = ushort.MaxValue;
            foreach (Tile2i position in group)
            {
                var offset = position - group.Key;
                unassigned &= ~(1u << (offset.Y * SurfaceDesignation.SIZE_TILES + offset.X));
            }

            // A nonzero high bit keeps even a full 4x4 move on the native per-tile merge path.
            m_protectedDecals.UnionWith(group);
            if (!m_surfaces.AddOrReplaceDesignation(proto, new SurfaceDesignationData(group.Key, unassigned | (1u << 16), TileSurfaceSlimId.PhantomId)))
                m_protectedDecals.ExceptWith(group);
        }
    }

    public TileSurfaceData? CaptureProtectedDecal(Tile2iAndIndex tile, bool consume)
    {
        if (!(consume ? m_protectedDecals.Remove(tile.TileCoord) : m_protectedDecals.Contains(tile.TileCoord)))
            return null;

        if (!m_surfaces.TryGetDesignationAt(tile.TileCoord, out var designation) || designation.IsPlacing)
            return null;

        var current = m_terrain.GetTileSurface(tile.Index);
        return current.DecalSlimId.IsNotPhantom ? current : null;
    }

    public void RestoreProtectedDecal(Tile2iAndIndex tile, TileSurfaceData original)
    {
        var remaining = m_terrain.GetTileSurface(tile.Index);
        if (remaining.RawValueDecal != original.RawValueDecal)
            m_terrain.SetCustomSurface(tile, new TileSurfaceData(remaining.RawValue, original.RawValueDecal));
    }

    private bool SourcePavementCleared(Order order)
    {
        foreach (Tile2i tile in order.FinishedPavement)
        {
            if (m_surfaces.TryGetDesignationAt(tile, out var designation) && !designation.IsPlacing && !designation.IsFulfilledAt(tile))
                return false;
        }
        return true;
    }

    private string? ValidateTerrain(Order order, ImmutableArray<TileSurfaceCopyPasteData> surfaces,
        ImmutableArray<TileSurfaceCopyPasteData> decals, bool preview)
    {
        var paving = new HashSet<Tile2i>();
        var designator = m_protos.GetOrThrow<SurfaceDesignationProto>(IdsCore.TerrainDesignators.PlaceSurfaceDesignator);
        foreach (var tile in ExpandTiles(surfaces))
        {
            paving.Add(tile.Position);
            var data = new SurfaceDesignationData(SurfaceDesignation.GetOrigin(tile.Position), 0, tile.SurfaceData.SurfaceSlimId);
            if (!m_terrain.IsValidCoord(tile.Position) || !m_surfaces.CanPlaceSurfaceTile(tile.Position)
                || !m_surfaces.IsDesignationAllowed(designator, data, out _))
                return BdtLocalization.MovePavementInvalid.Format(tile.Position.ToString()).ToString();
            if (m_surfaces.TryGetDesignationAt(tile.Position, out var designation) && !designation.IsFulfilledAt(tile.Position)
                && !(preview && !designation.IsPlacing && order.FinishedPavement.Contains(tile.Position))
                && !(order.Undoing && designation.IsPlacing && designation.GetSurfaceAt(tile.Position) == tile.SurfaceData.SurfaceSlimId))
                return BdtLocalization.MoveExistingDesignation.Format(tile.Position.ToString()).ToString();
            var placed = GetSurface(tile.Position);
            if (placed.IsValid && !placed.IsAutoPlaced && placed.SurfaceSlimId.IsNotPhantom
                && placed.SurfaceSlimId != tile.SurfaceData.SurfaceSlimId
                && !(preview && order.FinishedPavement.Contains(tile.Position)))
                return BdtLocalization.MoveExistingPavement.Format(tile.Position.ToString()).ToString();
        }

        foreach (var tile in ExpandTiles(decals))
        {
            if (!m_terrain.IsValidCoord(tile.Position))
                return BdtLocalization.MoveDecalOutsideMap.TranslatedString;
            var placed = GetSurface(tile.Position);
            if ((!placed.IsValid || placed.SurfaceSlimId.IsPhantom) && !paving.Contains(tile.Position) && !TryGetPlacing(tile.Position, out _))
                return BdtLocalization.MoveDecalNeedsPavement.Format(tile.Position.ToString()).ToString();
            if (placed.DecalSlimId.IsNotPhantom && placed.RawValueDecal != tile.SurfaceData.RawValueDecal)
                return BdtLocalization.MoveExistingDecal.Format(tile.Position.ToString()).ToString();
        }
        return null;
    }

    private void PlaceTerrain(ImmutableArray<TileSurfaceCopyPasteData> surfaces, ImmutableArray<TileSurfaceCopyPasteData> decals)
    {
        if (surfaces.IsNotEmpty)
        {
            var command = new PasteSurfaceDesignationsCmd(IdsCore.TerrainDesignators.PlaceSurfaceDesignator, surfaces);
            ((IAction<PasteSurfaceDesignationsCmd>)m_surfaces).Invoke(command);
        }
        if (decals.IsNotEmpty)
        {
            var command = new BatchAddSurfaceDecalCmd(ExpandTiles(decals).ToImmutableArray());
            ((IAction<BatchAddSurfaceDecalCmd>)m_decals).Invoke(command);
        }
    }

    private void UndoTargetTerrain(Order order)
    {
        RemoveDecals(order.TargetDecals);
        var pending = new Lyst<TileSurfaceCopyPasteData>();
        var finished = new List<Tile2i>();
        foreach (var tile in ExpandTiles(order.TargetSurfaces))
        {
            if (TryGetPlacing(tile.Position, out var designation) && !designation.IsFulfilledAt(tile.Position)
                && designation.GetSurfaceAt(tile.Position) == tile.SurfaceData.SurfaceSlimId)
                pending.Add(tile);
            else if (GetSurface(tile.Position) is { IsValid: true, IsAutoPlaced: false } placed
                && placed.SurfaceSlimId == tile.SurfaceData.SurfaceSlimId)
                finished.Add(tile.Position);
        }
        RemovePendingPavement(pending);
        DesignateClears(finished);
    }

    private void CancelSourcePavementClears(Order order)
    {
        foreach (Tile2i position in order.FinishedPavement)
        {
            if (m_protectedDecals.Remove(position) && m_surfaces.TryGetDesignationAt(position, out var designation)
                && !designation.IsPlacing)
            {
                designation.UnassignTile(position);
                m_surfaces.NotifyDesignationUpdate(designation);
            }
        }
    }

    private ImmutableArray<TileSurfaceCopyPasteData> UndoSourceClears(Order order)
    {
        CancelSourcePavementClears(order);
        var clearedAtDestination = order.TerrainPlaced
            ? new HashSet<Tile2i>(ExpandTiles(order.TargetSurfaces).Select(tile => tile.Position))
            : new HashSet<Tile2i>();
        return ExpandTiles(order.SourceSurfaces).Where(tile => !order.FinishedPavement.Contains(tile.Position)
            || clearedAtDestination.Contains(tile.Position)
            || GetSurface(tile.Position).SurfaceSlimId != tile.SurfaceData.SurfaceSlimId).ToImmutableArray();
    }

    private void RemovePendingPavement(Lyst<TileSurfaceCopyPasteData> pending)
    {
        if (pending.IsEmpty)
            return;

        // Unassigning a placing designation also clears decal-only data in vanilla.
        var decals = pending.Select(tile => (Tile: m_terrain.ExtendTileIndex(tile.Position), Surface: GetSurface(tile.Position)))
            .Where(tile => tile.Surface.DecalSlimId.IsNotPhantom).ToArray();
        m_surfaces.Invoke(new BatchRemoveSurfacePlacingDesignationsCmd(pending.ToImmutableArray()));
        foreach (var tile in decals)
            RestoreProtectedDecal(tile.Tile, tile.Surface);
    }

    private void RemoveDecals(ImmutableArray<TileSurfaceCopyPasteData> decals)
    {
        var matching = ExpandTiles(decals).Where(tile => GetSurface(tile.Position).RawValueDecal == tile.SurfaceData.RawValueDecal);
        var command = new BatchRemoveSurfaceDecalCmd(matching.ToImmutableArray());
        if (command.Data.IsNotEmpty)
            m_decals.Invoke(command);
    }

    private bool TryGetPlacing(Tile2i tile, out SurfaceDesignation designation) =>
        m_surfaces.TryGetDesignationAt(tile, out designation) && designation.IsPlacing;

    private TileSurfaceData GetSurface(Tile2i tile) => m_terrain.GetTileSurface(m_terrain.GetTileIndex(tile));

    private static IEnumerable<TileSurfaceCopyPasteData> ExpandTiles(ImmutableArray<TileSurfaceCopyPasteData> data)
    {
        foreach (var item in data)
        {
            for (int y = 0; y < item.Height; y++)
            for (int x = 0; x < item.Width; x++)
                yield return new TileSurfaceCopyPasteData(item.SurfaceData, item.Position + new RelTile2i(x, y), 1, 1);
        }
    }
}
