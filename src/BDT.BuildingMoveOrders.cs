// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Entities.Validators;
using Mafi.Core.Input;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Localization;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Unity.UiToolkit;

namespace CoIDesignerToolkit;

internal sealed partial class BuildingMoveOrders : IDisposable
{
    internal sealed class Order
    {
        public Order(ImmutableArray<TileSurfaceCopyPasteData> surfaces, ImmutableArray<TileSurfaceCopyPasteData> decals)
        {
            SourceSurfaces = surfaces;
            SourceDecals = decals;
        }

        public ImmutableArray<EntityConfigData> Source = ImmutableArray<EntityConfigData>.Empty;
        public readonly ImmutableArray<TileSurfaceCopyPasteData> SourceSurfaces;
        public readonly ImmutableArray<TileSurfaceCopyPasteData> SourceDecals;
        public readonly HashSet<EntityId> CompletedSources = new();
        public readonly HashSet<EntityId> DemolitionOrdered = new();
        public readonly HashSet<EntityId> PlacedBuildings = new();
        public readonly HashSet<Tile2i> FinishedPavement = new();
        public ImmutableArray<EntityConfigData> Target = ImmutableArray<EntityConfigData>.Empty;
        public ImmutableArray<TileSurfaceCopyPasteData> TargetSurfaces = ImmutableArray<TileSurfaceCopyPasteData>.Empty;
        public ImmutableArray<TileSurfaceCopyPasteData> TargetDecals = ImmutableArray<TileSurfaceCopyPasteData>.Empty;
        public volatile bool Accepted;
        public bool TerrainPlaced;
        public volatile bool ConfirmationPending;
        public volatile bool Confirmed;
        public volatile bool Terminal;
        public volatile bool EditingTarget;
        public volatile bool PreviewDropped;
        public volatile bool ClosePreview;
        public bool Undoing;
        public int? InitialUniqueSourceCount;
        public volatile string? Blocked;
        public string? CutFailure;
    }

    public const string StateConfigKey = "bdtBuildingMoveStateJson";

    private static readonly ModLogger s_log = new("BDT.BuildingMove");
    private readonly EntitiesManager m_entities;
    private readonly IConstructionManager m_construction;
    private readonly IAction<StartDeconstructionOfStaticEntityCmd> m_demolish;
    private readonly IAction<BatchCreateStaticEntitiesCmd> m_construct;
    private readonly LayoutEntityAddRequestFactory m_requestFactory;
    private readonly TerrainManager m_terrain;
    private readonly SurfaceDesignationsManager m_surfaces;
    private readonly TerrainSurfaceManager m_decals;
    private readonly ProtosDb m_protos;
    private readonly ISimLoopEvents m_simLoop;
    private readonly UiRoot m_ui;
    private readonly BlueprintsLibrary m_blueprints;
    private readonly ConfigSerializationContext m_serializationContext;
    private readonly IModStateJsonStore m_store;
    private readonly ConcurrentQueue<Action> m_input = new();
    private readonly List<Order> m_orders = new();
    private HashSet<EntityId> m_canceledDemolitions = new();
    private readonly HashSet<Tile2i> m_protectedDecals = new();
    private int m_retryTick;
    public Order? BlockedOrder { get; private set; }

    [ThreadStatic] private static LayoutEntityAddRequest? s_uniquePreviewRequest;
    private static readonly System.Lazy<AccessTools.FieldRef<LayoutEntityAddRequest, Option<Predicate<EntityId>>>> s_collisionFilter =
        new(() => AccessTools.FieldRefAccess<LayoutEntityAddRequest, Option<Predicate<EntityId>>>("<IgnoreForCollisions>k__BackingField"));

