// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Entities.Validators;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Notifications;
using Mafi.Core.Products;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Localization;
using Mafi.Serialization;
using Mafi.Unity.Ui.Library.Inspectors;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;

namespace CoIDesignerToolkit;

/// <summary>
/// Extends the legacy stacker without changing its prototype or serialized entity data.
/// </summary>
public static class LegacyStackerPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.LegacyStackerPatches");

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(Stacker), nameof(Stacker.SimUpdate)),
            postfix: new HarmonyMethod(typeof(LegacyStackerPatches), nameof(StackerSimUpdatePostfix)));

        Type? inspectorType = typeof(Mafi.Unity.Entities.EntityMb).Assembly.GetType("Mafi.Unity.Ui.Inspectors.StackerInspector");
        ConstructorInfo[] constructors = inspectorType?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? Array.Empty<ConstructorInfo>();
        ConstructorInfo? constructor = constructors.Length > 0 ? constructors[0] : null;
        if (constructor == null)
        {
            s_log.Warning("Legacy stacker inspector type or constructor was not found; the full-alert control is unavailable.");
            return;
        }

        harmony.Patch(
            constructor,
            postfix: new HarmonyMethod(typeof(LegacyStackerPatches), nameof(StackerInspectorCtorPostfix)));
    }

    public static void StackerSimUpdatePostfix(
        Stacker __instance,
        Queueue<Pair<LooseProductProto, SimStep>> ___m_productsToDump,
        SimStep ___m_operatedSteps,
        TerrainManager ___m_terrainManager)
    {
        bool isFull = IsDumpAreaFull(
            __instance,
            ___m_productsToDump,
            ___m_operatedSteps,
            ___m_terrainManager);
        LegacyStackerFullAlertManager.Observe(__instance, isFull);
    }

    public static void StackerInspectorCtorPostfix(object __instance)
    {
        if (__instance is not BaseInspector<Stacker> inspector)
            return;

        inspector.TopRightButtons.Add(
            new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Bell128.png")
                .CompactForInspectorHeader(toggle: true)
                .OnClick((Action)delegate
                {
                    Stacker? stacker = inspector.Entity;
                    if (stacker != null)
                    {
                        LegacyStackerFullAlertManager.SetAlertEnabled(
                            stacker,
                            !LegacyStackerFullAlertManager.IsAlertEnabled(stacker.Id));
                    }
                }, false)
                .ObserveSelected(() =>
                    inspector.Entity != null &&
                    LegacyStackerFullAlertManager.IsAlertEnabled(inspector.Entity.Id))
                .Tooltip(BdtLocalization.LegacyStackerAlertFullTooltip)
                .Toggleable());
    }

    private static bool IsDumpAreaFull(
        Stacker stacker,
        Queueue<Pair<LooseProductProto, SimStep>> productsToDump,
        SimStep operatedSteps,
        TerrainManager terrainManager)
    {
        if (!stacker.IsEnabled || productsToDump.IsEmpty)
            return false;

        Pair<LooseProductProto, SimStep> first = productsToDump.First;
        if (operatedSteps - first.Second < stacker.Prototype.DumpDelay)
            return false;

        HeightTilesF targetHeight =
            (new HeightTilesI(stacker.CenterTile.Z + stacker.Prototype.DumpHeadRelPos.Z) - stacker.DumpHeightOffset)
            .HeightTilesF;
        Tile2iAndIndex dumpTile = terrainManager.ExtendTileIndex(stacker.DumpPositionXy);
        foreach (Tile2iAndIndexRel corner in terrainManager.FourTileCornersDeltas)
        {
            Tile2iAndIndex candidate = dumpTile + corner;
            if (!terrainManager.IsOffLimitsOrInvalid(candidate.TileCoord) &&
                terrainManager.GetHeight(candidate.Index) < targetHeight)
            {
                return false;
            }
        }
        return true;
    }
}

public sealed class LegacyStackerSupportValidator : IEntityAdditionValidator<LayoutEntityAddRequest>
{
    private readonly TerrainManager m_terrainManager;

    public EntityValidatorPriority Priority => EntityValidatorPriority.Default;

    public LegacyStackerSupportValidator(TerrainManager terrainManager)
    {
        m_terrainManager = terrainManager;
    }

