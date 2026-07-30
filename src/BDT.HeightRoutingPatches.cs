// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Factory.Transports;
using Mafi.Localization;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui.Controllers;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;
using UnityEngine;

namespace CoIDesignerToolkit;

/// <summary>
/// Implements the Transport Height-Routing Modifier (ASAP vs. Lazy height matching).
/// Allows players to hold Alt (or click the toolbox ramp icon to make it sticky) while laying transports
/// to keep the transport at its starting height for as long as possible before ramping near the goal.
/// </summary>
internal static class HeightRoutingPatches
{
    public const TransportPathFinderFlags LazyHeightMatchingFlag = (TransportPathFinderFlags)0x80;
    private static readonly ModLogger s_log = new ModLogger("BDT.HeightRouting");

    private static readonly FieldInfo? s_optimalRelZField =
        AccessTools.Field(typeof(TransportPathFinder), "m_optimalRelZ");

    private static readonly FieldInfo? s_goalRelCoordField =
        AccessTools.Field(typeof(TransportPathFinder), "m_goalRelCoord");

    private static readonly FieldInfo? s_toolboxField =
        AccessTools.Field(typeof(TransportBuildController), "m_toolbox");

    private static readonly FieldInfo? s_noTurnBtnField =
        AccessTools.Field(typeof(TransportBuildController), "m_noTurnBtn");

    private static readonly FieldInfo? s_shortcutsManagerField =
        AccessTools.Field(typeof(TransportBuildController), "m_shortcutsManager");

    private static readonly FieldInfo? s_lastMousePosField =
        AccessTools.Field(typeof(TransportBuildController), "m_lastMousePos");

    private static readonly FieldInfo? s_currProtoField =
        AccessTools.Field(typeof(TransportBuildController), "m_currTransportProto");

    private const string RampIconPath = "Assets/Unity/UserInterface/Toolbar/Ramp.svg";

    private static ToolboxItem? s_lazyHeightBtn;
    private static bool s_manualToggleState;

    public static bool IsLazyHeightActive { get; private set; }

    public static void Apply(Harmony harmony)
    {
        try
        {
            ConstructorInfo[] ctors = typeof(TransportBuildController).GetConstructors();
            if (ctors.Length > 0)
            {
                harmony.Patch(
                    ctors[0],
                    postfix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(TransportBuildController_Ctor_Postfix))
                );
            }

            harmony.Patch(
                AccessTools.Method(typeof(TransportBuildController), nameof(TransportBuildController.InputUpdate)),
                postfix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(TransportBuildController_InputUpdate_Postfix))
            );