    public BuildingMoveOrders(DependencyResolver resolver, IModStateJsonStore store)
    {
        m_entities = resolver.Resolve<EntitiesManager>();
        m_construction = resolver.Resolve<IConstructionManager>();
        m_demolish = resolver.Resolve<IAction<StartDeconstructionOfStaticEntityCmd>>();
        m_construct = resolver.Resolve<IAction<BatchCreateStaticEntitiesCmd>>();
        m_requestFactory = resolver.Resolve<LayoutEntityAddRequestFactory>();
        m_terrain = resolver.Resolve<TerrainManager>();
        m_surfaces = resolver.Resolve<SurfaceDesignationsManager>();
        m_decals = resolver.Resolve<TerrainSurfaceManager>();
        m_protos = resolver.Resolve<ProtosDb>();
        m_simLoop = resolver.Resolve<ISimLoopEvents>();
        m_ui = resolver.Resolve<UiRoot>();
        m_blueprints = resolver.Resolve<BlueprintsLibrary>();
        m_serializationContext = resolver.Resolve<ConfigSerializationContext>();
        m_store = store;
        Load();
        m_construction.EntityConstructionStateChanged.AddNonSaveable(this, OnConstructionStateChanged);
        m_simLoop.UpdateAfterCmdProc.AddNonSaveable(this, Update);
    }

    public void Dispose()
    {
        m_construction.EntityConstructionStateChanged.RemoveNonSaveable(this, OnConstructionStateChanged);
        m_simLoop.UpdateAfterCmdProc.RemoveNonSaveable(this, Update);
    }

    public void RestoreUndoHistory()
    {
        foreach (Order order in m_orders)
        {
            if (!order.Undoing)
                UndoManager.RecordBuildingMove(order, () => Undo(order));
        }
    }

    public void ShowFailure(string reason) => m_ui.ToastNotifProvider.ShowFailure(new LocStrFormatted(reason));

    private void ShowSuccess(string message) => m_ui.ToastNotifProvider.ShowSuccess(new LocStrFormatted(message));

    public void AcceptSurfaceCut(Order order) => m_input.Enqueue(() => FinishCut(order, null));

    public EntityValidationResult CutBuilding(Order order, IStaticEntity entity, EntityRemoveReason reason)
    {
        EntityValidationResult canRemove = m_entities.CanRemoveEntity(entity, reason);
        if (canRemove.IsError)
            return canRemove;

        if (m_orders.Any(existing => existing.CompletedSources.Contains(entity.Id) && !existing.Undoing
            && (existing.Confirmed || !existing.PreviewDropped)))
            return EntityValidationResult.CreateError(BdtLocalization.MoveAlreadyInProgress.AsFormatted);

        // CutBuilding is called for completed sources. If a dropped cut ordered demolition
        // and the source is completed again, that demolition was canceled. Release it from
        // the old cut's undo so a later undo cannot recreate the source of this new cut.
        for (int i = m_orders.Count - 1; i >= 0; i--)
        {
            Order previous = m_orders[i];
            if (!previous.Confirmed && previous.PreviewDropped && previous.DemolitionOrdered.Remove(entity.Id)
                && !CutChangedWorld(previous))
            {
                Complete(previous);
                UndoManager.RemoveBuildingMove(previous);
            }
        }

        order.CompletedSources.Add(entity.Id);
        if (!IsUnique(entity.Prototype))
            StartDemolition(order, entity);
        return canRemove;
    }

    public void FinishCut(Order order, Lyst<EntityConfigData>? configs)
    {
        order.Source = configs?.Select(config => config.CreateCopy()).ToImmutableArray() ?? ImmutableArray<EntityConfigData>.Empty;
        if (order.CutFailure is { } failure && !order.PreviewDropped)
            ShowFailure(failure);
        if (order.Source.IsEmpty && order.SourceSurfaces.IsEmpty && order.SourceDecals.IsEmpty)
            return;

        // Leave a selection containing only vanilla ghosts and transport sections on the vanilla paste path.
        if (order.CompletedSources.Count == 0 && order.SourceSurfaces.IsEmpty && order.SourceDecals.IsEmpty)
            return;

        CutTerrain(order);
        order.Accepted = true;
        m_orders.Add(order);
        UndoManager.RecordBuildingMove(order, () => Undo(order));
    }

    public void DropPreview(Order order)
    {
        order.PreviewDropped = true;
        m_input.Enqueue(() =>
        {
            if (!order.Confirmed && !CutChangedWorld(order))
            {
                Complete(order);
                UndoManager.RemoveBuildingMove(order);
            }
        });
    }

