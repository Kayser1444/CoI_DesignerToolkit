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

    // Current BlueprintDetail stores the item/cost/missing-content controls in a
    // nested BlueprintContentSummary at child index 2. Older game versions put
    // those controls directly on BlueprintDetail, so the helper below supports
    // both layouts.
    private const int CONTENT_INDEX = 2;

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

            // Rename the vanilla "Cost:" label to "Construction cost:". In
            // current builds it is nested inside BlueprintContentSummary;
            // directly casting detail[2] was the source of the reported
            // InvalidCastException.
            RenameConstructionCost(detail);

            // Build an ops section: heading + tiles row, hidden until a blueprint with stats is shown.
            var tilesRow = new Row().Wrap().MarginTop(1.pt());
            var opsSection = new Column()
                .MarginTop(2.pt())
                .Visible(false);
            opsSection.Add(
                new Label(BdtLocalization.BlueprintOperationalCost.AsFormatted).FontBold(),
                tilesRow);

            // Put operational stats immediately after the content/cost block.
            // The fallback keeps compatibility with the older flattened layout.
            int insertIndex = detail.ChildrenCount;
            if (detail.ChildrenCount > CONTENT_INDEX)
                insertIndex = detail[CONTENT_INDEX] is Column ? CONTENT_INDEX + 1 : CONTENT_INDEX + 2;
            detail.InsertAt(Math.Min(insertIndex, detail.ChildrenCount), opsSection);
            s_opsSections.Add(__instance, opsSection);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "DetailCtorPostfix");
        }
    }

    private static void RenameConstructionCost(Column detail)
    {
        if (detail.ChildrenCount <= CONTENT_INDEX)
            return;

        if (detail[CONTENT_INDEX] is IComponentWithText directCostTitle)
        {
            directCostTitle.SetValue(BdtLocalization.BlueprintConstructionCost.AsFormatted);
            return;
        }

        if (detail[CONTENT_INDEX] is Column content && content.ChildrenCount > 1 &&
            content[1] is IComponentWithText nestedCostTitle)
        {
            nestedCostTitle.SetValue(BdtLocalization.BlueprintConstructionCost.AsFormatted);
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
        col.AlignItemsCenter().MarginRight(2.pt());
        col.Add(
            new Icon(iconPath).Size(36.px()).Tooltip(tooltip),
            new Label(value).FontBold().TextCenterMiddle().MarginTopBottom(1.pt()));
        return col;
    }
}
