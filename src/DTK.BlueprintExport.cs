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
using System.Text;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Economy;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Maintenance;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;
using UnityEngine;
using StringComparer = System.StringComparer;

namespace CoIDesignerToolkit;

/// <summary>
/// Injects a "Copy as Markdown" button into <c>BlueprintDetail</c> and <c>BlueprintFolderDetail</c>.
/// For a single blueprint the button copies two separate tables: Operational stats and Construction cost,
/// plus a Components breakdown. For a folder it copies a single wide table with one row per blueprint.
/// </summary>
internal static class BlueprintExport
{
    private static readonly ModLogger s_log = new ModLogger("DTK.BpExport");

    private const string CLIPBOARD_ICON = "Assets/Unity/UserInterface/General/Clipboard.svg";

    // ── Single-blueprint state ──────────────────────────────────────────────
    private sealed class State
    {
        public IBlueprint? Blueprint;
        public ButtonIconText? CopyBtn;
    }

    private static readonly ConditionalWeakTable<object, State> s_states =
        new ConditionalWeakTable<object, State>();

    // ── Folder state ────────────────────────────────────────────────────────
    private sealed class FolderState
    {
        public IBlueprintsFolder? Folder;
        public ButtonIconText? CopyBtn;
    }

    private static readonly ConditionalWeakTable<object, FolderState> s_folderStates =
        new ConditionalWeakTable<object, FolderState>();

    // ── Per-blueprint computed stats ────────────────────────────────────────
    private struct BpStats
    {
        public string Name;
        /// <summary>Relative folder path within the exported root; empty string = direct child of the root folder.</summary>
        public string FolderPath;
        public int Entities;
        public int Workers;
        public int ElecKw;
        public int CompTf;
        /// <summary>Maintenance product display-name → formatted value (A-Z keyed).</summary>
        public SortedDictionary<string, string> MaintenanceValues;
        /// <summary>Construction product display-name → formatted quantity (A-Z keyed).</summary>
        public SortedDictionary<string, string> ConstructionValues;
        /// <summary>Component entity display-name → count (A-Z keyed).</summary>
        public SortedDictionary<string, int> ComponentCounts;
    }

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;

            // ── BlueprintDetail ──────────────────────────────────────────────
            var detailType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintDetail");
            if (detailType == null)
            {
                s_log.Warning("BlueprintDetail type not found — skipping Blueprint Export.");
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
                postfix: new HarmonyMethod(typeof(BlueprintExport), nameof(DetailCtorPostfix)));

            var setBp = detailType.GetMethod(
                "SetBlueprint",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setBp == null)
            {
                s_log.Warning("SetBlueprint method not found on BlueprintDetail.");
                return;
            }

            harmony.Patch(setBp,
                postfix: new HarmonyMethod(typeof(BlueprintExport), nameof(SetBlueprintPostfix)));

            s_log.Info("Patched BlueprintDetail for markdown export.");

            // ── BlueprintFolderDetail ────────────────────────────────────────
            var folderDetailType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintFolderDetail");
            if (folderDetailType == null)
            {
                s_log.Warning("BlueprintFolderDetail type not found — skipping folder export.");
                return;
            }

            var folderCtors = folderDetailType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (folderCtors.Length > 0)
                harmony.Patch(folderCtors[0],
                    postfix: new HarmonyMethod(typeof(BlueprintExport), nameof(FolderCtorPostfix)));

            var setFolder = folderDetailType.GetMethod(
                "SetFolder",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setFolder != null)
                harmony.Patch(setFolder,
                    postfix: new HarmonyMethod(typeof(BlueprintExport), nameof(SetFolderPostfix)));