            harmony.Patch(
                AccessTools.Method(typeof(PathFindingTransportPreview), nameof(PathFindingTransportPreview.ShowStartPreview)),
                prefix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(ShowStartPreview_Prefix))
            );

            harmony.Patch(
                AccessTools.Method(typeof(PathFindingTransportPreview), nameof(PathFindingTransportPreview.ShowContinuationPreview)),
                prefix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(ShowContinuationPreview_Prefix))
            );

            MethodInfo? initGoalDataMethod = AccessTools.Method(typeof(TransportPathFinder), "initGoalData");
            if (initGoalDataMethod != null)
            {
                harmony.Patch(
                    initGoalDataMethod,
                    postfix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(InitGoalData_Postfix))
                );
            }

            MethodInfo? tryGetStepCostMethod = AccessTools.Method(typeof(TransportPathFinder), "tryGetStepCost");
            if (tryGetStepCostMethod != null)
            {
                harmony.Patch(
                    tryGetStepCostMethod,
                    postfix: new HarmonyMethod(typeof(HeightRoutingPatches), nameof(TryGetStepCost_Postfix))
                );
            }

            s_log.Info("Transport height-routing patches applied successfully.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to apply transport height-routing patches.");
        }
    }

    private static void TransportBuildController_Ctor_Postfix(TransportBuildController __instance)
    {
        try
        {
            if (s_toolboxField?.GetValue(__instance) is Toolbox toolbox)
            {
                s_lazyHeightBtn = toolbox.AddEntry(
                    RampIconPath,
                    GetDisplayKeyBindings,
                    OnToolboxBtnClick,
                    BdtLocalization.HeightRoutingTooltip
                );

                ToolboxItem? noTurnBtn = s_noTurnBtnField?.GetValue(__instance) as ToolboxItem;
                ReorderToolboxItemAfter(toolbox, s_lazyHeightBtn, noTurnBtn);

                s_log.Info("Successfully added Height Routing button to TransportBuildController toolbox.");
            }
            else
            {
                s_log.Warning("Could not resolve m_toolbox from TransportBuildController.");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to add height-routing button to TransportBuildController toolbox.");
        }
    }

    private static void ReorderToolboxItemAfter(Toolbox toolbox, ToolboxItem item, ToolboxItem? anchorItem)
    {
        try
        {
            FieldInfo? bodyField = AccessTools.Field(typeof(Toolbox), "m_body");
            FieldInfo? itemsField = AccessTools.Field(typeof(Toolbox), "m_toolboxItems");
            FieldInfo? dividersField = AccessTools.Field(typeof(Toolbox), "m_verticalDividers");
            MethodInfo? updateDividersMethod = AccessTools.Method(typeof(Toolbox), "updateDividers");

            UiComponent? body = bodyField?.GetValue(toolbox) as UiComponent;
            Lyst<ToolboxItem>? items = itemsField?.GetValue(toolbox) as Lyst<ToolboxItem>;
            Lyst<VerticalDivider>? dividers = dividersField?.GetValue(toolbox) as Lyst<VerticalDivider>;

            if (items != null && dividers != null && body != null)
            {
                int currentIndex = items.IndexOf(item);
                if (currentIndex < 0) return;

                int targetIndex = anchorItem != null ? items.IndexOf(anchorItem) + 1 : 4;
                if (targetIndex <= 0) targetIndex = 4;
                if (currentIndex == targetIndex) return;

                VerticalDivider div = dividers[currentIndex];

                items.RemoveAt(currentIndex);
                dividers.RemoveAt(currentIndex);

                int insertIndex = Math.Min(targetIndex, items.Count);
                items.Insert(insertIndex, item);
                dividers.Insert(insertIndex, div);

                item.RemoveFromHierarchy();
                div.RemoveFromHierarchy();

                int bodyInsertIndex = insertIndex * 2;
                body.InsertAt(bodyInsertIndex, item);
                body.InsertAt(bodyInsertIndex + 1, div);

                updateDividersMethod?.Invoke(toolbox, null);
                s_log.Info($"Successfully placed Height Routing button after Shift modifier at toolbox index {insertIndex}.");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to reorder Height Routing button in toolbox.");
        }
    }

    private static KeyBindings GetDisplayKeyBindings(ShortcutsManager manager)
    {
        KeyBindings kb = HotkeysRegistry.TransportLazyHeight;
        if (kb.Primary.IsEmpty && !kb.Secondary.IsEmpty)
        {
            return new KeyBindings(kb.Mode, kb.Secondary, KeyBinding.Empty(kb.Secondary.Category));
        }
        return kb;
    }

    private static void OnToolboxBtnClick()
    {
        s_manualToggleState = !s_manualToggleState;
        HotkeysRegistry.PlayClickSound();
    }

    private static void TransportBuildController_InputUpdate_Postfix(TransportBuildController __instance, ref bool __result)
    {
        if (!__instance.IsActive)
        {
            IsLazyHeightActive = false;
            s_manualToggleState = false;
            return;
        }

        bool supportsElevation = false;
        if (s_currProtoField?.GetValue(__instance) is Option<TransportProto> optProto && optProto.HasValue)
        {
            supportsElevation = optProto.Value.CanGoUpDown;
        }

        Toolbox? toolbox = s_toolboxField?.GetValue(__instance) as Toolbox;

        if (!supportsElevation)
        {
            IsLazyHeightActive = false;
            s_manualToggleState = false;
            if (s_lazyHeightBtn != null && toolbox != null)
            {
                toolbox.SetEntryVisible(s_lazyHeightBtn, false);
            }
            return;
        }

        ShortcutsManager? manager = s_shortcutsManagerField?.GetValue(__instance) as ShortcutsManager;
        if (s_lazyHeightBtn != null && manager != null)
        {
            if (toolbox != null)
            {
                toolbox.SetEntryVisible(s_lazyHeightBtn, true);
            }
            s_lazyHeightBtn.Update(manager);
        }

        bool isHotKeyHeld = manager != null && manager.IsOn(HotkeysRegistry.TransportLazyHeight);

        bool newState = isHotKeyHeld || s_manualToggleState;

        if (IsLazyHeightActive != newState)
        {
            IsLazyHeightActive = newState;
            s_lastMousePosField?.SetValue(__instance, Vector3.zero);
            __result = true;
        }

        if (s_lazyHeightBtn != null)
        {
            s_lazyHeightBtn.Selected(IsLazyHeightActive);
        }
    }

    private static void ShowStartPreview_Prefix(ref PathFindingTransportPreview.PreviewRequest request)
    {
        if (IsLazyHeightActive)
        {
            TransportPathFinderOptions newOptions = new TransportPathFinderOptions(
                request.PathFinderOptions.PreferredHeight,
                request.PathFinderOptions.ForcedStartDirection,
                request.PathFinderOptions.BannedStartDirections,
                request.PathFinderOptions.Flags | LazyHeightMatchingFlag
            );

            request = PathFindingTransportPreview.PreviewRequest.CreateStartRequest(
                request.Proto,
                request.NewPosition,
                newOptions,
                request.StartDirection,
                request.DisablePortSnapping
            );
        }
    }

    private static void ShowContinuationPreview_Prefix(ref PathFindingTransportPreview.PreviewRequest request)
    {
        if (IsLazyHeightActive)
        {
            TransportPathFinderOptions newOptions = new TransportPathFinderOptions(
                request.PathFinderOptions.PreferredHeight,
                request.PathFinderOptions.ForcedStartDirection,
                request.PathFinderOptions.BannedStartDirections,
                request.PathFinderOptions.Flags | LazyHeightMatchingFlag
            );

            request = PathFindingTransportPreview.PreviewRequest.CreateContRequest(
                request.Proto,
                request.NewPosition,
                request.Pivots,
                request.PillarHints,
                request.ExistingTrajectory,
                newOptions,
                request.BannedTiles,
                request.StartDirection,
                request.DisablePortSnapping
            );
        }
    }

    private static void InitGoalData_Postfix(TransportPathFinder __instance)
    {
        if (__instance.Options.HasFlags(LazyHeightMatchingFlag))
        {
            s_optimalRelZField?.SetValue(__instance, 8); // 8 is START_REL_COORD.Z
        }
    }

    private static void TryGetStepCost_Postfix(TransportPathFinder __instance, int nodeIndex, int parentIndex, ref int outCost, ref bool __result)
    {
        if (__result && __instance.Options.HasFlags(LazyHeightMatchingFlag))
        {
            int z = (nodeIndex >> 12) & 0xF;
            if (z != 8) // 8 is START_REL_COORD.Z
            {
                if (s_goalRelCoordField?.GetValue(__instance) is RelTile3i goalRel)
                {
                    int nodeX = nodeIndex & 0x3F;
                    int nodeY = (nodeIndex >> 6) & 0x3F;
                    int distToGoal2D = Math.Abs(goalRel.X - nodeX) + Math.Abs(goalRel.Y - nodeY);
                    outCost += distToGoal2D * 100;
                }
            }
        }
    }
}
