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
using System.Globalization;
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

namespace CoIDesignerToolkit;

/// <summary>
/// Injects a "Copy as Markdown" button into <c>BlueprintDetail</c> and <c>BlueprintFolderDetail</c>.
/// For a single blueprint the button copies two separate tables: Operational stats and Construction cost,
/// plus a Components breakdown. For a folder it copies a single wide table with one row per blueprint.
/// </summary>
internal static class BlueprintExport
{
    private static readonly ModLogger s_log = new ModLogger("BDT.BpExport");

    private const string CLIPBOARD_ICON = "Assets/Unity/UserInterface/General/Clipboard.svg";

    private enum MarkdownRenderLanguage
    {
        English,
        Local,
        Hybrid,
    }

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
        /// <summary>Maintenance product proto → monthly value.</summary>
        public Dictionary<VirtualProductProto, Fix32> MaintenanceValues;
        /// <summary>Construction product proto → quantity.</summary>
        public Dictionary<ProductProto, int> ConstructionValues;
        /// <summary>Component entity proto → count.</summary>
        public Dictionary<Proto, int> ComponentCounts;
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

            var copyBtn = new ButtonIconText(Button.General, CLIPBOARD_ICON, BdtLocalization.CopyAsMarkdownButton.AsFormatted)
                .Tooltip(BdtLocalization.CopyBlueprintMarkdownTooltip.AsFormatted)
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

            var copyBtn = new ButtonIconText(Button.General, CLIPBOARD_ICON, BdtLocalization.CopyAsMarkdownButton.AsFormatted)
                .Tooltip(BdtLocalization.CopyFolderMarkdownTooltip.AsFormatted)
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
            MaintenanceValues   = new Dictionary<VirtualProductProto, Fix32>(),
            ConstructionValues  = new Dictionary<ProductProto, int>(),
            ComponentCounts     = new Dictionary<Proto, int>(),
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

        // Maintenance values stay keyed by proto until Markdown rendering.
        foreach (var kvp in maintenanceByProduct)
            if (kvp.Value > Fix32.Zero)
                stats.MaintenanceValues[kvp.Key] = kvp.Value;

        // Construction values stay keyed by proto until Markdown rendering.
        var products = constructionCost.Products;
        for (int i = 0; i < products.Length; i++)
        {
            var pq = products[i];
            stats.ConstructionValues[pq.Product] = pq.Quantity.Value;
        }

        // Component counts stay keyed by proto until Markdown rendering.
        foreach (KeyValuePair<Proto, int> kvp in blueprint.AllMajorProtos)
        {
            stats.ComponentCounts.TryGetValue(kvp.Key, out int existing2);
            stats.ComponentCounts[kvp.Key] = existing2 + kvp.Value;
        }

