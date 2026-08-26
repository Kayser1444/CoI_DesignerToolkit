// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.WellPumps;
using Mafi.Core.Syncers;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Generation;
using Mafi.Localization;
using Mafi.Unity.Entities;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Inspectors;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.UiToolkit.Library.FloatingPanel;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class GroundwaterInspectorPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.GroundwaterInspectorPatches");
    private static readonly string STATS_ICON_PATH = "Assets/Unity/UserInterface/Toolbar/Stats.svg";

    public static void Apply(Harmony harmony)
    {
        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var type = assembly.GetType("Mafi.Unity.Ui.Inspectors.MachineInspector");
            if (type == null)
            {
                s_log.Warning("MachineInspector type not found");
                return;
            }

            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length > 0)
            {
                harmony.Patch(ctors[0], postfix: new HarmonyMethod(typeof(GroundwaterInspectorPatches), nameof(InspectorCtorPostfix)));
                s_log.Info("Patched MachineInspector constructor for groundwater rich tooltip");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply GroundwaterInspectorPatches: {ex.Message}");
        }
    }

    public static void InspectorCtorPostfix(object __instance)
    {
        try
        {
            var inspectorType = __instance.GetType();
            PropertyInfo? entityProp = inspectorType.GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? mainBodyField = inspectorType.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var currentType = inspectorType;
            while (currentType != null && (entityProp == null || mainBodyField == null))
            {
                entityProp ??= currentType.GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                mainBodyField ??= currentType.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                currentType = currentType.BaseType;
            }

            if (entityProp == null || mainBodyField == null) return;

            var mainBody = mainBodyField.GetValue(__instance) as Column;
            var uiComponent = __instance as UiComponent;
            if (mainBody == null || uiComponent == null) return;

            // Search for the specific reservePanel inside MainBody (the one containing ProductBufferUi)
            PanelWithHeader? reservePanel = null;
            foreach (var child in mainBody)
            {
                if (child is PanelWithHeader p)
                {
                    foreach (var bodyChild in p.Body)
                    {
                        if (bodyChild is ProductBufferUi)
                        {
                            reservePanel = p;
                            break;
                        }
                    }
                    if (reservePanel != null) break;
                }
            }

            if (reservePanel == null)
            {
                s_log.Warning("Could not find reservePanel containing ProductBufferUi in MachineInspector");
                return;
            }

            // Find the title label in TitleRow
            Label? titleLabel = null;
            foreach (var titleChild in reservePanel.TitleRow)
            {
                if (titleChild is Label l)
                {
                    titleLabel = l;
                    break;
                }
            }

            // Create our rich tooltip component
            var tooltipUi = new GroundwaterReserveTooltipUi();

            // Create stats icon button in title row
            var statsIcon = new Icon(STATS_ICON_PATH)
                .Size(16.px())
                .MarginLeft(4.pt())
                .Color(Theme.PrimaryColor);

            statsIcon.FloaterInteractive(() => tooltipUi);
            reservePanel.TitleRow.Add(statsIcon);

            if (titleLabel != null)
            {
                titleLabel.FloaterInteractive(() => tooltipUi);
            }

            // Observe entity changes to update tooltip visibility and data
            uiComponent.Observe(() => entityProp.GetValue(__instance) as Machine)
                .Do(machine =>
                {
                    if (machine is WellPump wellPump)
                    {
                        if (titleLabel != null)
                        {
                            titleLabel.InfoIconPosition(Label.InfoIconPos.None);
                            titleLabel.Tooltip(LocStrFormatted.Empty);
                        }
                        statsIcon.Show();
                        var resource = GetResourceForPump(wellPump);
                        tooltipUi.UpdateData(wellPump, resource);
                    }
                    else if (machine is IVirtualResourceMiningEntity miningEntity)
                    {
                        if (titleLabel != null)
                        {
                            titleLabel.InfoIconPosition(Label.InfoIconPos.None);
                            titleLabel.Tooltip(LocStrFormatted.Empty);
                        }
                        statsIcon.Show();
                        tooltipUi.UpdateData(miningEntity, null);
                    }
                    else
                    {
                        statsIcon.Hide();
                    }
                });

            // Observe buffer changes for ongoing live updates
            uiComponent.Observe(() => (entityProp.GetValue(__instance) as IVirtualResourceMiningEntity)?.QuantityLeftToMine)
                .Do(_ =>
                {
                    if (entityProp.GetValue(__instance) is WellPump pump)
                    {
                        if (titleLabel != null)
                        {
                            titleLabel.InfoIconPosition(Label.InfoIconPos.None);
                            titleLabel.Tooltip(LocStrFormatted.Empty);
                        }
                        var resource = GetResourceForPump(pump);
                        tooltipUi.UpdateData(pump, resource);
                    }
                });
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in InspectorCtorPostfix for groundwater tooltip: {ex}");
        }
    }

    private static IVirtualTerrainResource? GetResourceForPump(WellPump pump)
    {
        try
        {
            var resField = typeof(WellPump).GetField("m_resource", BindingFlags.Instance | BindingFlags.NonPublic);
            if (resField != null)
            {
                var option = (Option<IVirtualTerrainResource>)resField.GetValue(pump);
                if (option.HasValue) return option.Value;
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to get resource from pump field: {ex.Message}");
        }

        try
        {
            var resource = GroundwaterStatsManager.Instance?.GetResourceAt(pump.ProductToMine, pump.Transform.Position.Tile2i);
            if (resource != null) return resource;
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to get resource from GroundwaterStatsManager: {ex.Message}");
        }

        return null;
    }
}
