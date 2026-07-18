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
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;
using Mafi.Localization;
using Mafi.Unity.Ui.Blueprints;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Injects an "Update" button into the blueprint placement panel.
/// When clicked, activates area selection; the selected content replaces the
/// current blueprint while preserving its name, description, overlap deltas, and
/// folder position.
/// </summary>
internal static class BlueprintUpdater
{
    private static readonly ModLogger s_log = new ModLogger("BDT.BpUpdate");

    private const string REPLACE_ICON = "Assets/Unity/UserInterface/General/Replace.svg";

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var windowType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintsWindow");
            if (windowType == null)
            {
                s_log.Warning("BlueprintsWindow type not found — skipping Update button.");
                return;
            }

            var ctors = windowType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length == 0)
            {
                s_log.Warning("No constructors found on BlueprintsWindow.");
                return;
            }

            harmony.Patch(ctors[0],
                postfix: new HarmonyMethod(typeof(BlueprintUpdater), nameof(WindowCtorPostfix)));
            s_log.Info("Patched BlueprintsWindow constructor.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "BlueprintUpdater.ApplyPatches");
        }
    }

    // Postfix runs after the BlueprintsWindow constructor.
    // Harmony matches the 'blueprintCreationController' parameter by name.
    private static void WindowCtorPostfix(object __instance, BlueprintCreationController blueprintCreationController)
    {
        try
        {
            var window = (BlueprintsWindow)__instance;

            // m_placementPanel moved from BlueprintsWindow to BlueprintsLibraryTab in 0.8.6.
            // The old host is still Update 4.1, so remove this compatibility call
            // only when Update 4.1 itself is no longer supported; see GameApiCompat.
            UiComponent innerRow = GameApiCompat.GetPlacementPanelFirstChild(window);

            var updateBtn = new ButtonIconText(Button.Primary, REPLACE_ICON, "Update".AsLoc())
                .Tooltip("Update selected blueprint from a new area selection".AsLoc());
            updateBtn.OnClick(() => OnUpdateClick(window, blueprintCreationController));

            innerRow.Add(new VerticalDivider().MarginLeftRight(2.pt()));
            innerRow.Add(updateBtn);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "WindowCtorPostfix");
        }
    }

    private static void OnUpdateClick(BlueprintsWindow window, BlueprintCreationController blueprintCreationController)
    {
        if (!GameApiCompat.TryGetSelectedBlueprint(window, out IBlueprint blueprint))
            return;

        object host = GameApiCompat.GetBlueprintLibraryHost(window);
        IBlueprintsFolder folder = GameApiCompat.GetCurrentFolder(host);

        // Capture metadata before the selection tool deactivates the window.
        string name = blueprint.Name;
        string desc = blueprint.Desc;
        int overlapX = blueprint.OverlapDeltaX;
        int overlapY = blueprint.OverlapDeltaY;

        // Find the blueprint's current position so we can restore it after replacement.
        int bpIndex = -1;
        for (int i = 0; i < folder.Blueprints.Count; i++)
        {
            if (folder.Blueprints[i] == blueprint)
            {
                bpIndex = i;
                break;
            }
        }

        blueprintCreationController.ActivateForSelection(
            (ImmutableArray<EntityConfigData> items,
             ImmutableArray<TileSurfaceCopyPasteData> surfaces,
             ImmutableArray<TileSurfaceCopyPasteData> decals) =>
            {
                window.BlueprintsLibrary.DeleteItem(folder, blueprint);
                Option<IBlueprint> newBpOpt = window.BlueprintsLibrary.AddBlueprint(folder, items, surfaces, decals);

                if (newBpOpt.HasValue)
                {
                    IBlueprint newBp = newBpOpt.Value;
                    window.BlueprintsLibrary.RenameItem(newBp, name);
                    window.BlueprintsLibrary.SetDescription(newBp, desc);
                    window.BlueprintsLibrary.SetOverlapDeltas(newBp, overlapX, overlapY);

                    // Reorder to the original slot. TryReorderItem's newIndex addresses a combined
                    // list of [all folders, then all blueprints] in the parent folder.
                    if (bpIndex >= 0)
                        window.BlueprintsLibrary.TryReorderItem(newBp, folder, folder.Folders.Count + bpIndex);

                    // Signal the window to auto-select the new blueprint on re-activation,
                    // which drives a Detail panel refresh.
                    GameApiCompat.SetNewBlueprintItem(window, newBpOpt.As<IBlueprintItem>());
                }

                GameApiCompat.ActivateBlueprintController(window);
            });
    }
}