            s_log.Info("Patched BlueprintFolderDetail for folder markdown export.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "BlueprintExport.ApplyPatches");
        }
    }

    private static void DetailCtorPostfix(object __instance)
    {
        try
        {
            var detail = (Column)__instance;
            var state = new State();
            s_states.Add(__instance, state);

            var copyBtn = new ButtonIconText(Button.General, CLIPBOARD_ICON, "Copy as Markdown".AsLoc())
                .Tooltip("Copy blueprint stats as a Markdown table to the clipboard, ready to paste into the Hub.".AsLoc())
                .Visible(false);

            copyBtn.OnClick(() =>
            {
                if (state.Blueprint == null) return;
                try
                {
                    GUIUtility.systemCopyBuffer = BuildMarkdown(state.Blueprint);
                    s_log.Info($"Copied markdown for '{state.Blueprint.Name}' to clipboard.");
                }
                catch (Exception ex)
                {
                    s_log.Exception(ex, "OnCopyClick");
                }
            });

            state.CopyBtn = copyBtn;
            // Insert next to the Placement overlap button (index 0) in the bottom row.
            // The version label at index 1 has FlexGrow so it fills remaining space.
            var bottomRow = (Row)detail[detail.ChildrenCount - 1];
            bottomRow.InsertAt(1, copyBtn);
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
            if (!s_states.TryGetValue(__instance, out State state)) return;
            state.Blueprint = blueprint;
            state.CopyBtn?.Visible(blueprint != null);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "SetBlueprintPostfix");
        }
    }

    // ── Folder patches ──────────────────────────────────────────────────────

    private static void FolderCtorPostfix(object __instance)
    {
        try
        {
            var detail = (Column)__instance;
            var folderState = new FolderState();
            s_folderStates.Add(__instance, folderState);

            var copyBtn = new ButtonIconText(Button.General, CLIPBOARD_ICON, "Copy as Markdown".AsLoc())
                .Tooltip("Copy folder blueprint list as a Markdown table to the clipboard.".AsLoc())
                .Visible(false);

            copyBtn.OnClick(() =>
            {
                if (folderState.Folder == null) return;
                try
                {
                    GUIUtility.systemCopyBuffer = BuildFolderMarkdown(folderState.Folder);
                    s_log.Info($"Copied folder markdown for '{folderState.Folder.Name}' to clipboard.");
                }
                catch (Exception ex)
                {
                    s_log.Exception(ex, "OnFolderCopyClick");
                }
            });

            folderState.CopyBtn = copyBtn;
            // Wrap in a row so the button doesn't stretch to fill the column width.
            var btnRow = new Row(2.pt()).AlignItemsCenterMiddle().MarginTop(2.pt());
            btnRow.Add(copyBtn);
            detail.Add(btnRow);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "FolderCtorPostfix");
        }
    }

    private static void SetFolderPostfix(object __instance, IBlueprintsFolder folder)
    {
        try
        {
            if (!s_folderStates.TryGetValue(__instance, out FolderState folderState)) return;
            folderState.Folder = folder;
            folderState.CopyBtn?.Visible(folder != null && HasAnyBlueprints(folder));
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "SetFolderPostfix");
        }
    }

    // ── Markdown builders ───────────────────────────────────────────────────

    /// <summary>
    /// Computes all stats for a single blueprint into a <see cref="BpStats"/> struct.
    /// </summary>
    private static BpStats ComputeBpStats(IBlueprint blueprint)
    {
        var stats = new BpStats
        {
            Name                = blueprint.Name,
            Entities            = blueprint.Items.Length,
            MaintenanceValues   = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ConstructionValues  = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ComponentCounts     = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        };

        var maintenanceByProduct = new Dictionary<VirtualProductProto, Fix32>();
        var constructionCost     = new MutableAssetValue();

        foreach (EntityConfigData item in blueprint.Items)
        {
            var proto = item.Prototype.ValueOrNull;

            if (proto is IEntityProto entityProto)
            {
                stats.Workers += entityProto.Costs.Workers;

                if (entityProto.Costs.Maintenance.MaxMaintenancePerMonth.IsPositive)
                {
                    var product = entityProto.Costs.Maintenance.Product;
                    maintenanceByProduct.TryGetValue(product, out Fix32 existing);
                    maintenanceByProduct[product] =
                        existing + entityProto.Costs.Maintenance.MaxMaintenancePerMonth.Value;
                }
            }

            if (proto is IProtoWithPowerConsumption elecProto && elecProto.ElectricityConsumed.IsPositive)
                stats.ElecKw += elecProto.ElectricityConsumed.Value;

            if (proto is IProtoWithComputingConsumption compProto && compProto.ComputingConsumed.IsPositive)
                stats.CompTf += compProto.ComputingConsumed.Value;

            if (proto is TransportProto tp && item.Trajectory.HasValue)
                constructionCost.Add(tp.GetPriceFor(item.Trajectory.Value.Pivots));
            else if (proto is LayoutEntityProto lep)
                constructionCost.Add(lep.Costs.BaseConstructionCost);
        }

        // Maintenance — stored A-Z by product name
        foreach (var kvp in maintenanceByProduct)
            if (kvp.Value > Fix32.Zero)
                stats.MaintenanceValues[kvp.Key.Strings.Name.TranslatedString] =
                    kvp.Value.ToStringRoundedAdaptive();

        // Construction cost — stored A-Z by product name
        var products = constructionCost.Products;
        for (int i = 0; i < products.Length; i++)
        {
            var pq = products[i];
            stats.ConstructionValues[pq.Product.Strings.Name.TranslatedString] =
                pq.Quantity.Value.ToStringWithSiSuffix().Value;
        }

        // Component counts — stored A-Z by entity name
        foreach (KeyValuePair<Proto, int> kvp in blueprint.AllMajorProtos)
        {
            string name = kvp.Key.Strings.Name.TranslatedString;
            stats.ComponentCounts.TryGetValue(name, out int existing2);
            stats.ComponentCounts[name] = existing2 + kvp.Value;
        }

        return stats;
    }

    /// <summary>
    /// Single-blueprint Markdown: two separate tables (Operational / Construction) plus Components.
    /// </summary>
    internal static string BuildMarkdown(IBlueprint blueprint)
    {
        var s  = ComputeBpStats(blueprint);
        var sb = new StringBuilder();

        // Heading + description
        sb.AppendLine($"## {s.Name}");
        if (!string.IsNullOrWhiteSpace(blueprint.Desc))
        {
            sb.AppendLine();
            sb.AppendLine(blueprint.Desc.Trim());
        }

        // ── Components ───────────────────────────────────────────────────────
        if (s.ComponentCounts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Components");
            sb.AppendLine("| Entity | Count |");
            sb.AppendLine("|---|---|");
            foreach (var kvp in s.ComponentCounts)   // already A-Z
                sb.AppendLine($"| {kvp.Key} | {kvp.Value} |");
        }

        // ── Construction ─────────────────────────────────────────────────────
        if (s.ConstructionValues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Construction");
            sb.AppendLine("| Product | Quantity |");
            sb.AppendLine("|---|---|");
            foreach (var kvp in s.ConstructionValues)   // already A-Z
                sb.AppendLine($"| {kvp.Key} | {kvp.Value} |");
        }

        // ── Operational ──────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("### Operational");
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Entities | {s.Entities} |");
        if (s.Workers > 0) sb.AppendLine($"| Workers | {s.Workers} |");
        if (s.ElecKw  > 0) sb.AppendLine($"| Electricity | {FormatElectricity(s.ElecKw)} |");
        if (s.CompTf  > 0) sb.AppendLine($"| Computing | {s.CompTf} TF |");
        foreach (var kvp in s.MaintenanceValues)   // already A-Z
            sb.AppendLine($"| {kvp.Key} / mo | {kvp.Value} |");

        return sb.ToString();
    }

    /// <summary>
    /// Folder Markdown: one row per blueprint (including all sub-folders recursively).
    /// Columns: Blueprint | Folder | Entities [| Workers] [| Electricity] [| Computing]
    ///          [| {maint} / mo …] [| {constr product} …]
    /// Rows are sorted by folder path A-Z, then by blueprint name A-Z within the same folder.
    /// </summary>
    internal static string BuildFolderMarkdown(IBlueprintsFolder folder)
    {
        // ── Collect BPs from this folder and all sub-folders recursively ───
        var bpList = new List<BpStats>();
        CollectBps(folder, "", bpList);

        // Sort by folder path A-Z, then by name A-Z within the same folder
        bpList.Sort((a, b) =>
        {
            int c = string.Compare(a.FolderPath, b.FolderPath, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        // ── Discover dynamic columns ───────────────────────────────────────
        bool hasWorkers = false, hasElec = false, hasComp = false;
        var maintCols  = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var constrCols = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in bpList)
        {
            if (s.Workers > 0) hasWorkers = true;
            if (s.ElecKw  > 0) hasElec    = true;
            if (s.CompTf  > 0) hasComp    = true;
            foreach (var k in s.MaintenanceValues.Keys)  maintCols.Add(k);
            foreach (var k in s.ConstructionValues.Keys) constrCols.Add(k);
        }

        // ── Build table ────────────────────────────────────────────────────
        var sb = new StringBuilder();

        sb.AppendLine($"## {folder.Name}");
        if (!string.IsNullOrWhiteSpace(folder.Desc))
        {
            sb.AppendLine();
            sb.AppendLine(folder.Desc.Trim());
        }
        sb.AppendLine();

        // Header row
        sb.Append("| Blueprint | Folder | Entities |");
        if (hasWorkers) sb.Append(" Workers |");
        if (hasElec)    sb.Append(" Electricity |");
        if (hasComp)    sb.Append(" Computing |");
        foreach (var col in maintCols)  sb.Append($" {col} / mo |");
        foreach (var col in constrCols) sb.Append($" {col} |");
        sb.AppendLine();

        // Separator row
        sb.Append("|---|---|---|");
        if (hasWorkers) sb.Append("---|");
        if (hasElec)    sb.Append("---|");
        if (hasComp)    sb.Append("---|");
        foreach (var _ in maintCols)  sb.Append("---|");
        foreach (var _ in constrCols) sb.Append("---|");
        sb.AppendLine();

        // Data rows
        foreach (var s in bpList)
        {
            string folderCell = string.IsNullOrEmpty(s.FolderPath) ? "." : s.FolderPath;
            sb.Append($"| {s.Name} | {folderCell} | {s.Entities} |");
            if (hasWorkers) sb.Append(s.Workers > 0 ? $" {s.Workers} |" : " - |");
            if (hasElec)    sb.Append(s.ElecKw  > 0 ? $" {FormatElectricity(s.ElecKw)} |" : " - |");
            if (hasComp)    sb.Append(s.CompTf  > 0 ? $" {s.CompTf} TF |" : " - |");
            foreach (var col in maintCols)
                sb.Append(s.MaintenanceValues.TryGetValue(col, out var mv) ? $" {mv} |" : " - |");
            foreach (var col in constrCols)
                sb.Append(s.ConstructionValues.TryGetValue(col, out var cv) ? $" {cv} |" : " - |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively collects all blueprints under <paramref name="folder"/> into <paramref name="result"/>,
    /// setting each entry's <see cref="BpStats.FolderPath"/> to the path relative to the root folder.
    /// </summary>
    private static void CollectBps(IBlueprintsFolder folder, string relativePath, List<BpStats> result)
    {
        for (int i = 0; i < folder.Blueprints.Count; i++)
        {
            var s = ComputeBpStats(folder.Blueprints[i]);
            s.FolderPath = relativePath;
            result.Add(s);
        }

        for (int i = 0; i < folder.Folders.Count; i++)
        {
            var sub = folder.Folders[i];
            string subPath = string.IsNullOrEmpty(relativePath)
                ? sub.Name
                : $"{relativePath}/{sub.Name}";
            CollectBps(sub, subPath, result);
        }
    }

    /// <summary>Returns true if <paramref name="folder"/> contains at least one blueprint anywhere in its tree.</summary>
    private static bool HasAnyBlueprints(IBlueprintsFolder folder)
    {
        if (folder.Blueprints.Count > 0) return true;
        for (int i = 0; i < folder.Folders.Count; i++)
            if (HasAnyBlueprints(folder.Folders[i])) return true;
        return false;
    }

    private static string FormatElectricity(int kw)
    {
        if (kw < 1000) return $"{kw} kW";
        double mw = kw / 1000.0;
        if (mw < 10.0)  return mw.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " MW";
        if (mw < 100.0) return mw.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MW";
        return $"{(int)Math.Round(mw)} MW";
    }
}