    private static bool CutChangedWorld(Order order) => order.DemolitionOrdered.Count > 0
        || order.SourceSurfaces.IsNotEmpty || order.SourceDecals.IsNotEmpty
        || order.Source.Any(config => config.OriginalEntityId is not { } id || !order.CompletedSources.Contains(id));

    public void Confirm(Order order, ImmutableArray<EntityConfigData> entities,
        ImmutableArray<TileSurfaceCopyPasteData> surfaces, ImmutableArray<TileSurfaceCopyPasteData> decals)
    {
        if (order.ConfirmationPending || (order.Confirmed && !order.EditingTarget))
            return;
        order.ConfirmationPending = true;
        m_input.Enqueue(() =>
        {
            try
            {
                if (!order.Accepted || order.Undoing || order.Terminal || (order.Confirmed && !order.EditingTarget))
                    return;

                var waiting = new HashSet<EntityId>(order.CompletedSources.Where(IsStillInWorld));
                string? error = ValidateBuildings(entities, waiting, preview: true) ?? ValidateTerrain(order, surfaces, decals, preview: true);
                if (error != null)
                {
                    ShowFailure(error);
                    return;
                }

                // Still-present originals are affected only after the whole target has passed preflight.
                foreach (EntityId id in waiting)
                {
                    if (TryGetStatic(id, out var entity) && !order.DemolitionOrdered.Contains(id))
                    {
                        EntityValidationResult validation = m_entities.CanRemoveEntity(entity, EntityRemoveReason.Remove);
                        if (validation.IsError)
                        {
                            ShowFailure(validation.ErrorMessageForPlayer.ToString());
                            return;
                        }
                    }
                }

                order.InitialUniqueSourceCount ??= CountUniqueSources(order);
                order.Target = entities;
                order.TargetSurfaces = surfaces;
                order.TargetDecals = decals;
                order.Confirmed = true;
                order.EditingTarget = false;
                order.ClosePreview = true;
                ClearBlocked(order);
                foreach (EntityId id in waiting)
                {
                    if (TryGetStatic(id, out var entity))
                        StartDemolition(order, entity);
                }
                if (TryPlace(order))
                    Complete(order);
                else if (order.Blocked == null)
                    ShowSuccess(BdtLocalization.MoveQueued.TranslatedString);
            }
            finally
            {
                order.ConfirmationPending = false;
            }
        });
    }

    public static bool CanRecheckLayout(EntityConfigData config) => config.Prototype.ValueOrNull is ILayoutEntityProto;

    public static bool IsPreviewingUniqueSource(LayoutEntityAddRequest request) =>
        ReferenceEquals(request, s_uniquePreviewRequest);

    private static bool IsUnique(IStaticEntityProto proto) =>
        proto is ILayoutEntityProto { IsUnique: true } || BuildingMoveApi.IsReportedUnique(proto.Id.Value);

    private bool TryGetStatic(EntityId id, out IStaticEntity entity) =>
        m_entities.TryGetEntity(id, out entity) && !entity.IsDestroyed;

    private bool IsStillInWorld(EntityId id) => TryGetStatic(id, out _);

    private void StartDemolition(Order order, IStaticEntity entity)
    {
        if (order.DemolitionOrdered.Contains(entity.Id))
            return;
        var command = new StartDeconstructionOfStaticEntityCmd(entity, EntityRemoveReason.Remove);
        m_demolish.Invoke(command);
        if (command.Result)
            order.DemolitionOrdered.Add(entity.Id);
    }

    private static bool IsRestoredConstructionState(ConstructionState state) =>
        state is ConstructionState.InConstruction or ConstructionState.Constructed;

    private static int CountUniqueSources(Order order) => order.Source.Count(config =>
        config.OriginalEntityId is { } id && order.CompletedSources.Contains(id)
        && config.Prototype.ValueOrNull is IStaticEntityProto proto && IsUnique(proto));

