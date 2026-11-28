// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;

namespace CoIDesignerToolkit;

internal sealed partial class BuildingMoveOrders
{
    private static readonly ConstructorInfo s_blueprintConstructor = typeof(IBlueprint).Assembly
        .GetType("Mafi.Core.Entities.Blueprints.Blueprint", throwOnError: true)
        .GetConstructor(new[] { typeof(string), typeof(ImmutableArray<EntityConfigData>),
            typeof(ImmutableArray<TileSurfaceCopyPasteData>), typeof(ImmutableArray<TileSurfaceCopyPasteData>), typeof(ConfigSerializationContext) });

    [DataContract]
    private sealed class SavedState
    {
        [DataMember] public int Version = 1;
        [DataMember] public SavedOrder[] Orders = Array.Empty<SavedOrder>();
        [DataMember] public int[][] ProtectedDecals = Array.Empty<int[]>();
    }

    [DataContract]
    private sealed class SavedOrder
    {
        [DataMember] public string Source = "";
        [DataMember] public string Target = "";
        [DataMember] public int[] CompletedSources = Array.Empty<int>();
        [DataMember] public int[] DemolitionOrdered = Array.Empty<int>();
        [DataMember] public int[] PlacedBuildings = Array.Empty<int>();
        [DataMember] public int[][] FinishedPavement = Array.Empty<int[]>();
        [DataMember] public bool Confirmed;
        [DataMember] public bool Undoing;
        [DataMember] public int? InitialUniqueSourceCount;
    }

    public void Save()
    {
        try
        {
            var saved = new SavedState
            {
                ProtectedDecals = m_protectedDecals.Select(tile => new[] { tile.X, tile.Y }).ToArray(),
                Orders = m_orders.Select(order => new SavedOrder
                {
                    Source = Encode(order.Source, order.SourceSurfaces, order.SourceDecals),
                    Target = Encode(order.Target, order.TargetSurfaces, order.TargetDecals),
                    CompletedSources = order.CompletedSources.Select(id => id.Value).ToArray(),
                    DemolitionOrdered = order.DemolitionOrdered.Select(id => id.Value).ToArray(),
                    PlacedBuildings = order.PlacedBuildings.Select(id => id.Value).ToArray(),
                    FinishedPavement = order.FinishedPavement.Select(tile => new[] { tile.X, tile.Y }).ToArray(),
                    Confirmed = order.Confirmed,
                    Undoing = order.Undoing,
                    InitialUniqueSourceCount = order.InitialUniqueSourceCount,
                }).ToArray(),
            };
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(SavedState)).WriteObject(stream, saved);
            var result = m_store.SaveJson(Encoding.UTF8.GetString(stream.ToArray()));
            if (!result.Succeeded)
                s_log.Warning("Could not save building moves. " + result.ErrorMessage);
        }
        catch (Exception ex)
        {
            s_log.Warning("Could not save building moves. " + ex.Message);
        }
    }

    private void Load()
    {
        string json = m_store.LoadJson();
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var saved = (SavedState)new DataContractJsonSerializer(typeof(SavedState)).ReadObject(stream);
            if (saved.Version != 1)
                throw new SerializationException("Unsupported building move state version.");

            foreach (var entry in saved.Orders)
            {
                IBlueprint? source = Decode(entry.Source);
                IBlueprint? target = Decode(entry.Target);
                var order = new Order(source?.Surfaces ?? ImmutableArray<TileSurfaceCopyPasteData>.Empty,
                    source?.Decals ?? ImmutableArray<TileSurfaceCopyPasteData>.Empty)
                {
                    Source = source?.Items ?? ImmutableArray<EntityConfigData>.Empty,
                    Target = target?.Items ?? ImmutableArray<EntityConfigData>.Empty,
                    TargetSurfaces = target?.Surfaces ?? ImmutableArray<TileSurfaceCopyPasteData>.Empty,
                    TargetDecals = target?.Decals ?? ImmutableArray<TileSurfaceCopyPasteData>.Empty,
                    Accepted = true,
                    PreviewDropped = true,
                    Confirmed = entry.Confirmed,
                    Undoing = entry.Undoing,
                    InitialUniqueSourceCount = entry.InitialUniqueSourceCount,
                };
                order.CompletedSources.UnionWith(entry.CompletedSources.Select(value => new EntityId(value)));
                order.DemolitionOrdered.UnionWith(entry.DemolitionOrdered.Select(value => new EntityId(value)));
                order.PlacedBuildings.UnionWith(entry.PlacedBuildings.Select(value => new EntityId(value)));
                order.FinishedPavement.UnionWith(entry.FinishedPavement.Select(coords => new Tile2i(coords[0], coords[1])));
                if (order.Confirmed || CutChangedWorld(order))
                    m_orders.Add(order);
            }
            m_protectedDecals.UnionWith(saved.ProtectedDecals.Select(coords => new Tile2i(coords[0], coords[1])));
        }
        catch (Exception ex)
        {
            s_log.Warning("Could not restore building moves. " + ex.Message);
        }
    }

    private string Encode(ImmutableArray<EntityConfigData> entities, ImmutableArray<TileSurfaceCopyPasteData> surfaces,
        ImmutableArray<TileSurfaceCopyPasteData> decals)
    {
        if (entities.IsEmpty && surfaces.IsEmpty && decals.IsEmpty)
            return "";

        var blueprint = (IBlueprint)s_blueprintConstructor.Invoke(new object[] { "Building move", entities, surfaces, decals, m_serializationContext });
        return m_blueprints.ConvertToString(blueprint);
    }

    private IBlueprint? Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return null;
        if (m_blueprints.TryParseFromString(encoded, out string error, out var item) && item is IBlueprint blueprint)
            return blueprint;

        throw new SerializationException("Invalid building move payload. " + error);
    }
}
