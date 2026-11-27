// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.Entities.Validators;
using Mafi.Core.Prototypes;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui.Controllers.Tools;

namespace CoIDesignerToolkit;

internal static class CompletedBuildingCut
{
    private static readonly Type CutController =
        typeof(BaseEntityCursorInputController<IStaticEntity>).Assembly.GetType("Mafi.Unity.Ui.Controllers.Tools.CutPasteEntityInputController", throwOnError: true);
    private static readonly MethodInfo UpdateVisibility =
        AccessTools.DeclaredMethod(typeof(BaseEntityCursorInputController<IStaticEntity>), "updateIsVisible");

    private static WeakReference<BaseEntityCursorInputController<IStaticEntity>>? s_controller;
    private static IUnityInputMgr? s_inputManager;
    private static bool s_cutAvailability;

    [ThreadStatic]
    private static HashSet<EntityId>? s_completedCutIds;

    private static bool IsEnabled =>
        DesignerToolkitSettings.IsSandbox && DesignerToolkitSettings.InstantBuildModeEnabled;

    public static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(AccessTools.DeclaredMethod(CutController, "Matches"),
            transpiler: Patch(nameof(AllowConstructedSelection)));
        harmony.Patch(AccessTools.GetDeclaredConstructors(CutController).Single(ctor => !ctor.IsStatic),
            postfix: Patch(nameof(RememberController)));
        harmony.Patch(UpdateVisibility, transpiler: Patch(nameof(AllowSandboxVisibility)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EntitiesCommandsProcessor), nameof(EntitiesCommandsProcessor.Invoke),
                new[] { typeof(CutEntitiesForPasteCmd) }),
            prefix: Patch(nameof(BeginCut)), postfix: Patch(nameof(RecordCut)),
            transpiler: Patch(nameof(CaptureCompletedCutIds)), finalizer: Patch(nameof(EndCut)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EntitiesManager), nameof(EntitiesManager.CanCutEntity)),
            prefix: Patch(nameof(ValidateCompletedCut)));
    }

    public static void Initialize(IUnityInputMgr inputManager)
    {
        s_inputManager = inputManager;
        RefreshAvailability();
    }

    public static void Dispose()
    {
        s_inputManager = null;
        s_controller = null;
    }

    public static void RefreshAvailabilityIfChanged(GameTime _)
    {
        if (IsEnabled != s_cutAvailability)
            RefreshAvailability();
    }

    public static void RefreshAvailability()
    {
        s_cutAvailability = IsEnabled;
        if (s_controller != null && s_controller.TryGetTarget(out var controller))
            UpdateVisibility.Invoke(controller, null);
    }

    public static void CancelActivePlacement()
    {
        if (s_controller != null && s_controller.TryGetTarget(out var controller) && controller.IsActive)
            s_inputManager?.DeactivateController(controller);
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
        (IsEnabled && proto.Id == IdsCore.Technology.CutTool) || unlocked.IsUnlocked(proto);

    private static IEnumerable<CodeInstruction> AllowConstructedSelection(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.PropertyGetter(typeof(IStaticEntity), nameof(IStaticEntity.ConstructionState)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(GetSelectableConstructionState)));

    private static ConstructionState GetSelectableConstructionState(IStaticEntity entity) =>
        IsEnabled && entity.ConstructionState == ConstructionState.Constructed
            ? ConstructionState.NotInitialized
            : entity.ConstructionState;

    private static void BeginCut() => s_completedCutIds = IsEnabled ? new HashSet<EntityId>() : null;

    private static IEnumerable<CodeInstruction> CaptureCompletedCutIds(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(EntitiesCloneConfigHelper), nameof(EntitiesCloneConfigHelper.CreateConfigFrom)),
            AccessTools.DeclaredMethod(typeof(CompletedBuildingCut), nameof(CreateCutConfig)));

    private static EntityConfigData CreateCutConfig(EntitiesCloneConfigHelper helper, IEntity entity)
    {
        if (s_completedCutIds != null && entity is IStaticEntity { ConstructionState: ConstructionState.Constructed })
            s_completedCutIds.Add(entity.Id);

        return helper.CreateConfigFrom(entity);
    }

    private static bool ValidateCompletedCut(EntitiesManager __instance, IStaticEntity entity, ref EntityValidationResult __result)
    {
        if (s_completedCutIds == null || entity.ConstructionState != ConstructionState.Constructed)
            return true;

        __result = __instance.CanRemoveEntity(entity, EntityRemoveReason.Remove);
        return false;
    }

    private static void RecordCut(CutEntitiesForPasteCmd cmd)
    {
        if (s_completedCutIds != null && cmd.CutConfigsOut is { IsNotEmpty: true } configs)
            UndoManager.RecordCut(configs, s_completedCutIds);
    }

    private static void EndCut() => s_completedCutIds = null;
}