    private void OnConstructionStateChanged(IStaticEntity entity, ConstructionState state)
    {
        if (m_orders.Count == 0 || !IsRestoredConstructionState(state) || !IsUnique(entity.Prototype))
            return;
        if (m_orders.Any(order => order.Confirmed && !order.Undoing && order.DemolitionOrdered.Contains(entity.Id)))
            m_canceledDemolitions.Add(entity.Id);
    }

    private HashSet<Order>? ReviewCanceledDemolitions(bool reconcileSourceStates)
    {
        if (m_canceledDemolitions.Count == 0 && !reconcileSourceStates)
            return null;

        var observedCancellations = m_canceledDemolitions;
        m_canceledDemolitions = new HashSet<EntityId>();
        HashSet<Order>? ready = null;
        for (int i = m_orders.Count - 1; i >= 0; i--)
        {
            Order order = m_orders[i];
            if (!order.Confirmed || order.Undoing)
                continue;

            var canceled = order.DemolitionOrdered.Where(id => observedCancellations.Contains(id)
                || (reconcileSourceStates && TryGetStatic(id, out var source)
                    && IsRestoredConstructionState(source.ConstructionState) && IsUnique(source.Prototype))).ToArray();
            if (canceled.Length == 0)
                continue;

            if (ResolveCanceledDemolition(order, canceled))
            {
                (ready ??= new HashSet<Order>()).Add(order);
                ShowSuccess(BdtLocalization.MoveContinueAfterCancel.TranslatedString);
            }
            else
                ShowFailure(BdtLocalization.MoveCanceled.TranslatedString);
        }
        return ready;
    }

    private bool ResolveCanceledDemolition(Order order, EntityId[] canceled)
    {
        // A save made before this field existed can still be reconciled from the original cut.
        order.InitialUniqueSourceCount ??= CountUniqueSources(order);
        order.EditingTarget = false;
        order.ClosePreview = true;
        ClearBlocked(order);

        if (order.InitialUniqueSourceCount == 1 && canceled.Length == 1)
        {
            EntityId id = canceled[0];
            order.Target = order.Target.Where(config => config.OriginalEntityId != id).ToImmutableArray();
            order.Source = order.Source.Where(config => config.OriginalEntityId != id).ToImmutableArray();
            order.CompletedSources.Remove(id);
            order.DemolitionOrdered.Remove(id);
            if (order.Target.IsNotEmpty || order.TargetSurfaces.IsNotEmpty || order.TargetDecals.IsNotEmpty)
                return true;
        }

        Complete(order);
        foreach (EntityId id in order.DemolitionOrdered.ToArray())
        {
            if (!TryGetStatic(id, out var source))
                continue;
            order.DemolitionOrdered.Remove(id);
            if (!canceled.Contains(id) && source.ConstructionState is
                ConstructionState.InDeconstruction or ConstructionState.PendingDeconstruction)
                source.AbortDeconstruction();
        }
        CancelSourcePavementClears(order);
        if (!CutChangedWorld(order) && order.PlacedBuildings.Count == 0 && !order.TerrainPlaced)
            UndoManager.RemoveBuildingMove(order);
        return false;
    }

    private void Update()
    {
        bool retryBlocked = ++m_retryTick >= 30;
        var readyAfterCancellation = ReviewCanceledDemolitions(retryBlocked);
        while (m_input.TryDequeue(out Action? action))
            action();
        bool instantBuild = DesignerToolkitSettings.IsSandbox && DesignerToolkitSettings.InstantBuildModeEnabled;
        var relaxed = BuildingMoveApi.TakeRelaxedPrototypes();
        if (!retryBlocked && !instantBuild && relaxed == null && readyAfterCancellation == null)
            return;
        if (retryBlocked)
            m_retryTick = 0;

        for (int i = 0; i < m_orders.Count;)
        {
            Order order = m_orders[i];
            bool retryForRuleChange = relaxed != null && !order.Undoing && order.Target.Any(config =>
                config.Prototype.ValueOrNull is { } proto && relaxed.Contains(proto.Id.Value));
            bool retryAfterCancellation = readyAfterCancellation?.Contains(order) == true;
            if (order.Confirmed && !order.EditingTarget
                && (retryAfterCancellation || retryForRuleChange || retryBlocked || (instantBuild && order.Blocked == null)) && TryPlace(order))
                Complete(order);
            else
                i++;
        }
    }