    public EntityValidationResult CanAdd(LayoutEntityAddRequest addRequest)
    {
        if (addRequest.Proto is not StackerProto stackerProto)
            return EntityValidationResult.Success;

        List<int>? violatingTileIndices = addRequest.RecordTileErrorsAndMetadata ? new List<int>() : null;
        int violatingVertices = 0;
        foreach (OccupiedVertexRelative vertex in addRequest.OccupiedVertices)
        {
            bool hasHeightBounds =
                vertex.MinTerrainHeightOrMinValueRaw > short.MinValue ||
                vertex.MaxTerrainHeightOrMaxValueRaw < short.MaxValue;
            bool isSupportVertex = !vertex.Constraint.HasAnyConstraints(
                LayoutTileConstraint.Ground | LayoutTileConstraint.UsingPillar);
            if (!hasHeightBounds || !isSupportVertex)
                continue;

            HeightTilesF terrainHeight = m_terrainManager.GetHeight(addRequest.Origin.Xy + vertex.RelCoord);
            ThicknessTilesF buriedTolerance =
                stackerProto.CustomBuriedTolerance ?? StaticEntitiesTerrainInteractionManager.TOLERANCE;
            ThicknessTilesF suspendedTolerance =
                stackerProto.CustomSuspendedTolerance ?? StaticEntitiesTerrainInteractionManager.TOLERANCE;
            bool terrainTooLow =
                terrainHeight + suspendedTolerance <
                addRequest.Origin.Height + new ThicknessTilesI(vertex.MinTerrainHeightOrMinValueRaw);
            bool terrainTooHigh =
                terrainHeight - buriedTolerance >
                addRequest.Origin.Height + new ThicknessTilesI(vertex.MaxTerrainHeightOrMaxValueRaw);
            if (!terrainTooLow && !terrainTooHigh)
                continue;

            violatingVertices++;
            violatingTileIndices?.Add(vertex.LowestTileIndex);
            if (!addRequest.RecordTileErrorsAndMetadata &&
                violatingVertices > addRequest.Layout.CollapseVerticesThreshold)
            {
                break;
            }
        }

        if (violatingVertices <= addRequest.Layout.CollapseVerticesThreshold)
            return EntityValidationResult.Success;

        if (violatingTileIndices != null)
        {
            foreach (int tileIndex in violatingTileIndices)
                addRequest.SetTileError(tileIndex);
        }

        LocStr error = BdtLocalization.LegacyStackerUnsupported;
        if (addRequest.SuppressFlags.HasFlag(ValidationSuppressFlag.TerrainCollision))
            return EntityValidationResult.CreateSuppressedError(error);
        return EntityValidationResult.CreateError(error);
    }
}

public static class LegacyStackerFullAlertManager
{
    public const string CONFIG_KEY = "bdtLegacyStackerFullAlertStateJson";
    private const int SCHEMA_VERSION = 1;
    private const int FULL_ALERT_DELAY_TICKS = 256;

    private sealed class AlertState
    {
        public readonly Stacker Stacker;
        public EntityNotificator Notificator;
        public int FullTicks;

        public AlertState(Stacker stacker)
        {
            Stacker = stacker;
            Notificator = stacker.Context.NotificationsManager.CreateNotificatorFor(
                IdsCore.Notifications.CannotDeliverFromMineTower);
        }
    }

    private static readonly ModLogger s_log = new ModLogger("BDT.LegacyStackerFullAlertManager");
    private static readonly Dictionary<int, AlertState> s_states = new Dictionary<int, AlertState>();
    private static readonly HashSet<int> s_disabledEntityIds = new HashSet<int>();
    private static IModStateJsonStore? s_store;
    private static bool s_notificationsSuspended;

    public static void Initialize(EntitiesManager entitiesManager, IModStateJsonStore store)
    {
        Clear();
        s_store = store;
        LoadState();

        bool pruned = false;
        foreach (int entityIdValue in new List<int>(s_disabledEntityIds))
        {
            if (!entitiesManager.TryGetEntity<Stacker>(new EntityId(entityIdValue), out _))
            {
                s_disabledEntityIds.Remove(entityIdValue);
                pruned = true;
            }
        }
        if (pruned)
            SaveState();
    }

    public static bool IsAlertEnabled(EntityId entityId) => !s_disabledEntityIds.Contains(entityId.Value);