        return stats;
    }

    /// <summary>
    /// Single-blueprint Markdown: two separate tables (Operational / Construction) plus Components.
    /// </summary>
    internal static string BuildMarkdown(IBlueprint blueprint)
    {
        var sb = new StringBuilder();

        // Heading + description
        sb.AppendLine($"## {blueprint.Name}");
        if (!string.IsNullOrWhiteSpace(blueprint.Desc))
        {
            sb.AppendLine();
            sb.AppendLine(blueprint.Desc.Trim());
        }

        if (ShouldRenderBothLanguages())
        {
            BpStats stats = ComputeBpStats(blueprint);
            AppendBlueprintTables(sb, stats, MarkdownRenderLanguage.English);
            AppendBlueprintTables(sb, stats, MarkdownRenderLanguage.Local);
        }
        else
        {
            MarkdownRenderLanguage language = ResolveRenderLanguage();
            AppendBlueprintTables(sb, ComputeBpStats(blueprint), language);
        }

        return sb.ToString();
    }

    private static void AppendBlueprintTables(
        StringBuilder sb,
        BpStats s,
        MarkdownRenderLanguage language)
    {
        // ── Components ───────────────────────────────────────────────────────
        if (s.ComponentCounts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"### {MarkdownText(BdtLocalization.MarkdownComponentsHeading, language)}");
            sb.AppendLine($"| {MarkdownText(BdtLocalization.MarkdownEntityHeader, language)} | {MarkdownText(BdtLocalization.MarkdownCountHeader, language)} |");
            sb.AppendLine("|---|---|");
            foreach (var proto in SortedProtos(s.ComponentCounts.Keys, language))
                sb.AppendLine($"| {DisplayName(proto, language)} | {FormatInteger(s.ComponentCounts[proto], language)} |");
        }

        // ── Construction ─────────────────────────────────────────────────────
        if (s.ConstructionValues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"### {MarkdownText(BdtLocalization.MarkdownConstructionHeading, language)}");
            sb.AppendLine($"| {MarkdownText(BdtLocalization.MarkdownProductHeader, language)} | {MarkdownText(BdtLocalization.MarkdownQuantityHeader, language)} |");
            sb.AppendLine("|---|---|");
            foreach (var proto in SortedProtos(s.ConstructionValues.Keys, language))
                sb.AppendLine($"| {DisplayName(proto, language)} | {FormatSiQuantity(s.ConstructionValues[proto], language)} |");
        }

        // ── Operational ──────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine($"### {MarkdownText(BdtLocalization.MarkdownOperationalHeading, language)}");
        sb.AppendLine($"| {MarkdownText(BdtLocalization.MarkdownPropertyHeader, language)} | {MarkdownText(BdtLocalization.MarkdownValueHeader, language)} |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| {MarkdownText(BdtLocalization.MarkdownEntitiesStat, language)} | {FormatInteger(s.Entities, language)} |");
        if (s.Workers > 0) sb.AppendLine($"| {MarkdownText(Tr.Workers, language)} | {FormatInteger(s.Workers, language)} |");
        if (s.ElecKw  > 0) sb.AppendLine($"| {MarkdownText(Tr.ElectricityStats, language)} | {FormatElectricity(s.ElecKw, language)} |");
        if (s.CompTf  > 0) sb.AppendLine($"| {MarkdownText(Tr.ComputingStats, language)} | {FormatInteger(s.CompTf, language)} TF |");
        foreach (var proto in SortedProtos(s.MaintenanceValues.Keys, language))
            sb.AppendLine($"| {DisplayName(proto, language)} {MarkdownText(BdtLocalization.MarkdownPerMonthSuffix, language)} | {FormatAdaptive(s.MaintenanceValues[proto], language)} |");
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
        var sb = new StringBuilder();

        sb.AppendLine($"## {folder.Name}");
        if (!string.IsNullOrWhiteSpace(folder.Desc))
        {
            sb.AppendLine();
            sb.AppendLine(folder.Desc.Trim());
        }

        if (ShouldRenderBothLanguages())
        {
            AppendFolderTable(sb, folder, MarkdownRenderLanguage.English);
            AppendFolderTable(sb, folder, MarkdownRenderLanguage.Local);
        }
        else
        {
            AppendFolderTable(sb, folder, ResolveRenderLanguage());
        }

        return sb.ToString();
    }

    private static void AppendFolderTable(
        StringBuilder sb,
        IBlueprintsFolder folder,
        MarkdownRenderLanguage language)
    {
        var bpList = new List<BpStats>();
        CollectBps(folder, "", bpList);

        bpList.Sort((a, b) =>
        {
            int c = string.Compare(a.FolderPath, b.FolderPath, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        // ── Discover dynamic columns ───────────────────────────────────────
        bool hasWorkers = false, hasElec = false, hasComp = false;
        var maintCols  = new HashSet<VirtualProductProto>();
        var constrCols = new HashSet<ProductProto>();

        foreach (var s in bpList)
        {
            if (s.Workers > 0) hasWorkers = true;
            if (s.ElecKw  > 0) hasElec    = true;
            if (s.CompTf  > 0) hasComp    = true;
            foreach (var k in s.MaintenanceValues.Keys)  maintCols.Add(k);
            foreach (var k in s.ConstructionValues.Keys) constrCols.Add(k);
        }

        sb.AppendLine();

        // Header row
        sb.Append($"| {MarkdownText(BdtLocalization.MarkdownBlueprintHeader, language)} | {MarkdownText(BdtLocalization.MarkdownFolderHeader, language)} | {MarkdownText(BdtLocalization.MarkdownEntitiesStat, language)} |");
        if (hasWorkers) sb.Append($" {MarkdownText(Tr.Workers, language)} |");
        if (hasElec)    sb.Append($" {MarkdownText(Tr.ElectricityStats, language)} |");
        if (hasComp)    sb.Append($" {MarkdownText(Tr.ComputingStats, language)} |");
        List<VirtualProductProto> sortedMaintCols = SortedProtos(maintCols, language);
        List<ProductProto> sortedConstrCols = SortedProtos(constrCols, language);

        foreach (var col in sortedMaintCols)  sb.Append($" {DisplayName(col, language)} {MarkdownText(BdtLocalization.MarkdownPerMonthSuffix, language)} |");
        foreach (var col in sortedConstrCols) sb.Append($" {DisplayName(col, language)} |");
        sb.AppendLine();

        // Separator row
        sb.Append("|---|---|---|");
        if (hasWorkers) sb.Append("---|");
        if (hasElec)    sb.Append("---|");
        if (hasComp)    sb.Append("---|");
        foreach (var _ in sortedMaintCols)  sb.Append("---|");
        foreach (var _ in sortedConstrCols) sb.Append("---|");
        sb.AppendLine();

        // Data rows
        foreach (var s in bpList)
        {
            string folderCell = string.IsNullOrEmpty(s.FolderPath) ? "." : s.FolderPath;
            sb.Append($"| {s.Name} | {folderCell} | {FormatInteger(s.Entities, language)} |");
            if (hasWorkers) sb.Append(s.Workers > 0 ? $" {FormatInteger(s.Workers, language)} |" : " - |");
            if (hasElec)    sb.Append(s.ElecKw  > 0 ? $" {FormatElectricity(s.ElecKw, language)} |" : " - |");
            if (hasComp)    sb.Append(s.CompTf  > 0 ? $" {FormatInteger(s.CompTf, language)} TF |" : " - |");
            foreach (var col in sortedMaintCols)
                sb.Append(s.MaintenanceValues.TryGetValue(col, out var mv) ? $" {FormatAdaptive(mv, language)} |" : " - |");
            foreach (var col in sortedConstrCols)
                sb.Append(s.ConstructionValues.TryGetValue(col, out var cv) ? $" {FormatSiQuantity(cv, language)} |" : " - |");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Recursively collects all blueprints under <paramref name="folder"/> into <paramref name="result"/>,
    /// setting each entry's <see cref="BpStats.FolderPath"/> to the path relative to the root folder.
    /// </summary>
    private static void CollectBps(
        IBlueprintsFolder folder,
        string relativePath,
        List<BpStats> result)
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

    private static string FormatElectricity(int kw, MarkdownRenderLanguage language)
    {
        if (kw < 1000) return $"{FormatInteger(kw, language)} kW";
        double mw = kw / 1000.0;
        if (mw < 10.0)  return FormatDecimal(mw, 2, language) + " MW";
        if (mw < 100.0) return FormatDecimal(mw, 1, language) + " MW";
        return $"{FormatInteger((int)Math.Round(mw), language)} MW";
    }

    private static string FormatSiQuantity(int value, MarkdownRenderLanguage language)
    {
        long abs = Math.Abs((long)value);
        if (abs < 1000) return FormatInteger(value, language);
        if (abs < 1000000)
        {
            if (abs < 10000) return FormatDecimalOptional(value / 1000.0, 1, language) + "k";
            return FormatInteger((int)Math.Round(value / 1000.0), language) + "k";
        }
        if (abs < 1000000000)
        {
            if (abs < 10000000) return FormatDecimalOptional(value / 1000000.0, 1, language) + "M";
            return FormatInteger((int)Math.Round(value / 1000000.0), language) + "M";
        }
        if (abs < 10000000000L) return FormatDecimalOptional(value / 1000000000.0, 1, language) + "G";
        return FormatInteger((long)Math.Round(value / 1000000000.0), language) + "G";
    }

    private static string FormatAdaptive(Fix32 value, MarkdownRenderLanguage language)
    {
        double number = value.ToDouble();
        double abs = Math.Abs(number);
        int decimals;
        if (abs < 0.0995 && abs > 0.0) decimals = 3;
        else if (abs < 0.995 && abs > 0.0) decimals = 2;
        else if (abs < 9.95) decimals = 1;
        else decimals = 0;
        return decimals > 0
            ? FormatDecimal(number, decimals, language)
            : FormatInteger((int)Math.Round(number), language);
    }

    private static string FormatInteger(long value, MarkdownRenderLanguage language)
    {
        return value.ToString("#,0", NumberFormatFor(language));
    }

    private static string FormatDecimal(double value, int decimals, MarkdownRenderLanguage language)
    {
        string format = decimals <= 0 ? "#,0" : "#,0." + new string('0', decimals);
        return value.ToString(format, NumberFormatFor(language));
    }

    private static string FormatDecimalOptional(double value, int decimals, MarkdownRenderLanguage language)
    {
        string format = decimals <= 0 ? "#,0" : "#,0." + new string('#', decimals);
        return value.ToString(format, NumberFormatFor(language));
    }

    private static NumberFormatInfo NumberFormatFor(MarkdownRenderLanguage language)
    {
        switch (DesignerToolkitSettings.MarkdownNumberFormat)
        {
            case MarkdownNumberFormat.English:
                return CultureInfo.GetCultureInfo(LocalizationManager.EN_US_CULTURE_INFO_ID).NumberFormat;
            case MarkdownNumberFormat.Local:
                return LocalizationManager.CurrentCultureInfo.NumberFormat;
            case MarkdownNumberFormat.Auto:
            default:
                return language == MarkdownRenderLanguage.English
                    ? CultureInfo.GetCultureInfo(LocalizationManager.EN_US_CULTURE_INFO_ID).NumberFormat
                    : LocalizationManager.CurrentCultureInfo.NumberFormat;
        }
    }

    private static MarkdownRenderLanguage ResolveRenderLanguage()
    {
        switch (DesignerToolkitSettings.MarkdownTableLanguage)
        {
            case MarkdownTableLanguage.Local:
                return MarkdownRenderLanguage.Local;
            case MarkdownTableLanguage.Hybrid:
                return MarkdownRenderLanguage.Hybrid;
            case MarkdownTableLanguage.English:
            case MarkdownTableLanguage.Both:
            default:
                return MarkdownRenderLanguage.English;
        }
    }

    private static bool ShouldRenderBothLanguages()
    {
        return DesignerToolkitSettings.MarkdownTableLanguage == MarkdownTableLanguage.Both
            && !string.Equals(
                LocalizationManager.CurrentLangInfo.CultureInfoId,
                LocalizationManager.EN_US_CULTURE_INFO_ID,
                StringComparison.Ordinal);
    }

    private static List<TProto> SortedProtos<TProto>(
        IEnumerable<TProto> protos,
        MarkdownRenderLanguage language)
        where TProto : Proto
    {
        var sorted = new List<TProto>(protos);
        sorted.Sort((a, b) =>
        {
            int c = string.Compare(DisplayName(a, language), DisplayName(b, language), StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Id.ToString(), b.Id.ToString(), StringComparison.Ordinal);
        });
        return sorted;
    }

    private static string DisplayName(
        Proto proto,
        MarkdownRenderLanguage language)
    {
        return LocalizedText(proto.Strings.Name, language);
    }

    private static string MarkdownText(
        LocStr text,
        MarkdownRenderLanguage language)
    {
        return LocalizedText(text, language);
    }

    private static string LocalizedText(
        LocStr text,
        MarkdownRenderLanguage language)
    {
        string english = LocalizationManager.GetUsEnStringFor(text);
        switch (language)
        {
            case MarkdownRenderLanguage.Local:
                return text.TranslatedString;
            case MarkdownRenderLanguage.Hybrid:
                string local = text.TranslatedString;
                return string.Equals(local, english, StringComparison.Ordinal)
                    ? local
                    : $"{local} ({english})";
            case MarkdownRenderLanguage.English:
            default:
                return english;
        }
    }
}