    private void Complete(Order order)
    {
        order.Terminal = true;
        m_orders.Remove(order);
    }

    private bool TryPlace(Order order)
    {
        if ((order.Undoing && order.PlacedBuildings.Any(IsStillInWorld)) || !SourcePavementCleared(order))
            return false;

        var activeSources = new HashSet<EntityId>(order.CompletedSources.Where(IsStillInWorld));
        if (!order.Undoing && order.Target.Any(target => target.OriginalEntityId is { } id && activeSources.Contains(id)
            && target.Prototype.ValueOrNull is IStaticEntityProto proto && IsUnique(proto)))
            return false;

        string? buildingError = ValidateBuildings(order.Target, null, preview: false);
        if (buildingError != null)
        {
            if (activeSources.Count > 0 && ValidateBuildings(order.Target, activeSources, preview: true) == null)
                return false;
            return Block(order, buildingError);
        }

        string? terrainError = ValidateTerrain(order, order.TargetSurfaces, order.TargetDecals, preview: false);
        if (terrainError != null)
            return Block(order, terrainError);

        if (order.Target.IsNotEmpty)
        {
            var command = new BatchCreateStaticEntitiesCmd(order.Target, applyConfiguration: true);
            var placedNow = new HashSet<EntityId>();
            UndoManager.RunForBuildingMove(placedNow, () => m_construct.Invoke(command));
            if (!order.Undoing)
                order.PlacedBuildings.UnionWith(placedNow);
            if (!command.Result)
            {
                // Native batch placement can fail after adding part of the batch. Keep only the
                // unresolved items so a later retry cannot place the successful items a second time.
                order.Target = order.Target.Where(target => !placedNow.Any(id =>
                    TryGetStatic(id, out var entity) && MatchesPlacement(target, entity)))
                    .ToImmutableArray();
                return Block(order, string.IsNullOrWhiteSpace(command.ErrorMessage) ? BdtLocalization.MoveConstructionFailed.TranslatedString : command.ErrorMessage);
            }
            order.Target = ImmutableArray<EntityConfigData>.Empty;
        }

        order.TargetSurfaces = ExpandTiles(order.TargetSurfaces).Where(tile => GetSurface(tile.Position).SurfaceSlimId != tile.SurfaceData.SurfaceSlimId).ToImmutableArray();
        order.TargetDecals = ExpandTiles(order.TargetDecals).Where(tile => GetSurface(tile.Position).RawValueDecal != tile.SurfaceData.RawValueDecal).ToImmutableArray();
        PlaceTerrain(order.TargetSurfaces, order.TargetDecals);
        order.TerrainPlaced = !order.Undoing;
        ClearBlocked(order);
        ShowSuccess(order.Undoing ? BdtLocalization.UndoCutSuccessMessage.TranslatedString : BdtLocalization.MoveConstructionOrdered.TranslatedString);
        return true;
    }

    private static bool MatchesPlacement(EntityConfigData config, IStaticEntity entity)
    {
        if (!ReferenceEquals(config.Prototype.ValueOrNull, entity.Prototype))
            return false;
        if (entity is Transport transport && config.Trajectory.ValueOrNull is { } trajectory)
            return trajectory.Pivots.All(pivot => transport.Trajectory.TryGetLowPivotIndexFor(pivot, out _, out _));
        return entity is ILayoutEntity layout && config.Transform == layout.Transform;
    }

    private bool Block(Order order, string reason)
    {
        if (order.Blocked != reason)
        {
            order.Blocked = reason;
            BlockedOrder = order;
            ShowFailure(BdtLocalization.MoveBlocked.Format(reason).ToString());
        }
        return false;
    }

    private void ClearBlocked(Order order)
    {
        order.Blocked = null;
        if (ReferenceEquals(BlockedOrder, order))
            BlockedOrder = m_orders.LastOrDefault(candidate => candidate.Blocked != null);
    }

