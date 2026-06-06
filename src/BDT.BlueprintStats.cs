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
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Patches <c>BlueprintDetail</c> to inject a summary row showing workers, electricity,
/// computing, and maintenance for all entities in the selected blueprint.
/// The row is inserted after the Construction Cost row.
/// </summary>
internal static class BlueprintStats
{
    private static readonly ModLogger s_log = new ModLogger("BDT.BpStats");

    // Stores the ops-section Column (heading + tiles row) keyed by BlueprintDetail instance.
    private static readonly ConditionalWeakTable<object, Column> s_opsSections =
        new ConditionalWeakTable<object, Column>();

    private const string ICON_WORKERS     = "Assets/Unity/UserInterface/General/WorkerSmall.svg";
    private const string ICON_ELECTRICITY = "Assets/Unity/UserInterface/General/ElectricityColored.svg";
    private const string ICON_COMPUTING   = "Assets/Unity/UserInterface/General/Computing128.png";

    // BlueprintDetail adds children in this order (single Add() call, then a second):
    //   [0] Title  [1] m_items  [2] m_costToBuildTitle  [3] m_costRow
    //   [4] m_failedToLoadTitle  [5] m_failedToLoadData  [6] DescField  [7] row (bottom)
    // We rename [2] and insert our ops section at index 4 (just after m_costRow).
    private const int OPS_SECTION_INSERT_INDEX = 4;
    // Index of m_costToBuildTitle inside BlueprintDetail's child list.
    private const int COST_TITLE_INDEX = 2;

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var detailType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintDetail");
            if (detailType == null)
            {
                s_log.Warning("BlueprintDetail type not found — skipping Blueprint Stats.");
                return;
            }

            var ctors = detailType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length == 0)
            {
                s_log.Warning("No constructors found on BlueprintDetail.");
                return;
            }

            harmony.Patch(ctors[0],
                postfix: new HarmonyMethod(typeof(BlueprintStats), nameof(DetailCtorPostfix)));

            var setBp = detailType.GetMethod(
                "SetBlueprint",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setBp == null)
            {
                s_log.Warning("SetBlueprint method not found on BlueprintDetail.");
                return;
            }

            harmony.Patch(setBp,
                postfix: new HarmonyMethod(typeof(BlueprintStats), nameof(SetBlueprintPostfix)));

            s_log.Info("Patched BlueprintDetail constructor and SetBlueprint.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "BlueprintStats.ApplyPatches");
        }
    }

    private static void DetailCtorPostfix(object __instance)
    {
        try
        {
            var detail = (Column)__instance;

            // Rename the vanilla "Cost:" label to "Construction cost:".
            ((IComponentWithText)detail[COST_TITLE_INDEX]).SetValue("Construction cost:".AsLoc());

            // Build an ops section: heading + tiles row, hidden until a blueprint with stats is shown.
            var tilesRow = new Row().Wrap().MarginTop(1.pt());
            var opsSection = new Column()
                .MarginTop(2.pt())
                .Visible(false);
            opsSection.Add(
                new Label("Operational cost:".AsLoc()).FontBold(),
                tilesRow);

            detail.InsertAt(OPS_SECTION_INSERT_INDEX, opsSection);
            s_opsSections.Add(__instance, opsSection);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "DetailCtorPostfix");
        }
    }

    private static void SetBlueprintPostfix(object __instance, IBlueprint blueprint)
    {
        try
        {
            if (!s_opsSections.TryGetValue(__instance, out Column opsSection))
                return;

            // [0] = heading label, [1] = tiles row
            var tilesRow = (Row)opsSection[1];
            tilesRow.Clear();

            if (blueprint == null)
            {
                opsSection.Visible(false);
                return;
            }

            int workers = 0;
            int elecKw  = 0;
            int compTf  = 0;
            var maintenanceByProduct = new Dictionary<VirtualProductProto, Fix32>();

            foreach (EntityConfigData item in blueprint.Items)
            {
                var proto = item.Prototype.ValueOrNull;

                if (proto is IEntityProto entityProto)
                {
                    workers += entityProto.Costs.Workers;

                    if (entityProto.Costs.Maintenance.MaxMaintenancePerMonth.IsPositive)
                    {
                        var product = entityProto.Costs.Maintenance.Product;
                        maintenanceByProduct.TryGetValue(product, out Fix32 existing);
                        maintenanceByProduct[product] =
                            existing + entityProto.Costs.Maintenance.MaxMaintenancePerMonth.Value;
                    }
                }

                if (proto is IProtoWithPowerConsumption elecProto && elecProto.ElectricityConsumed.IsPositive)
                    elecKw += elecProto.ElectricityConsumed.Value;

                if (proto is IProtoWithComputingConsumption compProto && compProto.ComputingConsumed.IsPositive)
                    compTf += compProto.ComputingConsumed.Value;
            }

            if (workers > 0)
                tilesRow.Add(MakeTile(ICON_WORKERS, workers.ToString().AsLoc(), Tr.EntityWorkersRequiredTooltip));

            if (elecKw > 0)
                tilesRow.Add(MakeTile(ICON_ELECTRICITY, new Electricity(elecKw).Format(), Tr.EntityElectricityConsumptionTooltip));

            if (compTf > 0)
                tilesRow.Add(MakeTile(ICON_COMPUTING, new Computing(compTf).FormatShort(), Tr.EntityComputingConsumptionTooltip));

            foreach (KeyValuePair<VirtualProductProto, Fix32> kvp in maintenanceByProduct)
            {
                if (kvp.Value > Fix32.Zero)
                    tilesRow.Add(MakeTile(kvp.Key.IconPath, kvp.Value.ToStringRoundedAdaptive().AsLoc(), kvp.Key.Strings.Name));
            }

            opsSection.Visible(tilesRow.IsNotEmpty);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "SetBlueprintPostfix");
        }
    }

    private static Column MakeTile(string iconPath, LocStrFormatted value, LocStr tooltip)
    {
        var col = new Column();
        col.AlignItemsStretch().MarginRight(2.pt());
        col.Add(
            new Icon(iconPath).Size(36.px()).Tooltip(tooltip),
            new Label(value).FontBold().TextCenterMiddle().MarginTopBottom(1.pt()));
        return col;
    }
}
