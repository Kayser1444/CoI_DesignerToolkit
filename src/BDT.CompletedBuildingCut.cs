// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using CoI.AutoHelpers.Persistence;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Entities.Validators;
using Mafi.Core.Input;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.Factory;
using Mafi.Unity.Terrain;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Controllers.LayoutEntityPlacing;
using Mafi.Unity.Ui.Controllers.Tools;

namespace CoIDesignerToolkit;

internal static class CompletedBuildingCut
{
    private static readonly Type CutController =
        typeof(BaseEntityCursorInputController<IStaticEntity>).Assembly.GetType("Mafi.Unity.Ui.Controllers.Tools.CutPasteEntityInputController", throwOnError: true);
    private static readonly Type CursorController = typeof(BaseEntityCursorInputController<IStaticEntity>);
    private static readonly MethodInfo ActivatePlacer = AccessTools.DeclaredMethod(CutController, "activatePlacerWith");
    private static readonly FieldInfo PlacerField = AccessTools.Field(CutController.BaseType, "EntityPlacer");
    private static readonly FieldInfo FilterField = AccessTools.Field(CursorController, "FilterToolbox");
    private static readonly Type MassPlacer = typeof(StaticEntityMassPlacer);
    private static readonly FieldInfo PreviewsField = AccessTools.Field(MassPlacer, "m_entityPreviews");
    private static readonly FieldInfo SurfacePreviewField = AccessTools.Field(MassPlacer, "m_surfacesToAdd");
    private static readonly FieldInfo DecalPreviewField = AccessTools.Field(MassPlacer, "m_decalsToAdd");
    private static readonly MethodInfo UpdateVisibility = AccessTools.DeclaredMethod(CursorController, "updateIsVisible");

    private sealed class MovePreview
    {
        public readonly BuildingMoveOrders.Order Order;
        public readonly EntityConfigData Config;

        public MovePreview(BuildingMoveOrders.Order order, EntityConfigData config)
        {
            Order = order;
            Config = config;
        }
    }

    private static readonly ConditionalWeakTable<LayoutEntityPreview, MovePreview> s_movePreviews = new();
    private static readonly ConditionalWeakTable<CutEntitiesForPasteCmd, BuildingMoveOrders.Order> s_cutOrders = new();
    private static WeakReference<BaseEntityCursorInputController<IStaticEntity>>? s_controller;
    private static IUnityInputMgr? s_inputManager;
    private static BuildingMoveOrders? s_orders;
    private static BuildingMoveOrders.Order? s_lastCut;
    private static StaticEntityMassPlacer? s_activePlacer;
    private static BuildingMoveOrders.Order? s_activeOrder;
    private static bool s_sandboxCutAvailable;

    [ThreadStatic] private static BuildingMoveOrders.Order? s_selectingCut;
    [ThreadStatic] private static BuildingMoveOrders.Order? s_runningCut;

    private static bool IsEnabled => DesignerToolkitSettings.MoveBuildingsEnabled;
    private static bool SandboxCutAvailable => IsEnabled && DesignerToolkitSettings.IsSandbox;