    private string? ValidateBuildings(ImmutableArray<EntityConfigData> configs, HashSet<EntityId>? ignore, bool preview)
    {
        foreach (EntityConfigData config in configs)
        {
            if (config.Prototype.ValueOrNull is not IStaticEntityProto proto)
                return BdtLocalization.MoveMissingPrototype.TranslatedString;
            if (!proto.TryCheckUnlocked(out string unlockError))
                return unlockError;
            if (proto is not ILayoutEntityProto layout || config.Transform is not { } transform)
                continue;

            var data = new EntityAddRequestData(transform, ignoreForCollisions: ignore == null ? null : id => ignore.Contains(id));
            LayoutEntityAddRequest request = m_requestFactory.CreateRequestFor(layout, data);
            try
            {
                EntityValidationResult validation = RunLayoutValidation(config, request, preview ? ignore : null, forceRunAll: false, null);
                if (validation.IsError)
                    return validation.ErrorMessageForPlayer.ToString();
            }
            finally
            {
                request.ReturnToPool();
            }
        }
        return null;
    }

    public EntityValidationResult ValidatePreview(Order order, EntityConfigData config, LayoutEntityAddRequest request,
        bool forceRunAll, Lyst<IEntityPreAddValidator>? preAddValidators)
    {
        var ignore = new HashSet<EntityId>(order.CompletedSources.Where(IsStillInWorld));
        Predicate<EntityId>? previous = request.IgnoreForCollisions.ValueOrNull;
        s_collisionFilter.Value(request) = new Predicate<EntityId>(id => ignore.Contains(id) || (previous?.Invoke(id) ?? false));
        return RunLayoutValidation(config, request, ignore, forceRunAll, preAddValidators);
    }

    private EntityValidationResult RunLayoutValidation(EntityConfigData config, LayoutEntityAddRequest request,
        HashSet<EntityId>? ignore, bool forceRunAll, Lyst<IEntityPreAddValidator>? preAddValidators)
    {
        if (config.OriginalEntityId is not { } source || ignore?.Contains(source) != true
            || !TryGetStatic(source, out var original) || !IsUnique(original.Prototype))
            return m_entities.CanAdd(request, forceRunAll, preAddValidators);

        if (request.Proto.IsUnique && !m_entities.Entities.Any(entity => entity.Id != source && entity.GetType() == request.Proto.EntityType))
            s_uniquePreviewRequest = request;
        BuildingMoveApi.PreviewSource = source;
        BuildingMoveApi.PreviewPrototype = original.Prototype.Id.Value;
        try
        {
            return m_entities.CanAdd(request, forceRunAll, preAddValidators);
        }
        finally
        {
            BuildingMoveApi.PreviewSource = null;
            BuildingMoveApi.PreviewPrototype = null;
            s_uniquePreviewRequest = null;
        }
    }

    private void Undo(Order order)
    {
        if (order.Undoing)
            return;

        order.Undoing = true;
        order.Terminal = false;
        order.EditingTarget = false;
        ClearBlocked(order);
        if (order.Confirmed)
        {
            foreach (EntityId id in order.PlacedBuildings)
            {
                if (TryGetStatic(id, out var destination))
                    m_demolish.Invoke(new StartDeconstructionOfStaticEntityCmd(destination, EntityRemoveReason.Remove));
            }
        }

        foreach (EntityId id in order.DemolitionOrdered)
        {
            if (TryGetStatic(id, out var source) && source.ConstructionState is
                ConstructionState.InDeconstruction or ConstructionState.PendingDeconstruction)
                source.AbortDeconstruction();
        }

        order.Target = order.Source.Where(config => config.OriginalEntityId is not { } id
            || !order.CompletedSources.Contains(id) || (order.DemolitionOrdered.Contains(id) && !IsStillInWorld(id))).ToImmutableArray();
        var originalSurfaces = UndoSourceClears(order);
        if (order.TerrainPlaced)
            UndoTargetTerrain(order);
        order.TargetSurfaces = originalSurfaces;
        order.TargetDecals = order.SourceDecals;
        order.Confirmed = true;
        if (!m_orders.Contains(order))
            m_orders.Add(order);
        if (TryPlace(order))
            Complete(order);
    }
}