    public static void SetAlertEnabled(Stacker stacker, bool enabled)
    {
        if (enabled)
            s_disabledEntityIds.Remove(stacker.Id.Value);
        else
            s_disabledEntityIds.Add(stacker.Id.Value);

        if (s_states.TryGetValue(stacker.Id.Value, out AlertState? state))
            UpdateNotification(state, enabled && state.FullTicks > FULL_ALERT_DELAY_TICKS);
        SaveState();
    }

    public static void Observe(Stacker stacker, bool isFull)
    {
        if (stacker.IsDestroyed)
            return;

        if (!s_states.TryGetValue(stacker.Id.Value, out AlertState? state))
        {
            if (!isFull)
                return;
            state = new AlertState(stacker);
            s_states.Add(stacker.Id.Value, state);
        }

        state.FullTicks = isFull ? Math.Min(state.FullTicks + 1, FULL_ALERT_DELAY_TICKS + 1) : 0;
        UpdateNotification(
            state,
            IsAlertEnabled(stacker.Id) && state.FullTicks > FULL_ALERT_DELAY_TICKS);
    }

    public static void OnEntityRemoved(IEntity entity)
    {
        if (s_states.TryGetValue(entity.Id.Value, out AlertState? state))
        {
            state.Notificator.Deactivate(state.Stacker.Context.NotificationsManager);
            s_states.Remove(entity.Id.Value);
        }
        if (s_disabledEntityIds.Remove(entity.Id.Value))
            SaveState();
    }

    public static void BeforeSave()
    {
        SaveState();
        s_notificationsSuspended = true;
        foreach (AlertState state in s_states.Values)
            state.Notificator.Deactivate(state.Stacker.Context.NotificationsManager);
    }

    public static void AfterSave()
    {
        s_notificationsSuspended = false;
        foreach (AlertState state in s_states.Values)
        {
            UpdateNotification(
                state,
                IsAlertEnabled(state.Stacker.Id) && state.FullTicks > FULL_ALERT_DELAY_TICKS);
        }
    }

    public static void Clear()
    {
        foreach (AlertState state in s_states.Values)
            state.Notificator.Deactivate(state.Stacker.Context.NotificationsManager);
        s_states.Clear();
        s_disabledEntityIds.Clear();
        s_store = null;
        s_notificationsSuspended = false;
    }

    private static void UpdateNotification(AlertState state, bool shouldNotify)
    {
        state.Notificator.NotifyIff(
            shouldNotify && !s_notificationsSuspended,
            state.Stacker);
    }

    private static void LoadState()
    {
        if (s_store == null)
            return;
        string json = s_store.LoadJson();
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (parsed is not Dict<string, object> root ||
                !root.TryGetValue("schemaVersion", out object? rawSchema) ||
                !TryReadInt(rawSchema, out int schemaVersion) ||
                schemaVersion != SCHEMA_VERSION ||
                !root.TryGetValue("disabledEntityIds", out object? rawIds) ||
                rawIds is not object[] ids)
            {
                return;
            }

            foreach (object rawId in ids)
            {
                if (TryReadInt(rawId, out int id) && id > 0)
                    s_disabledEntityIds.Add(id);
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load legacy stacker full-alert state: {ex.Message}");
        }
    }

    private static void SaveState()
    {
        if (s_store == null)
            return;

        try
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":").Append(SCHEMA_VERSION).Append(",\"disabledEntityIds\":[");
            List<int> sortedEntityIds = new List<int>(s_disabledEntityIds);
            sortedEntityIds.Sort();
            bool first = true;
            foreach (int entityId in sortedEntityIds)
            {
                if (!first)
                    builder.Append(',');
                first = false;
                builder.Append(entityId);
            }
            builder.Append("]}");

            ModStateJsonSaveResult result = s_store.SaveJson(builder.ToString());
            if (!result.Succeeded)
                s_log.Warning($"Failed to save legacy stacker full-alert state: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Exception saving legacy stacker full-alert state: {ex.Message}");
        }
    }

    private static bool TryReadInt(object value, out int result)
    {
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }
        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }
        if (value is double doubleValue && doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
        {
            result = (int)doubleValue;
            return Math.Abs(doubleValue - result) < double.Epsilon;
        }
        result = 0;
        return false;
    }
}