    public static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(AccessTools.DeclaredMethod(CutController, "Matches"),
            transpiler: Patch(nameof(AllowConstructedSelection)));
        harmony.Patch(AccessTools.GetDeclaredConstructors(CutController).Single(ctor => !ctor.IsStatic),
            postfix: Patch(nameof(RememberController)));
        harmony.Patch(UpdateVisibility, transpiler: Patch(nameof(AllowSandboxVisibility)));
        harmony.Patch(AccessTools.DeclaredMethod(CursorController, "collectSurfacesAndDecals"),
            prefix: Patch(nameof(CollectMoveSurfaces)));
        harmony.Patch(AccessTools.DeclaredMethod(CursorController, "anyEntityMatcher"), prefix: Patch(nameof(RespectBuildingHoverFilter)));
        harmony.Patch(AccessTools.DeclaredMethod(CutController, "OnEntitiesSelected"),
            postfix: Patch(nameof(RespectFilters)));
        harmony.Patch(AccessTools.DeclaredMethod(CutController, "onSync"),
            prefix: Patch(nameof(BeginInputCut)), transpiler: Patch(nameof(RouteCutCommands)),
            finalizer: Patch(nameof(EndInputCut)));
        harmony.Patch(AccessTools.DeclaredMethod(CutController, nameof(BaseEntityCursorInputController<IStaticEntity>.Deactivate)),
            postfix: Patch(nameof(DropPendingCut)));
        harmony.Patch(AccessTools.DeclaredMethod(CutController, "OnHoverChanged"), transpiler: Patch(nameof(HighlightFinishedPavement)));
        harmony.Patch(ActivatePlacer, postfix: Patch(nameof(ActivateMovePreview)),
            transpiler: Patch(nameof(SkipMovePlacementHistory)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(StaticEntityMassPlacer), nameof(StaticEntityMassPlacer.InputUpdate)),
            prefix: Patch(nameof(CloseAcceptedPreview)), transpiler: Patch(nameof(RouteMovePlacement)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(StaticEntityMassPlacer), nameof(StaticEntityMassPlacer.Deactivate)),
            postfix: Patch(nameof(DeactivateMovePreview)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EntitiesCommandsProcessor), nameof(EntitiesCommandsProcessor.Invoke),
                new[] { typeof(CutEntitiesForPasteCmd) }),
            prefix: Patch(nameof(BeginEntityCut)), postfix: Patch(nameof(FinishEntityCut)),
            transpiler: Patch(nameof(RouteCompletedCut)), finalizer: Patch(nameof(EndEntityCut)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(LayoutEntityPreview), "SimUpdate"),
            transpiler: Patch(nameof(PreviewMoveValidation)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(UniqueEntityValidator), nameof(UniqueEntityValidator.CanAdd)),
            prefix: Patch(nameof(AllowUniqueMovePreview)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(TerrainManager), nameof(TerrainManager.ClearCustomSurface)),
            prefix: Patch(nameof(BeforeClearPavement)), postfix: Patch(nameof(AfterClearPavement)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(SurfaceDesignationsManager), nameof(SurfaceDesignationsManager.ClearCustomSurface)),
            prefix: Patch(nameof(BeforeClearPavementWithFoundation)), postfix: Patch(nameof(AfterClearPavement)));
    }

    public static void Initialize(DependencyResolver resolver, IModStateJsonStore store)
    {
        s_inputManager = resolver.Resolve<UiContext>().InputMgr;
        s_orders = new BuildingMoveOrders(resolver, store);
        RefreshAvailability();
    }

    public static void RestoreUndoHistory() => s_orders?.RestoreUndoHistory();

    public static void SaveState() => s_orders?.Save();

    public static void Dispose()
    {
        s_orders?.Dispose();
        s_orders = null;
        s_inputManager = null;
        s_controller = null;
        s_lastCut = null;
        s_activePlacer = null;
        s_activeOrder = null;
        BuildingMoveApi.Clear();
    }

    public static void RefreshAvailabilityIfChanged(GameTime _)
    {
        if (SandboxCutAvailable != s_sandboxCutAvailable)
            RefreshAvailability();
    }

    public static void RefreshAvailability()
    {
        s_sandboxCutAvailable = SandboxCutAvailable;
        if (s_controller != null && s_controller.TryGetTarget(out var controller))
            UpdateVisibility.Invoke(controller, null);
    }

    public static void CancelActivePlacement()
    {
        if (s_controller != null && s_controller.TryGetTarget(out var controller) && controller.IsActive)
            s_inputManager?.DeactivateController(controller);
    }

    public static void RetargetBlockedMove()
    {
        if (s_orders is not { } orders)
            return;
        if (orders.BlockedOrder is not { Undoing: false, Terminal: false } order)
        {
            orders.ShowFailure(BdtLocalization.MoveNoBlocked.TranslatedString);
            return;
        }
        if (s_controller == null || !s_controller.TryGetTarget(out var controller) || s_inputManager == null)
            return;

        CancelActivePlacement();
        s_inputManager.ActivateNewController(controller);
        if (!controller.IsActive)
            return;

        order.ClosePreview = false;
        order.EditingTarget = true;
        s_lastCut = order;
        var placer = (StaticEntityMassPlacer)PlacerField.GetValue(controller);
        placer.Activate(controller, CancelActivePlacement, CancelActivePlacement);
        placer.SetEntitiesToClone(order.Target.Select(config => config.CreateCopy()).ToLyst(),
            order.TargetSurfaces, order.TargetDecals, StaticEntityMassPlacer.ApplyConfigMode.AlwaysApply, skipAddToHistory: true);
        ActivateMovePreview(controller);
    }

    private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(CompletedBuildingCut), name);

    private static void RememberController(BaseEntityCursorInputController<IStaticEntity> __instance)
    {
        s_controller = new WeakReference<BaseEntityCursorInputController<IStaticEntity>>(__instance);
        if (s_inputManager != null)
            RefreshAvailability();
    }

    private static IEnumerable<CodeInstruction> AllowSandboxVisibility(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(UnlockedProtosDbForUi), nameof(UnlockedProtosDbForUi.IsUnlocked), new[] { typeof(IProto) }),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(IsToolUnlocked)));

    private static bool IsToolUnlocked(UnlockedProtosDbForUi unlocked, IProto proto) =>
        (SandboxCutAvailable && proto.Id == IdsCore.Technology.CutTool) || unlocked.IsUnlocked(proto);

    private static IEnumerable<CodeInstruction> AllowConstructedSelection(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.PropertyGetter(typeof(IStaticEntity), nameof(IStaticEntity.ConstructionState)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(GetSelectableConstructionState)));

    private static ConstructionState GetSelectableConstructionState(IStaticEntity entity) =>
        IsEnabled && entity.ConstructionState == ConstructionState.Constructed
            ? ConstructionState.NotInitialized
            : entity.ConstructionState;

    private static bool CollectMoveSurfaces(BaseEntityCursorInputController<IStaticEntity> __instance,
        RectangleTerrainArea2i area, Lyst<TileSurfaceCopyPasteData> selectedSurfaces, Lyst<TileSurfaceCopyPasteData> selectedDecals,
        Option<FilterToolbox> ___FilterToolbox)
    {
        if (!IsEnabled || __instance.GetType() != CutController || s_orders == null)
            return true;

        s_orders.CollectSurfaces(area, ___FilterToolbox.Value.AreSurfacesAllowed, ___FilterToolbox.Value.AreDecalsAllowed,
            selectedSurfaces, selectedDecals);
        return false;
    }

    private static bool RespectBuildingHoverFilter(BaseEntityCursorInputController<IStaticEntity> __instance,
        ref bool __result, out ColorRgba color)
    {
        color = ColorRgba.Empty;
        if (!IsEnabled || __instance.GetType() != CutController || GetFilter(__instance).AreEntitiesAllowed)
            return true;

        __result = false;
        return false;
    }

    private static void RespectFilters(object __instance, Lyst<IStaticEntity> ___m_entitiesToCut,
        Lyst<SubTransport> ___m_partialTransportsToCut, Lyst<TileSurfaceCopyPasteData> ___m_surfacesToCut,
        Lyst<TileSurfaceCopyPasteData> ___m_decalsToCut, ref bool ___m_performCut)
    {
        if (!IsEnabled || !___m_performCut)
            return;

        ApplyFilters(GetFilter(__instance), ___m_entitiesToCut, ___m_partialTransportsToCut, ___m_surfacesToCut, ___m_decalsToCut);
        ___m_performCut = ___m_entitiesToCut.IsNotEmpty || ___m_partialTransportsToCut.IsNotEmpty
            || ___m_surfacesToCut.IsNotEmpty || ___m_decalsToCut.IsNotEmpty;
    }

    private static void ApplyFilters(FilterToolbox filter, Lyst<IStaticEntity> entities, Lyst<SubTransport> transports,
        Lyst<TileSurfaceCopyPasteData> surfaces, Lyst<TileSurfaceCopyPasteData> decals)
    {
        if (!filter.AreEntitiesAllowed)
        {
            entities.Clear();
            transports.Clear();
        }
        else
        {
            // Vanilla partial cuts destroy the selected section directly. A completed transport
            // can move through normal demolition only when the whole transport was selected.
            transports.RemoveWhere(section => section.OriginalTransport.ConstructionState == ConstructionState.Constructed);
        }
        if (!filter.AreSurfacesAllowed)
            surfaces.Clear();
        if (!filter.AreDecalsAllowed)
            decals.Clear();
    }

    private static FilterToolbox GetFilter(object controller) =>
        ((Option<FilterToolbox>)FilterField.GetValue(controller)).Value;

    private static void BeginInputCut(BaseEntityCursorInputController<IStaticEntity> __instance,
        bool ___m_performCut, Option<CutEntitiesForPasteCmd> ___m_pendingCutCmd,
        Lyst<IStaticEntity> ___m_entitiesToCut, Lyst<SubTransport> ___m_partialTransportsToCut,
        Lyst<TileSurfaceCopyPasteData> ___m_surfacesToCut, Lyst<TileSurfaceCopyPasteData> ___m_decalsToCut)
    {
        if (!__instance.IsActive || !___m_performCut || ___m_pendingCutCmd.HasValue)
            return;

        s_lastCut = null;
        if (!IsEnabled || s_orders == null)
            return;

        ApplyFilters(GetFilter(__instance), ___m_entitiesToCut, ___m_partialTransportsToCut, ___m_surfacesToCut, ___m_decalsToCut);

        s_selectingCut = new BuildingMoveOrders.Order(___m_surfacesToCut.ToImmutableArray(), ___m_decalsToCut.ToImmutableArray());
        s_lastCut = s_selectingCut;
        if (___m_entitiesToCut.IsEmpty && ___m_partialTransportsToCut.IsEmpty)
            s_orders.AcceptSurfaceCut(s_selectingCut);
    }

    private static IEnumerable<CodeInstruction> HighlightFinishedPavement(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(AccessTools.DeclaredMethod(typeof(TerrainRenderer), nameof(TerrainRenderer.SetSurfaceHighlightArea)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(HighlightCutSurfaceArea)));

    private static void HighlightCutSurfaceArea(TerrainRenderer renderer, RectangleTerrainArea2i area, ColorRgba color,
        bool onlyDesignations) => renderer.SetSurfaceHighlightArea(area, color, onlyDesignations && !IsEnabled);

    private static void DropPendingCut()
    {
        if (s_lastCut is { } order)
        {
            s_lastCut = null;
            s_orders?.DropPreview(order);
        }
    }

    private static void EndInputCut() => s_selectingCut = null;

    private static IEnumerable<CodeInstruction> RouteCutCommands(IEnumerable<CodeInstruction> instructions)
    {
        var entity = ScheduledMethod(typeof(CutEntitiesForPasteCmd));
        var surface = ScheduledMethod(typeof(BatchRemoveSurfacePlacingDesignationsCmd));
        var decal = ScheduledMethod(typeof(BatchRemoveSurfaceDecalCmd));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(entity))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompletedBuildingCut), nameof(ScheduleCut))).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            else if (instruction.Calls(surface))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompletedBuildingCut), nameof(ScheduleSurfaceCut))).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            else if (instruction.Calls(decal))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompletedBuildingCut), nameof(ScheduleDecalCut))).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            else
                yield return instruction;
        }
    }

    private static MethodInfo ScheduledMethod(Type command) =>
        AccessTools.Method(typeof(IInputScheduler), nameof(IInputScheduler.ScheduleInputCmd)).MakeGenericMethod(command);

    private static CutEntitiesForPasteCmd ScheduleCut(IInputScheduler scheduler, CutEntitiesForPasteCmd command)
    {
        if (s_selectingCut is { } order)
            s_cutOrders.Add(command, order);
        return scheduler.ScheduleInputCmd(command);
    }

    private static BatchRemoveSurfacePlacingDesignationsCmd ScheduleSurfaceCut(IInputScheduler scheduler, BatchRemoveSurfacePlacingDesignationsCmd command) =>
        s_selectingCut == null ? scheduler.ScheduleInputCmd(command) : command;

    private static BatchRemoveSurfaceDecalCmd ScheduleDecalCut(IInputScheduler scheduler, BatchRemoveSurfaceDecalCmd command) =>
        s_selectingCut == null ? scheduler.ScheduleInputCmd(command) : command;

    private static void BeginEntityCut(CutEntitiesForPasteCmd cmd)
    {
        s_cutOrders.TryGetValue(cmd, out s_runningCut);
    }

    private static void FinishEntityCut(CutEntitiesForPasteCmd cmd)
    {
        if (s_runningCut is { } order)
            s_orders?.FinishCut(order, cmd.CutConfigsOut);
    }

    private static void EndEntityCut() => s_runningCut = null;

    private static IEnumerable<CodeInstruction> RouteCompletedCut(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(EntitiesManager), nameof(EntitiesManager.TryCutAndDestroy)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(CutBuilding)));

    private static EntityValidationResult CutBuilding(EntitiesManager manager, IStaticEntity entity, EntityRemoveReason reason)
    {
        BuildingMoveOrders.Order? order = s_runningCut;
        EntityValidationResult result = order != null && s_orders is { } orders && entity.ConstructionState == ConstructionState.Constructed
            ? orders.CutBuilding(order, entity, reason)
            : manager.TryCutAndDestroy(entity, reason);
        if (order != null && result.IsError)
            order.CutFailure ??= result.ErrorMessageForPlayer.ToString();
        return result;
    }

    private static void ActivateMovePreview(object __instance)
    {
        s_activeOrder = s_lastCut is { } order && (order.EditingTarget || order.Accepted || order.SourceSurfaces.IsNotEmpty || order.SourceDecals.IsNotEmpty)
            ? order : null;
        s_lastCut = null;
        s_activePlacer = s_activeOrder == null ? null
            : (StaticEntityMassPlacer)PlacerField.GetValue(__instance);
        if (s_activePlacer != null && s_activeOrder is { } move)
        {
            foreach (var entry in GetPreviews(s_activePlacer))
            {
                if (entry.Key is LayoutEntityPreview preview)
                {
                    s_movePreviews.Remove(preview);
                    s_movePreviews.Add(preview, new MovePreview(move, entry.Value));
                }
            }
        }
    }

    private static bool CloseAcceptedPreview(StaticEntityMassPlacer __instance)
    {
        if (__instance != s_activePlacer || s_activeOrder?.ClosePreview != true)
            return true;
        CancelActivePlacement();
        return false;
    }

    private static void DeactivateMovePreview(StaticEntityMassPlacer __instance)
    {
        if (s_activePlacer != __instance)
            return;
        if (s_activeOrder is { } order)
        {
            order.EditingTarget = false;
            if (!order.Confirmed)
                s_orders?.DropPreview(order);
        }
        s_activePlacer = null;
        s_activeOrder = null;
    }

    private static IEnumerable<CodeInstruction> SkipMovePlacementHistory(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(AccessTools.DeclaredMethod(MassPlacer, nameof(StaticEntityMassPlacer.SetEntitiesToClone)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(SetCutPreview)));

    private static void SetCutPreview(StaticEntityMassPlacer placer, Mafi.Collections.ReadonlyCollections.IIndexable<EntityConfigData> configs,
        ImmutableArray<TileSurfaceCopyPasteData> surfaces, ImmutableArray<TileSurfaceCopyPasteData> decals,
        StaticEntityMassPlacer.ApplyConfigMode configApplyMode, bool isHeightNormalizedToZero, bool skipAddToHistory,
        bool removalMode, int overlapDeltaX, int overlapDeltaY, Tile3i? heightAnchor) =>
        placer.SetEntitiesToClone(configs, surfaces, decals, configApplyMode, isHeightNormalizedToZero,
            skipAddToHistory || s_lastCut is { Accepted: true } || (s_lastCut?.SourceSurfaces.IsNotEmpty ?? false)
            || (s_lastCut?.SourceDecals.IsNotEmpty ?? false), removalMode, overlapDeltaX, overlapDeltaY, heightAnchor);

    private static IEnumerable<CodeInstruction> RouteMovePlacement(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var code = new List<CodeInstruction>(instructions);
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "ApplyAdditionalPoles");
        if (index < 0)
            throw new InvalidOperationException("BDT could not find the mass placer confirmation point.");

        Label resume = generator.DefineLabel();
        code[index + 1].labels.Add(resume);
        code.InsertRange(index + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompletedBuildingCut), nameof(ConfirmMovePlacement))),
            new CodeInstruction(OpCodes.Brfalse, resume),
            new CodeInstruction(OpCodes.Ldc_I4_1),
            new CodeInstruction(OpCodes.Ret),
        });
        return code;
    }

    private static Lyst<KeyValuePair<IStaticEntityPreview, EntityConfigData>> GetPreviews(StaticEntityMassPlacer placer) =>
        (Lyst<KeyValuePair<IStaticEntityPreview, EntityConfigData>>)PreviewsField.GetValue(placer);

    private static bool ConfirmMovePlacement(StaticEntityMassPlacer placer)
    {
        if (placer != s_activePlacer || s_activeOrder is not { } order || s_orders is not { } orders)
            return false;

        var previews = GetPreviews(placer);
        var surfaces = (Lyst<TileSurfaceCopyPasteData>)SurfacePreviewField.GetValue(placer);
        var decals = (Lyst<TileSurfaceCopyPasteData>)DecalPreviewField.GetValue(placer);

        var targets = new Lyst<EntityConfigData>(previews.Count);
        var seen = new HashSet<EntityId>();
        foreach (var entry in previews)
        {
            IStaticEntityPreview preview = entry.Key;
            EntityConfigData config = entry.Value;
            if (!BuildingMoveOrders.CanRecheckLayout(config) &&
                (!preview.ValidationResult.HasValue || preview.ValidationResult.Value.IsError || preview.ReplacementStatus.HasValue))
            {
                orders.ShowFailure(BdtLocalization.MoveDestinationBlocked.TranslatedString);
                return true;
            }
            if (config.OriginalEntityId is { } source && order.CompletedSources.Contains(source) && !seen.Add(source))
            {
                orders.ShowFailure(BdtLocalization.MovePasteOnce.TranslatedString);
                return true;
            }
            var target = config.CreateCopy();
            preview.UpdateConfigWithTransform(target);
            targets.Add(target);
        }

        orders.Confirm(order, targets.ToImmutableArray(), surfaces.ToImmutableArray(), decals.ToImmutableArray());
        return true;
    }

    private static IEnumerable<CodeInstruction> PreviewMoveValidation(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.DeclaredMethod(typeof(EntitiesManager), nameof(EntitiesManager.CanAdd));
        var replacement = AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(ValidateMovePreview));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
            else
                yield return instruction;
        }
    }

    private static EntityValidationResult ValidateMovePreview(EntitiesManager manager, IEntityAddRequest request, bool forceRunAll,
        Lyst<IEntityPreAddValidator>? preAddValidators, LayoutEntityPreview preview) =>
        s_orders is { } orders && request is LayoutEntityAddRequest layout && s_movePreviews.TryGetValue(preview, out var binding)
        && ReferenceEquals(binding.Order, s_activeOrder)
            ? orders.ValidatePreview(binding.Order, binding.Config, layout, forceRunAll, preAddValidators)
            : manager.CanAdd(request, forceRunAll, preAddValidators);

    private static bool AllowUniqueMovePreview( LayoutEntityAddRequest addRequest, ref EntityValidationResult __result)
    {
        if (!BuildingMoveOrders.IsPreviewingUniqueSource(addRequest))
            return true;

        __result = EntityValidationResult.Success;
        return false;
    }

    private static void BeforeClearPavement(Tile2iAndIndex tileAndIndex, out TileSurfaceData? __state) =>
        __state = s_orders?.CaptureProtectedDecal(tileAndIndex, consume: true);

    private static void BeforeClearPavementWithFoundation(Tile2iAndIndex tileAndIndex, out TileSurfaceData? __state) =>
        __state = s_orders?.CaptureProtectedDecal(tileAndIndex, consume: false);

    private static void AfterClearPavement(Tile2iAndIndex tileAndIndex, TileSurfaceData? __state)
    {
        if (__state is { } decal)
            s_orders?.RestoreProtectedDecal(tileAndIndex, decal);
    }
}
