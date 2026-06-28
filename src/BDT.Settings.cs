// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Localization;
using Mafi.Serialization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using CoI.AutoHelpers.Settings;
using UnityEngine;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit;

namespace CoIDesignerToolkit;

internal enum MarkdownTableLanguage
{
    English = 0,
    Local = 1,
    Both = 2,
    Hybrid = 3,
}

internal enum MarkdownNumberFormat
{
    Auto = 0,
    English = 1,
    Local = 2,
}

internal enum ThroughputHeatmapMode
{
    None = 0,
    Relative = 1,
    Capacity = 2,
}

internal static class DesignerToolkitSettings
{
    internal const string SettingsStateConfigKey = "dtkSettingsStateJson";

    private const string MARKDOWN_TABLE_LANGUAGE_KEY = "markdown_table_language";
    private const string MARKDOWN_NUMBER_FORMAT_KEY = "markdown_number_format";
    private const string INSTANT_BUILD_MODE_KEY = "instant_build_mode";
    private const string LEGACY_BELT_CONFIGURATIONS_KEY = "legacy_belt_configurations";
    private const string TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY = "transport_cleanup_hotkey_primary";

    private static Mafi.Core.Game.GameDifficultyConfig? s_difficultyConfig;

    public static void SetDifficultyConfig(Mafi.Core.Game.GameDifficultyConfig config)
    {
        s_difficultyConfig = config;
    }

    public static bool IsSandbox => s_difficultyConfig != null && s_difficultyConfig.IsSandbox;
    private const string TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY = "transport_cleanup_hotkey_secondary";
    private const string HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY = "height_filter_show_layer_hotkey_primary";
    private const string HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY = "height_filter_show_layer_hotkey_secondary";
    private const string HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY = "height_filter_hide_layer_hotkey_primary";
    private const string HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY = "height_filter_hide_layer_hotkey_secondary";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_KEY = "transport_cleanup_hotkey_key";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_CTRL_KEY = "transport_cleanup_hotkey_ctrl";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_ALT_KEY = "transport_cleanup_hotkey_alt";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_SHIFT_KEY = "transport_cleanup_hotkey_shift";
    
    private const string THROUGHPUT_OVERLAY_ENABLED_KEY = "throughput_overlay_enabled";
    private const string THROUGHPUT_GLOW_ENABLED_KEY = "throughput_glow_enabled";
    private const string THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY = "throughput_overlay_toggle_hotkey_primary";
    private const string THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY = "throughput_overlay_toggle_hotkey_secondary";
    private const string THROUGHPUT_HEATMAP_MODE_KEY = "throughput_heatmap_mode";
    private const string THROUGHPUT_COLORBLIND_MODE_KEY = "throughput_colorblind_mode";
    private const string THROUGHPUT_SHOW_AS_PERCENT_KEY = "throughput_show_as_percent";
    private const string THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY = "throughput_aoe_tool_hotkey_primary";
    private const string THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY = "throughput_aoe_tool_hotkey_secondary";
    private const string POLLUTION_OVERLAY_ENABLED_KEY = "pollution_overlay_enabled";
    private const string POLLUTION_GLOW_ENABLED_KEY = "pollution_glow_enabled";
    private const string POLLUTION_DAYS_TO_AVERAGE_KEY = "pollution_days_to_average";
    private const string POLLUTION_SHOW_AIR_KEY = "pollution_show_air";
    private const string POLLUTION_SHOW_GROUND_KEY = "pollution_show_ground";
    private const string POLLUTION_SHOW_VEHICLE_KEY = "pollution_show_vehicle";
    private const string POLLUTION_SHOW_SHIP_KEY = "pollution_show_ship";
    private const string POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY = "pollution_overlay_toggle_hotkey_primary";
    private const string POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY = "pollution_overlay_toggle_hotkey_secondary";
    private const string LAYOUT_BOX_MODE_ENABLED_KEY = "layout_box_mode_enabled";
    private const string USE_RECYCLE_BIN_KEY = "use_recycle_bin";
    private const string RECYCLE_BIN_FOLDER_NAME_KEY = "recycle_bin_folder_name";
    private const string BLUEPRINT_SPACING_KEY = "blueprint_spacing";
//     private const string LAYOUT_BOX_MODE_TOGGLE_HOTKEY_PRIMARY_KEY = "layout_box_mode_toggle_hotkey_primary";
//     private const string LAYOUT_BOX_MODE_TOGGLE_HOTKEY_SECONDARY_KEY = "layout_box_mode_toggle_hotkey_secondary";
    private const string UNDO_HOTKEY_PRIMARY_KEY = "undo_hotkey_primary";
    private const string UNDO_HOTKEY_SECONDARY_KEY = "undo_hotkey_secondary";

    private const string LEGACY_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_KEY = "";
    private const string LEGACY_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_CTRL_KEY = "";
    private const string LEGACY_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_ALT_KEY = "";
    private const string LEGACY_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SHIFT_KEY = "";

    private const int SETTINGS_SCHEMA_VERSION = 1;
    private const string SETTINGS_TAB_ICON_ASSET =
        "Assets/Unity/UserInterface/Toolbar/Blueprints.svg";
    private static readonly Percent SETTINGS_LABEL_WIDTH = 34.Percent();
    private static readonly Percent SETTINGS_COLUMN_WIDTH = 96.Percent();
    private static readonly Px SETTINGS_SECTION_INDENT = 4.pt();
    private static readonly Px SETTINGS_OPTIONS_GAP = 2.pt();
    private static readonly BdtHotkey DEFAULT_TRANSPORT_CLEANUP_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);
    private static readonly BdtHotkey DEFAULT_HEIGHT_FILTER_SHOW_LAYER_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.PageUp);
    private static readonly BdtHotkey DEFAULT_HEIGHT_FILTER_HIDE_LAYER_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.PageDown);
    private static readonly BdtHotkey DEFAULT_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.T);
    private static readonly BdtHotkey DEFAULT_THROUGHPUT_AOE_TOOL_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.T);
    private static readonly BdtHotkey DEFAULT_POLLUTION_OVERLAY_TOGGLE_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.P);
//     private static readonly BdtHotkey DEFAULT_LAYOUT_BOX_MODE_TOGGLE_HOTKEY =
//         BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.B);
    private static readonly BdtHotkey DEFAULT_UNDO_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftControl, KeyCode.Z);

    private static readonly ModLogger s_log = new ModLogger("BDT.Settings");

    private static ModJsonConfig? s_config;
    private static IModStateJsonStore? s_store;
    private static string? s_modDirectory;

    public static MarkdownTableLanguage MarkdownTableLanguage { get; private set; } =
        MarkdownTableLanguage.English;
    public static MarkdownNumberFormat MarkdownNumberFormat { get; private set; } =
        MarkdownNumberFormat.Auto;
    public static bool InstantBuildModeEnabled { get; private set; }
    public static bool LegacyBeltConfigurationsEnabled { get; private set; } = true;
    public static int HeightFilterMaxVisibleLevel { get; private set; } = 6;
    public static bool ThroughputOverlayEnabled { get; private set; } = true;
    public static bool ThroughputGlowEnabled { get; private set; } = true;
    public static ThroughputHeatmapMode ThroughputHeatmapMode { get; private set; } = ThroughputHeatmapMode.Capacity;
    public static bool ThroughputColorblindMode { get; private set; } = false;
    public static bool ThroughputShowAsPercent { get; private set; } = false;
    public static bool PollutionOverlayEnabled { get; private set; } = false;
    public static bool PollutionGlowEnabled { get; private set; } = false;
    public static int PollutionDaysToAverage { get; private set; } = 360;
    public static bool PollutionShowAir { get; private set; } = true;
    public static bool PollutionShowGround { get; private set; } = true;
    public static bool PollutionShowVehicle { get; private set; } = true;
    public static bool PollutionShowShip { get; private set; } = true;
    public static bool LayoutBoxModeEnabled { get; private set; } = false;
    public static bool UseRecycleBin { get; private set; } = true;
    public static string RecycleBinFolderName { get; private set; } = "Recycle Bin";
    public static int BlueprintSpacing { get; private set; } = 6;

    private static Func<BlueprintsLibrary>? s_blueprintsLibraryProvider;

    public static void SetBlueprintsLibraryProvider(Func<BlueprintsLibrary> provider)
    {
        s_blueprintsLibraryProvider = provider;
    }

    public static string GetFormattedRecycleBinName()
    {
        string name = RecycleBinFolderName;
        if (UseRecycleBin)
        {
            return $"<color=grey>{name}</color>";
        }
        return name;
    }

    private static void SetBlueprintSpacing(int spacing)
    {
        BlueprintSpacing = Math.Max(0, Math.Min(12, spacing));
    }

    private static void SetUseRecycleBin(bool enabled)
    {
        if (UseRecycleBin == enabled)
            return;

        UseRecycleBin = enabled;

        if (s_blueprintsLibraryProvider != null)
        {
            try
            {
                var library = s_blueprintsLibraryProvider();
                if (library != null)
                {
                    UpdateRecycleBinFolderFormatting(library);
                }
            }
            catch (Exception ex)
            {
                s_log.Exception(ex, "Failed to update recycle bin folder formatting on toggle");
            }
        }
    }

    private static string StripRichText(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static void UpdateRecycleBinFolderFormatting(BlueprintsLibrary library)
    {
        IBlueprintsFolder root = library.Root;
        if (root == null) return;

        string configName = RecycleBinFolderName;
        string coloredName = $"<color=grey>{configName}</color>";
        string targetName = UseRecycleBin ? coloredName : configName;

        IBlueprintsFolder? targetFolder = null;
        for (int i = 0; i < root.Folders.Count; i++)
        {
            var folder = root.Folders[i];
            string strippedName = StripRichText(folder.Name);
            if (string.Equals(strippedName, configName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strippedName, "Recycle Bin", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = folder;
                break;
            }
        }

        if (targetFolder != null && targetFolder.Name != targetName)
        {
            library.RenameItem(targetFolder, targetName);
            s_log.Info($"Updated recycle bin folder formatting to '{targetName}'");
        }
    }

    private static void SetRecycleBinFolderName(string newName)
    {
        if (RecycleBinFolderName == newName)
            return;

        string oldName = RecycleBinFolderName;
        RecycleBinFolderName = newName;

        if (s_blueprintsLibraryProvider != null)
        {
            try
            {
                var library = s_blueprintsLibraryProvider();
                if (library != null)
                {
                    RenameRecycleBinFolder(library, oldName, newName);
                }
            }
            catch (Exception ex)
            {
                s_log.Exception(ex, "Failed to rename recycle bin folder on settings change");
            }
        }
    }

    private static void RenameRecycleBinFolder(BlueprintsLibrary library, string oldName, string newName)
    {
        IBlueprintsFolder root = library.Root;
        if (root == null) return;

        string targetNewName = UseRecycleBin ? $"<color=grey>{newName}</color>" : newName;

        IBlueprintsFolder? oldFolder = null;
        IBlueprintsFolder? newFolder = null;

        for (int i = 0; i < root.Folders.Count; i++)
        {
            var folder = root.Folders[i];
            string strippedName = StripRichText(folder.Name);
            if (string.Equals(strippedName, oldName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strippedName, "Recycle Bin", StringComparison.OrdinalIgnoreCase))
            {
                oldFolder = folder;
            }
            else if (string.Equals(strippedName, newName, StringComparison.OrdinalIgnoreCase))
            {
                newFolder = folder;
            }
        }

        if (oldFolder != null && newFolder == null)
        {
            library.RenameItem(oldFolder, targetNewName);
            s_log.Info($"Renamed recycle bin folder from '{oldFolder.Name}' to '{targetNewName}'");
        }
    }

    public static BdtHotkey TransportCleanupHotkey { get; private set; } = DEFAULT_TRANSPORT_CLEANUP_HOTKEY;
    public static BdtHotkey HeightFilterShowLayerHotkey { get; private set; } = DEFAULT_HEIGHT_FILTER_SHOW_LAYER_HOTKEY;
    public static BdtHotkey HeightFilterHideLayerHotkey { get; private set; } = DEFAULT_HEIGHT_FILTER_HIDE_LAYER_HOTKEY;
    public static BdtHotkey ThroughputOverlayToggleHotkey { get; private set; } = DEFAULT_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY;
    public static BdtHotkey ThroughputAoEToolHotkey { get; private set; } = DEFAULT_THROUGHPUT_AOE_TOOL_HOTKEY;
    public static BdtHotkey PollutionOverlayToggleHotkey { get; private set; } = DEFAULT_POLLUTION_OVERLAY_TOGGLE_HOTKEY;
//     public static BdtHotkey LayoutBoxModeToggleHotkey { get; private set; } = DEFAULT_LAYOUT_BOX_MODE_TOGGLE_HOTKEY;
    public static BdtHotkey UndoHotkey { get; private set; } = DEFAULT_UNDO_HOTKEY;

    public static event Action<bool>? InstantBuildModeChanged;
    public static event Action<int>? HeightFilterMaxVisibleLevelChanged;
    public static event Action<bool>? ThroughputOverlayEnabledChanged;
    public static event Action<bool>? PollutionOverlayEnabledChanged;
    public static event Action<int>? PollutionDaysToAverageChanged;

    private static void SetThroughputHeatmapMode(ThroughputHeatmapMode mode)
    {
        ThroughputHeatmapMode = mode;
    }

    private static void SetThroughputColorblindMode(bool enabled)
    {
        ThroughputColorblindMode = enabled;
    }

    private static void SetThroughputShowAsPercent(bool enabled)
    {
        ThroughputShowAsPercent = enabled;
    }

    public static void SetPollutionOverlayEnabled(bool enabled)
    {
        if (PollutionOverlayEnabled == enabled)
            return;
        PollutionOverlayEnabled = enabled;
        try { PollutionOverlayEnabledChanged?.Invoke(enabled); }
        catch (Exception ex) { s_log.Warning($"Error raising PollutionOverlayEnabledChanged: {ex.Message}"); }
    }

    public static void SetPollutionGlowEnabled(bool enabled)
    {
        PollutionGlowEnabled = enabled;
    }

    public static void SetPollutionDaysToAverage(int days)
    {
        days = Math.Max(0, Math.Min(360, days));
        if (PollutionDaysToAverage == days)
            return;
        PollutionDaysToAverage = days;
        try { PollutionDaysToAverageChanged?.Invoke(days); }
        catch (Exception ex) { s_log.Warning($"Error raising PollutionDaysToAverageChanged: {ex.Message}"); }
    }

    public static void SetPollutionShowAir(bool enabled) { PollutionShowAir = enabled; }
    public static void SetPollutionShowGround(bool enabled) { PollutionShowGround = enabled; }
    public static void SetPollutionShowVehicle(bool enabled) { PollutionShowVehicle = enabled; }
    public static void SetPollutionShowShip(bool enabled) { PollutionShowShip = enabled; }

    public static void SetLayoutBoxModeEnabled(bool enabled)
    {
        LayoutBoxModeEnabled = enabled;
    }


    public static void Initialize(ModJsonConfig config, IModStateJsonStore store, string modDirectory)
    {
        s_config = config;
        s_store = store;
        s_modDirectory = modDirectory;
        MarkdownTableLanguage initialLanguage = FromInt(config.GetInt(MARKDOWN_TABLE_LANGUAGE_KEY, 0));
        MarkdownNumberFormat initialNumberFormat = NumberFormatFromInt(config.GetInt(MARKDOWN_NUMBER_FORMAT_KEY, 0));
        bool initialInstantBuildMode = config.GetBool(INSTANT_BUILD_MODE_KEY, false);
        bool initialLegacyBeltConfigurations = config.GetBool(LEGACY_BELT_CONFIGURATIONS_KEY, true);
        bool initialThroughputOverlayEnabled = config.GetBool(THROUGHPUT_OVERLAY_ENABLED_KEY, true);
        bool initialThroughputGlowEnabled = config.GetBool(THROUGHPUT_GLOW_ENABLED_KEY, true);
        ThroughputHeatmapMode initialThroughputHeatmapMode = HeatmapModeFromInt(config.GetInt(THROUGHPUT_HEATMAP_MODE_KEY, (int)ThroughputHeatmapMode.Capacity));
        bool initialThroughputColorblindMode = config.GetBool(THROUGHPUT_COLORBLIND_MODE_KEY, false);
        bool initialThroughputShowAsPercent = config.GetBool(THROUGHPUT_SHOW_AS_PERCENT_KEY, false);
        bool initialPollutionOverlayEnabled = config.GetBool(POLLUTION_OVERLAY_ENABLED_KEY, false);
        bool initialPollutionGlowEnabled = config.GetBool(POLLUTION_GLOW_ENABLED_KEY, false);
        int initialPollutionDaysToAverage = config.GetInt(POLLUTION_DAYS_TO_AVERAGE_KEY, 360);
        bool initialPollutionShowAir = config.GetBool(POLLUTION_SHOW_AIR_KEY, true);
        bool initialPollutionShowGround = config.GetBool(POLLUTION_SHOW_GROUND_KEY, true);
        bool initialPollutionShowVehicle = config.GetBool(POLLUTION_SHOW_VEHICLE_KEY, true);
        bool initialPollutionShowShip = config.GetBool(POLLUTION_SHOW_SHIP_KEY, true);
        bool initialLayoutBoxModeEnabled = config.GetBool(LAYOUT_BOX_MODE_ENABLED_KEY, false);

        BdtHotkey initialTransportCleanupHotkey = HotkeyFromConfig(
            config,
            TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY,
            TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_CTRL_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_ALT_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_SHIFT_KEY,
            DEFAULT_TRANSPORT_CLEANUP_HOTKEY);
        BdtHotkey initialShowLayerHotkey = HotkeyFromConfig(
            config,
            HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY,
            HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_HEIGHT_FILTER_SHOW_LAYER_HOTKEY);
        BdtHotkey initialHideLayerHotkey = HotkeyFromConfig(
            config,
            HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY,
            HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_HEIGHT_FILTER_HIDE_LAYER_HOTKEY);
        BdtHotkey initialThroughputOverlayToggleHotkey = HotkeyFromConfig(
            config,
            THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY,
            THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY);
        BdtHotkey initialThroughputAoEToolHotkey = HotkeyFromConfig(
            config,
            THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY,
            THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_THROUGHPUT_AOE_TOOL_HOTKEY);
        BdtHotkey initialPollutionOverlayToggleHotkey = HotkeyFromConfig(
            config,
            POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY,
            POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_POLLUTION_OVERLAY_TOGGLE_HOTKEY);
        BdtHotkey initialUndoHotkey = HotkeyFromConfig(
            config,
            UNDO_HOTKEY_PRIMARY_KEY,
            UNDO_HOTKEY_SECONDARY_KEY,
            "", "", "", "",
            DEFAULT_UNDO_HOTKEY);

        bool initialUseRecycleBin = config.GetBool(USE_RECYCLE_BIN_KEY, true);
        string initialRecycleBinFolderName = config.GetString(RECYCLE_BIN_FOLDER_NAME_KEY, "Recycle Bin");
        int initialBlueprintSpacing = config.GetInt(BLUEPRINT_SPACING_KEY, 6);

        TransportCleanupHotkey = initialTransportCleanupHotkey;
        HeightFilterShowLayerHotkey = initialShowLayerHotkey;
        HeightFilterHideLayerHotkey = initialHideLayerHotkey;
        ThroughputOverlayToggleHotkey = initialThroughputOverlayToggleHotkey;
        ThroughputAoEToolHotkey = initialThroughputAoEToolHotkey;
        PollutionOverlayToggleHotkey = initialPollutionOverlayToggleHotkey;
//         LayoutBoxModeToggleHotkey = initialLayoutBoxModeToggleHotkey;
        UndoHotkey = initialUndoHotkey;

        LoadFromJsonStore(
            store,
            initialLanguage,
            initialNumberFormat,
            initialInstantBuildMode,
            initialLegacyBeltConfigurations,
            initialThroughputOverlayEnabled,
            initialThroughputGlowEnabled,
            initialThroughputHeatmapMode,
            initialThroughputColorblindMode,
            initialThroughputShowAsPercent,
            initialPollutionOverlayEnabled,
            initialPollutionGlowEnabled,
            initialPollutionDaysToAverage,
            initialPollutionShowAir,
            initialPollutionShowGround,
            initialPollutionShowVehicle,
            initialPollutionShowShip,
            initialLayoutBoxModeEnabled,
            initialUseRecycleBin,
            initialRecycleBinFolderName,
            initialBlueprintSpacing);
    }

    public static void SaveToJsonStore(IModStateJsonStore store)
    {
        ModStateJsonSaveResult result = store.SaveJson(BuildStateJson());
        if (!result.Succeeded)
            s_log.Warning($"Failed to save BDT settings state to {result.StorageKind} value '{result.StateKey}': {result.ErrorMessage}");
    }

    public static ModSettingsTab BuildSettingsTab(DependencyResolver resolver)
    {
        return new ModSettingsTab(
            "designer-toolkit",
            BdtLocalization.ModName.AsFormatted,
            BdtLocalization.SettingsTabMarkdown.AsFormatted,
            100,
            () => BuildMarkdownSettingsContent(resolver),
            SETTINGS_TAB_ICON_ASSET);
    }

    private static UiComponent BuildMarkdownSettingsContent(DependencyResolver resolver)
    {
        var root = new Column(SETTINGS_OPTIONS_GAP)
            .AlignItemsStretch()
            .PaddingLeft(SETTINGS_SECTION_INDENT)
            .Width(SETTINGS_COLUMN_WIDTH);

        root.Add(new Title(BdtLocalization.SettingsMarkdownCopyHeading.AsFormatted)
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Dropdown<MarkdownTableLanguage> languageDropdown =
            new Dropdown<MarkdownTableLanguage>(LanguageDropdownOption)
                .Label(BdtLocalization.SettingsMarkdownTableLanguage.AsFormatted)
                .Tooltip(new LocStrFormatted(
                    BdtLocalization.SettingsMarkdownTableLanguageDescription.TranslatedString
                    + "\n\n"
                    + BdtLocalization.SettingsMarkdownTableLanguagePending.TranslatedString))
                .LabelWidth(SETTINGS_LABEL_WIDTH)
                .SetOptions(
                    MarkdownTableLanguage.English,
                    MarkdownTableLanguage.Local,
                    MarkdownTableLanguage.Both,
                    MarkdownTableLanguage.Hybrid)
                .SetValue(MarkdownTableLanguage)
                .OnValueChanged((language, _) => SetMarkdownTableLanguage(language));

        root.Add(languageDropdown);

        Dropdown<MarkdownNumberFormat> numberFormatDropdown =
            new Dropdown<MarkdownNumberFormat>(NumberFormatDropdownOption)
                .Label(BdtLocalization.SettingsMarkdownNumberFormat.AsFormatted)
                .Tooltip(BdtLocalization.SettingsMarkdownNumberFormatDescription.AsFormatted)
                .LabelWidth(SETTINGS_LABEL_WIDTH)
                .SetOptions(
                    MarkdownNumberFormat.Auto,
                    MarkdownNumberFormat.English,
                    MarkdownNumberFormat.Local)
                .SetValue(MarkdownNumberFormat)
                .OnValueChanged((numberFormat, _) => SetMarkdownNumberFormat(numberFormat));

        root.Add(numberFormatDropdown);
        root.Add(new Title(BdtLocalization.SettingsInstantBuildHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        bool isSandbox = resolver.Resolve<GameDifficultyConfig>().IsSandbox;

        Toggle instantBuildToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsInstantBuildMode.AsFormatted)
            .Tooltip(BdtLocalization.SettingsInstantBuildModeDescription.AsFormatted)
            .Value(InstantBuildModeEnabled)
            .OnValueChanged(SetInstantBuildMode);

        if (!isSandbox)
        {
            instantBuildToggle.Enabled(false);
            instantBuildToggle.Tooltip(BdtLocalization.SettingsInstantBuildModeSandboxOnly.AsFormatted);
        }
        else
        {
            instantBuildToggle.Tooltip(BdtLocalization.SettingsInstantBuildModeDescription.AsFormatted);
        }

        root.Add(instantBuildToggle);

        root.Add(new Title(BdtLocalization.SettingsHeightFilterHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Dropdown<int> heightFilterDropdown = new Dropdown<int>(HeightFilterDropdownOption)
            .Label(BdtLocalization.SettingsHeightFilterMaxVisible.AsFormatted)
            .Tooltip(BdtLocalization.SettingsHeightFilterMaxVisibleDescription.AsFormatted)
            .LabelWidth(SETTINGS_LABEL_WIDTH)
            .SetOptions(0, 1, 2, 3, 4, 5, 6)
            .SetValue(HeightFilterMaxVisibleLevel)
            .OnValueChanged((level, _) => SetHeightFilterMaxVisibleLevel(level));
        root.Add(heightFilterDropdown);

        BdtKeyBindingField showLayerPrimaryField;
        BdtKeyBindingField showLayerSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsHeightFilterShowHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => HeightFilterShowLayerHotkey,
            hotkey =>
            {
                HeightFilterShowLayerHotkey = hotkey;
                SaveGlobalHotkey(HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out showLayerPrimaryField,
            out showLayerSecondaryField));

        BdtKeyBindingField hideLayerPrimaryField;
        BdtKeyBindingField hideLayerSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsHeightFilterHideHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => HeightFilterHideLayerHotkey,
            hotkey =>
            {
                HeightFilterHideLayerHotkey = hotkey;
                SaveGlobalHotkey(HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out hideLayerPrimaryField,
            out hideLayerSecondaryField));

        root.Add(new Title(BdtLocalization.SettingsThroughputHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle throughputOverlayToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputToggle.AsFormatted)
            .Tooltip(BdtLocalization.SettingsThroughputToggleDescription.AsFormatted)
            .Value(ThroughputOverlayEnabled)
            .OnValueChanged(SetThroughputOverlayEnabled);
        root.Add(throughputOverlayToggle);

        Toggle throughputGlowToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputGlow.AsFormatted)
            .Tooltip(BdtLocalization.SettingsThroughputGlowDescription.AsFormatted)
            .Value(ThroughputGlowEnabled)
            .OnValueChanged(SetThroughputGlowEnabled);
        root.Add(throughputGlowToggle);

        Toggle colorblindToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputColorblind.AsFormatted)
            .Tooltip(BdtLocalization.SettingsThroughputColorblindDescription.AsFormatted)
            .Value(ThroughputColorblindMode)
            .OnValueChanged(SetThroughputColorblindMode);
        colorblindToggle.Enabled(ThroughputHeatmapMode != ThroughputHeatmapMode.None);

        Dropdown<ThroughputHeatmapMode> heatmapDropdown =
            new Dropdown<ThroughputHeatmapMode>(HeatmapDropdownOption)
                .Label(BdtLocalization.SettingsThroughputHeatmap.AsFormatted)
                .LabelWidth(SETTINGS_LABEL_WIDTH)
                .SetOptions(
                    ThroughputHeatmapMode.None,
                    ThroughputHeatmapMode.Relative,
                    ThroughputHeatmapMode.Capacity)
                .SetValue(ThroughputHeatmapMode)
                .OnValueChanged((mode, _) => {
                    SetThroughputHeatmapMode(mode);
                    colorblindToggle.Enabled(mode != ThroughputHeatmapMode.None);
                });
        root.Add(heatmapDropdown);
        root.Add(colorblindToggle);

        Toggle showAsPercentToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputShowAsPercent.AsFormatted)
            .Tooltip(BdtLocalization.SettingsThroughputShowAsPercentDescription.AsFormatted)
            .Value(ThroughputShowAsPercent)
            .OnValueChanged(SetThroughputShowAsPercent);
        root.Add(showAsPercentToggle);

        BdtKeyBindingField throughputTogglePrimaryField;
        BdtKeyBindingField throughputToggleSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsThroughputToggleHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => ThroughputOverlayToggleHotkey,
            hotkey =>
            {
                ThroughputOverlayToggleHotkey = hotkey;
                SaveGlobalHotkey(THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out throughputTogglePrimaryField,
            out throughputToggleSecondaryField));

        BdtKeyBindingField throughputAoEToolPrimaryField;
        BdtKeyBindingField throughputAoEToolSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsThroughputAoEToolHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => ThroughputAoEToolHotkey,
            hotkey =>
            {
                ThroughputAoEToolHotkey = hotkey;
                SaveGlobalHotkey(THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY, THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out throughputAoEToolPrimaryField,
            out throughputAoEToolSecondaryField));

        root.Add(new Title(BdtLocalization.SettingsPollutionHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle pollutionOverlayToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionToggle.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionToggleDescription.AsFormatted)
            .Value(PollutionOverlayEnabled)
            .OnValueChanged(SetPollutionOverlayEnabled);
        root.Add(pollutionOverlayToggle);

        Toggle pollutionGlowToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionGlow.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionGlowDescription.AsFormatted)
            .Value(PollutionGlowEnabled)
            .OnValueChanged(SetPollutionGlowEnabled);
        root.Add(pollutionGlowToggle);

        // --- DAYS TO AVERAGE ---
        var daysRow = new Row(2.pt()).AlignItemsCenter();
        var daysLabel = new Label(BdtLocalization.SettingsPollutionDaysToAverage.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionDaysToAverageDescription.AsFormatted)
            .Width(SETTINGS_LABEL_WIDTH);
        daysRow.Add(daysLabel);

        var daysSpacer = new UiComponent().FlexGrow(1f);
        daysRow.Add(daysSpacer);

        var daysControlRow = new Row(2.pt()).AlignItemsCenter();

        var daysMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var daysPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        TextField daysInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(45.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(daysInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        daysInput.Text(PollutionDaysToAverage.ToString());

        daysControlRow.Add(daysMinusBtn);
        daysControlRow.Add(daysInput);
        daysControlRow.Add(daysPlusBtn);
        daysRow.Add(daysControlRow);
        root.Add(daysRow);

        Action<int> updateDays = (val) =>
        {
            SetPollutionDaysToAverage(val);
            daysInput.Text(PollutionDaysToAverage.ToString());
        };

        daysInput.OnValueChanged((text) =>
        {
            if (int.TryParse(text, out int val))
            {
                updateDays(val);
            }
        });

        Action<int> adjustDays = (sign) =>
        {
            if (int.TryParse(daysInput.GetText(), out int current))
            {
                int step = 1;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 10;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 5;

                int next = Math.Max(0, Math.Min(360, current + sign * step));
                updateDays(next);
            }
        };

        daysMinusBtn.OnClick(() => adjustDays(-1), allowKeyPresses: true);
        daysPlusBtn.OnClick(() => adjustDays(1), allowKeyPresses: true);

        // --- SUB-TOGGLES ---
        Toggle showAirToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionShowAir.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionShowAirDescription.AsFormatted)
            .Value(PollutionShowAir)
            .OnValueChanged(SetPollutionShowAir);
        root.Add(showAirToggle);

        Toggle showGroundToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionShowGround.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionShowGroundDescription.AsFormatted)
            .Value(PollutionShowGround)
            .OnValueChanged(SetPollutionShowGround);
        root.Add(showGroundToggle);

        Toggle showVehicleToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionShowVehicle.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionShowVehicleDescription.AsFormatted)
            .Value(PollutionShowVehicle)
            .OnValueChanged(SetPollutionShowVehicle);
        root.Add(showVehicleToggle);

        Toggle showShipToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionShowShip.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionShowShipDescription.AsFormatted)
            .Value(PollutionShowShip)
            .OnValueChanged(SetPollutionShowShip);
        root.Add(showShipToggle);

        // --- HOTKEY ---
        BdtKeyBindingField pollutionTogglePrimaryField;
        BdtKeyBindingField pollutionToggleSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsPollutionToggleHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => PollutionOverlayToggleHotkey,
            hotkey =>
            {
                PollutionOverlayToggleHotkey = hotkey;
                SaveGlobalHotkey(POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out pollutionTogglePrimaryField,
            out pollutionToggleSecondaryField));

        root.Add(new Title(BdtLocalization.SettingsLayoutBoxModeHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle layoutBoxModeToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsLayoutBoxModeToggle.AsFormatted)
            .Tooltip(new LocStrFormatted(BdtLocalization.SettingsLayoutBoxModeDescription.TranslatedString + "\n\nThis keybind can be configured in the vanilla Settings | Controls menu."))
            .Value(LayoutBoxModeEnabled)
            .OnValueChanged(SetLayoutBoxModeEnabled);

        var layoutBoxRow = new Row().AlignItemsCenter();
        layoutBoxRow.Add(layoutBoxModeToggle);

        if (!HotkeysRegistry.LayoutBoxModeToggle.Primary.IsEmpty)
        {
            var primaryText = new LocStrFormatted($"<mark=#36383EAA><color=#E2E2E2><size=90%><b> {HotkeysRegistry.LayoutBoxModeToggle.Primary.ToString()} </b></size></color></mark>");
            var badgeLabel = new Label(primaryText).MarginLeft(6.pt());
            layoutBoxRow.Add(badgeLabel);
        }
        if (!HotkeysRegistry.LayoutBoxModeToggle.Secondary.IsEmpty)
        {
            var secondaryText = new LocStrFormatted($"<mark=#36383EAA><color=#E2E2E2><size=90%><b> {HotkeysRegistry.LayoutBoxModeToggle.Secondary.ToString()} </b></size></color></mark>");
            var badgeLabel = new Label(secondaryText).MarginLeft(4.pt());
            layoutBoxRow.Add(badgeLabel);
        }

        root.Add(layoutBoxRow);





//         BdtKeyBindingField layoutBoxModePrimaryField;
//         BdtKeyBindingField layoutBoxModeSecondaryField;
//         root.Add(BuildHotkeyRow(
//             BdtLocalization.SettingsLayoutBoxModeHotkey.AsFormatted,
//             BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
//             () => LayoutBoxModeToggleHotkey,
//             hotkey =>
//             {
//                 LayoutBoxModeToggleHotkey = hotkey;
//                 SaveGlobalHotkey(LAYOUT_BOX_MODE_TOGGLE_HOTKEY_PRIMARY_KEY, LAYOUT_BOX_MODE_TOGGLE_HOTKEY_SECONDARY_KEY, hotkey);
//             },
//             out layoutBoxModePrimaryField,
//             out layoutBoxModeSecondaryField));

        root.Add(new Title(BdtLocalization.SettingsRecycleBinHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle recycleBinToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsUseRecycleBin.AsFormatted)
            .Tooltip(BdtLocalization.SettingsUseRecycleBinDescription.AsFormatted)
            .Value(UseRecycleBin)
            .OnValueChanged(SetUseRecycleBin);
        root.Add(recycleBinToggle);

        TextField? recycleBinFolderNameField = null;
        recycleBinFolderNameField = new TextField()
            .Label(BdtLocalization.SettingsRecycleBinFolderName.AsFormatted)
            .Tooltip(BdtLocalization.SettingsRecycleBinFolderNameDescription.AsFormatted)
            .LabelWidth(SETTINGS_LABEL_WIDTH)
            .CharLimit(60)
            .Text(RecycleBinFolderName)
            .OnEditEnd(name => {
                bool isValid = !string.IsNullOrWhiteSpace(name) && name.Length <= 60;
                if (isValid)
                {
                    SetRecycleBinFolderName(name);
                }
                recycleBinFolderNameField!.MarkAsError(!isValid, "Invalid folder name. Must not be empty and under 60 characters.".AsLoc());
            });
        root.Add(recycleBinFolderNameField);

        root.Add(new Title(BdtLocalization.SettingsPlaceFolderHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        // --- BLUEPRINT SPACING ---
        var spacingRow = new Row(2.pt()).AlignItemsCenter();
        var spacingLabel = new Label(BdtLocalization.SettingsBlueprintSpacingLabel.AsFormatted)
            .Tooltip(BdtLocalization.SettingsBlueprintSpacingDescription.AsFormatted)
            .Width(SETTINGS_LABEL_WIDTH);
        spacingRow.Add(spacingLabel);

        var spacingSpacer = new UiComponent().FlexGrow(1f);
        spacingRow.Add(spacingSpacer);

        var spacingControlRow = new Row(2.pt()).AlignItemsCenter();

        var spacingMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var spacingPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        TextField spacingInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(45.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(spacingInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        spacingInput.Text(BlueprintSpacing.ToString());

        spacingControlRow.Add(spacingMinusBtn);
        spacingControlRow.Add(spacingInput);
        spacingControlRow.Add(spacingPlusBtn);
        spacingRow.Add(spacingControlRow);
        root.Add(spacingRow);

        Action<int> updateSpacing = (val) =>
        {
            SetBlueprintSpacing(val);
            spacingInput.Text(BlueprintSpacing.ToString());
        };

        spacingInput.OnValueChanged((text) =>
        {
            if (int.TryParse(text, out int val))
            {
                updateSpacing(val);
            }
        });

        Action<int> adjustSpacing = (sign) =>
        {
            if (int.TryParse(spacingInput.GetText(), out int current))
            {
                int step = 1;
                int next = Math.Max(0, Math.Min(12, current + sign * step));
                updateSpacing(next);
            }
        };

        spacingMinusBtn.OnClick(() => adjustSpacing(-1), allowKeyPresses: true);
        spacingPlusBtn.OnClick(() => adjustSpacing(1), allowKeyPresses: true);

        root.Add(new Title(BdtLocalization.SettingsUndoHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        BdtKeyBindingField undoPrimaryField;
        BdtKeyBindingField undoSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsUndoHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => UndoHotkey,
            hotkey =>
            {
                UndoHotkey = hotkey;
                SaveGlobalHotkey(UNDO_HOTKEY_PRIMARY_KEY, UNDO_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out undoPrimaryField,
            out undoSecondaryField));

        root.Add(new Title(BdtLocalization.SettingsTransportConstructionHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle legacyBeltConfigurationsToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsLegacyBeltConfigurations.AsFormatted)
            .Tooltip(BdtLocalization.SettingsLegacyBeltConfigurationsDescription.AsFormatted)
            .Value(LegacyBeltConfigurationsEnabled)
            .OnValueChanged(SetLegacyBeltConfigurations);
        root.Add(legacyBeltConfigurationsToggle);

        root.Add(new Title(BdtLocalization.SettingsTransportCleanupHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        BdtKeyBindingField transportCleanupPrimaryField;
        BdtKeyBindingField transportCleanupSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsTransportCleanupHotkey.AsFormatted,
            BdtLocalization.SettingsGlobalHotkeyTooltip.AsFormatted,
            () => TransportCleanupHotkey,
            hotkey =>
            {
                TransportCleanupHotkey = hotkey;
                SaveGlobalHotkey(TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY, TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY, hotkey);
            },
            out transportCleanupPrimaryField,
            out transportCleanupSecondaryField));

        root.Add(BuildFooter(() =>
        {
            languageDropdown.SetValue(MarkdownTableLanguage);
            numberFormatDropdown.SetValue(MarkdownNumberFormat);
            instantBuildToggle.Value(InstantBuildModeEnabled);
            legacyBeltConfigurationsToggle.Value(LegacyBeltConfigurationsEnabled);
            throughputOverlayToggle.Value(ThroughputOverlayEnabled);
            throughputGlowToggle.Value(ThroughputGlowEnabled);
            heatmapDropdown.SetValue(ThroughputHeatmapMode);
            colorblindToggle.Value(ThroughputColorblindMode);
            colorblindToggle.Enabled(ThroughputHeatmapMode != ThroughputHeatmapMode.None);
            showAsPercentToggle.Value(ThroughputShowAsPercent);
            pollutionOverlayToggle.Value(PollutionOverlayEnabled);
            pollutionGlowToggle.Value(PollutionGlowEnabled);
            daysInput.Text(PollutionDaysToAverage.ToString());
            showAirToggle.Value(PollutionShowAir);
            showGroundToggle.Value(PollutionShowGround);
            showVehicleToggle.Value(PollutionShowVehicle);
            showShipToggle.Value(PollutionShowShip);
            layoutBoxModeToggle.Value(LayoutBoxModeEnabled);
            heightFilterDropdown.SetValue(HeightFilterMaxVisibleLevel);
            recycleBinToggle.Value(UseRecycleBin);
            recycleBinFolderNameField.Text(RecycleBinFolderName);
            recycleBinFolderNameField.MarkAsError(false);
            spacingInput.Text(BlueprintSpacing.ToString());
            showLayerPrimaryField.Refresh();
            showLayerSecondaryField.Refresh();
            hideLayerPrimaryField.Refresh();
            hideLayerSecondaryField.Refresh();
            throughputTogglePrimaryField.Refresh();
            throughputToggleSecondaryField.Refresh();
            throughputAoEToolPrimaryField.Refresh();
            throughputAoEToolSecondaryField.Refresh();
//             layoutBoxModePrimaryField.Refresh();
//             layoutBoxModeSecondaryField.Refresh();
            pollutionTogglePrimaryField.Refresh();
            pollutionToggleSecondaryField.Refresh();
            undoPrimaryField.Refresh();
            undoSecondaryField.Refresh();
            transportCleanupPrimaryField.Refresh();
            transportCleanupSecondaryField.Refresh();
        }));

        return root;
    }

    private static Row BuildHotkeyRow(
        LocStrFormatted label,
        LocStrFormatted tooltip,
        Func<BdtHotkey> getHotkey,
        Action<BdtHotkey> setHotkey,
        out BdtKeyBindingField primaryField,
        out BdtKeyBindingField secondaryField)
    {
        primaryField = new BdtKeyBindingField(getHotkey, setHotkey, isPrimary: true);
        secondaryField = new BdtKeyBindingField(getHotkey, setHotkey, isPrimary: false);
        return new Row(2.pt())
        {
            new Label(label)
                .Tooltip(tooltip)
                .NoShrink()
                .Width(SETTINGS_LABEL_WIDTH),
            primaryField,
            secondaryField,
        };
    }

    private static PanelFooterRow BuildFooter(Action refresh)
    {
        var status = new Label(LocStrFormatted.Empty).MarginTopBottom(1.pt());

        var reset = new ButtonText(Button.General, BdtLocalization.SettingsRestoreDefaults.AsFormatted, () =>
        {
            MarkdownTableLanguage = MarkdownTableLanguage.English;
            MarkdownNumberFormat = MarkdownNumberFormat.Auto;
            SetInstantBuildMode(false);
            SetLegacyBeltConfigurations(true);
            SetHeightFilterMaxVisibleLevel(6);
            SetThroughputOverlayEnabled(true);
            SetThroughputGlowEnabled(true);
            SetThroughputHeatmapMode(ThroughputHeatmapMode.Capacity);
            SetThroughputColorblindMode(false);
            SetThroughputShowAsPercent(false);
            SetPollutionOverlayEnabled(false);
            SetPollutionGlowEnabled(false);
            SetPollutionDaysToAverage(30);
            SetPollutionShowAir(true);
            SetPollutionShowGround(true);
            SetPollutionShowVehicle(true);
            SetPollutionShowShip(true);
            SetUseRecycleBin(true);
            SetRecycleBinFolderName("Recycle Bin");
            SetBlueprintSpacing(6);
            HeightFilterShowLayerHotkey = DEFAULT_HEIGHT_FILTER_SHOW_LAYER_HOTKEY;
            HeightFilterHideLayerHotkey = DEFAULT_HEIGHT_FILTER_HIDE_LAYER_HOTKEY;
            ThroughputOverlayToggleHotkey = DEFAULT_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY;
            ThroughputAoEToolHotkey = DEFAULT_THROUGHPUT_AOE_TOOL_HOTKEY;
            PollutionOverlayToggleHotkey = DEFAULT_POLLUTION_OVERLAY_TOGGLE_HOTKEY;
//             LayoutBoxModeToggleHotkey = DEFAULT_LAYOUT_BOX_MODE_TOGGLE_HOTKEY;
            UndoHotkey = DEFAULT_UNDO_HOTKEY;
            TransportCleanupHotkey = DEFAULT_TRANSPORT_CLEANUP_HOTKEY;
            SaveGlobalHotkey(HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY, DEFAULT_HEIGHT_FILTER_SHOW_LAYER_HOTKEY);
            SaveGlobalHotkey(HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY, DEFAULT_HEIGHT_FILTER_HIDE_LAYER_HOTKEY);
            SaveGlobalHotkey(THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, DEFAULT_THROUGHPUT_OVERLAY_TOGGLE_HOTKEY);
            SaveGlobalHotkey(THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY, THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY, DEFAULT_THROUGHPUT_AOE_TOOL_HOTKEY);
            SaveGlobalHotkey(POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, DEFAULT_POLLUTION_OVERLAY_TOGGLE_HOTKEY);
//             SaveGlobalHotkey(LAYOUT_BOX_MODE_TOGGLE_HOTKEY_PRIMARY_KEY, LAYOUT_BOX_MODE_TOGGLE_HOTKEY_SECONDARY_KEY, DEFAULT_LAYOUT_BOX_MODE_TOGGLE_HOTKEY);
            SaveGlobalHotkey(UNDO_HOTKEY_PRIMARY_KEY, UNDO_HOTKEY_SECONDARY_KEY, DEFAULT_UNDO_HOTKEY);
            SaveGlobalHotkey(TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY, TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY, DEFAULT_TRANSPORT_CLEANUP_HOTKEY);
            refresh();
            status.Value(BdtLocalization.SettingsRestoredDefaults.AsFormatted);
        }).Tooltip(BdtLocalization.SettingsRestoreDefaultsTooltip.AsFormatted);

        var save = new ButtonText(Button.Primary, BdtLocalization.SettingsSaveAsGlobal.AsFormatted, () =>
        {
            if (s_store == null)
            {
                status.Value(BdtLocalization.SettingsStoreNotInitialized.AsFormatted);
                return;
            }

            SaveToJsonStore(s_store);
            status.Value(TrySaveGlobalConfig(out string error)
                ? BdtLocalization.SettingsSavedToConfig.AsFormatted
                : new LocStrFormatted(string.Format(BdtLocalization.SettingsSaveFailed.TranslatedString, error)));
        }).Tooltip(BdtLocalization.SettingsSaveAsGlobalTooltip.AsFormatted);

        return new PanelFooterRow().BodyAdd(
            row => row.Gap(2.pt()).AlignItemsCenter(),
            status,
            new UiComponent().FlexGrow(1f),
            reset,
            save);
    }

    private static bool TrySaveGlobalConfig(out string error)
    {
        error = string.Empty;
        try
        {
            if (s_config != null && !s_config.TrySetValue(MARKDOWN_TABLE_LANGUAGE_KEY, (int)MarkdownTableLanguage, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(MARKDOWN_NUMBER_FORMAT_KEY, (int)MarkdownNumberFormat, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(INSTANT_BUILD_MODE_KEY, InstantBuildModeEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(LEGACY_BELT_CONFIGURATIONS_KEY, LegacyBeltConfigurationsEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(THROUGHPUT_OVERLAY_ENABLED_KEY, ThroughputOverlayEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(THROUGHPUT_GLOW_ENABLED_KEY, ThroughputGlowEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(THROUGHPUT_HEATMAP_MODE_KEY, (int)ThroughputHeatmapMode, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(THROUGHPUT_COLORBLIND_MODE_KEY, ThroughputColorblindMode, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(THROUGHPUT_SHOW_AS_PERCENT_KEY, ThroughputShowAsPercent, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_OVERLAY_ENABLED_KEY, PollutionOverlayEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_GLOW_ENABLED_KEY, PollutionGlowEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_DAYS_TO_AVERAGE_KEY, PollutionDaysToAverage, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_AIR_KEY, PollutionShowAir, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_GROUND_KEY, PollutionShowGround, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_VEHICLE_KEY, PollutionShowVehicle, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_SHIP_KEY, PollutionShowShip, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(USE_RECYCLE_BIN_KEY, UseRecycleBin, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(RECYCLE_BIN_FOLDER_NAME_KEY, RecycleBinFolderName, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(BLUEPRINT_SPACING_KEY, BlueprintSpacing, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, TransportCleanupHotkey, TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY, TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, HeightFilterShowLayerHotkey, HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, HeightFilterHideLayerHotkey, HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY, HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, ThroughputOverlayToggleHotkey, THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, ThroughputAoEToolHotkey, THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY, THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, PollutionOverlayToggleHotkey, POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY, POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY, out error))
                return false;
//             if (s_config != null && !TrySetHotkeyConfig(s_config, LayoutBoxModeToggleHotkey, LAYOUT_BOX_MODE_TOGGLE_HOTKEY_PRIMARY_KEY, LAYOUT_BOX_MODE_TOGGLE_HOTKEY_SECONDARY_KEY, out error))
//                 return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, UndoHotkey, UNDO_HOTKEY_PRIMARY_KEY, UNDO_HOTKEY_SECONDARY_KEY, out error))
                return false;

            if (string.IsNullOrWhiteSpace(s_modDirectory))
            {
                error = "Could not resolve mod directory.";
                return false;
            }

            string path = Path.Combine(s_modDirectory, "config.json");
            string json = File.ReadAllText(path);
            string updated = TryReplaceConfigDefault(json, MARKDOWN_TABLE_LANGUAGE_KEY, (int)MarkdownTableLanguage, out bool languageUpdated);
            updated = TryReplaceConfigDefault(updated, MARKDOWN_NUMBER_FORMAT_KEY, (int)MarkdownNumberFormat, out bool numberFormatUpdated);
            updated = TryReplaceConfigDefault(updated, INSTANT_BUILD_MODE_KEY, InstantBuildModeEnabled, out bool instantBuildUpdated);
            updated = TryReplaceConfigDefault(updated, LEGACY_BELT_CONFIGURATIONS_KEY, LegacyBeltConfigurationsEnabled, out bool legacyBeltConfigurationsUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_OVERLAY_ENABLED_KEY, ThroughputOverlayEnabled, out bool throughputOverlayEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_GLOW_ENABLED_KEY, ThroughputGlowEnabled, out bool throughputGlowEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_HEATMAP_MODE_KEY, (int)ThroughputHeatmapMode, out bool throughputHeatmapModeUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_COLORBLIND_MODE_KEY, ThroughputColorblindMode, out bool throughputColorblindModeUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_SHOW_AS_PERCENT_KEY, ThroughputShowAsPercent, out bool throughputShowAsPercentUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_OVERLAY_ENABLED_KEY, PollutionOverlayEnabled, out bool pollutionOverlayEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_GLOW_ENABLED_KEY, PollutionGlowEnabled, out bool pollutionGlowEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_DAYS_TO_AVERAGE_KEY, PollutionDaysToAverage, out bool pollutionDaysToAverageUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_AIR_KEY, PollutionShowAir, out bool pollutionShowAirUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_GROUND_KEY, PollutionShowGround, out bool pollutionShowGroundUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_VEHICLE_KEY, PollutionShowVehicle, out bool pollutionShowVehicleUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_SHIP_KEY, PollutionShowShip, out bool pollutionShowShipUpdated);
            updated = TryReplaceConfigDefault(updated, LAYOUT_BOX_MODE_ENABLED_KEY, LayoutBoxModeEnabled, out bool layoutBoxModeEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, USE_RECYCLE_BIN_KEY, UseRecycleBin, out bool useRbUpdated);
            updated = TryReplaceConfigDefault(updated, RECYCLE_BIN_FOLDER_NAME_KEY, RecycleBinFolderName, out bool rbNameUpdated);
            updated = TryReplaceConfigDefault(updated, BLUEPRINT_SPACING_KEY, BlueprintSpacing, out bool blueprintSpacingUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                TransportCleanupHotkey,
                TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY,
                TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY,
                out bool transportCleanupHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                HeightFilterShowLayerHotkey,
                HEIGHT_FILTER_SHOW_LAYER_HOTKEY_PRIMARY_KEY,
                HEIGHT_FILTER_SHOW_LAYER_HOTKEY_SECONDARY_KEY,
                out bool showLayerHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                HeightFilterHideLayerHotkey,
                HEIGHT_FILTER_HIDE_LAYER_HOTKEY_PRIMARY_KEY,
                HEIGHT_FILTER_HIDE_LAYER_HOTKEY_SECONDARY_KEY,
                out bool hideLayerHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                ThroughputOverlayToggleHotkey,
                THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY,
                THROUGHPUT_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY,
                out bool throughputOverlayToggleHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                ThroughputAoEToolHotkey,
                THROUGHPUT_AOE_TOOL_HOTKEY_PRIMARY_KEY,
                THROUGHPUT_AOE_TOOL_HOTKEY_SECONDARY_KEY,
                out bool throughputAoEToolHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                PollutionOverlayToggleHotkey,
                POLLUTION_OVERLAY_TOGGLE_HOTKEY_PRIMARY_KEY,
                POLLUTION_OVERLAY_TOGGLE_HOTKEY_SECONDARY_KEY,
                out bool pollutionOverlayToggleHotkeyUpdated);
//             updated = TryReplaceHotkeyConfigDefaults(
//                 updated,
//                 LayoutBoxModeToggleHotkey,
//                 LAYOUT_BOX_MODE_TOGGLE_HOTKEY_PRIMARY_KEY,
//                 LAYOUT_BOX_MODE_TOGGLE_HOTKEY_SECONDARY_KEY,
//                 out bool layoutBoxModeHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                UndoHotkey,
                UNDO_HOTKEY_PRIMARY_KEY,
                UNDO_HOTKEY_SECONDARY_KEY,
                out bool undoHotkeyUpdated);
            if (!languageUpdated)
            {
                error = "Could not find markdown_table_language default in config.json.";
                return false;
            }
            if (!numberFormatUpdated)
            {
                error = "Could not find markdown_number_format default in config.json.";
                return false;
            }
            if (!instantBuildUpdated)
            {
                error = "Could not find instant_build_mode default in config.json.";
                return false;
            }
            if (!legacyBeltConfigurationsUpdated)
            {
                error = "Could not find legacy_belt_configurations default in config.json.";
                return false;
            }
            if (!throughputOverlayEnabledUpdated)
            {
                error = "Could not find throughput_overlay_enabled default in config.json.";
                return false;
            }
            if (!throughputGlowEnabledUpdated)
            {
                error = "Could not find throughput_glow_enabled default in config.json.";
                return false;
            }
            if (!throughputHeatmapModeUpdated)
            {
                error = "Could not find throughput_heatmap_mode default in config.json.";
                return false;
            }
            if (!throughputColorblindModeUpdated)
            {
                error = "Could not find throughput_colorblind_mode default in config.json.";
                return false;
            }
            if (!throughputShowAsPercentUpdated)
            {
                error = "Could not find throughput_show_as_percent default in config.json.";
                return false;
            }
            if (!pollutionOverlayEnabledUpdated)
            {
                error = "Could not find pollution_overlay_enabled default in config.json.";
                return false;
            }
            if (!pollutionGlowEnabledUpdated)
            {
                error = "Could not find pollution_glow_enabled default in config.json.";
                return false;
            }
            if (!pollutionDaysToAverageUpdated)
            {
                error = "Could not find pollution_days_to_average default in config.json.";
                return false;
            }
            if (!pollutionShowAirUpdated)
            {
                error = "Could not find pollution_show_air default in config.json.";
                return false;
            }
            if (!pollutionShowGroundUpdated)
            {
                error = "Could not find pollution_show_ground default in config.json.";
                return false;
            }
            if (!pollutionShowVehicleUpdated)
            {
                error = "Could not find pollution_show_vehicle default in config.json.";
                return false;
            }
            if (!pollutionShowShipUpdated)
            {
                error = "Could not find pollution_show_ship default in config.json.";
                return false;
            }
            if (!layoutBoxModeEnabledUpdated)
            {
                error = "Could not find layout_box_mode_enabled default in config.json.";
                return false;
            }
            if (!useRbUpdated)
            {
                error = "Could not find use_recycle_bin default in config.json.";
                return false;
            }
            if (!rbNameUpdated)
            {
                error = "Could not find recycle_bin_folder_name default in config.json.";
                return false;
            }
            if (!blueprintSpacingUpdated)
            {
                error = "Could not find blueprint_spacing default in config.json.";
                return false;
            }
            if (!transportCleanupHotkeyUpdated || !showLayerHotkeyUpdated || !hideLayerHotkeyUpdated || !throughputOverlayToggleHotkeyUpdated || !throughputAoEToolHotkeyUpdated || !pollutionOverlayToggleHotkeyUpdated || !undoHotkeyUpdated)
            {
                error = "Could not find hotkey defaults in config.json.";
                return false;
            }

            File.WriteAllText(path, updated, new System.Text.UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void SetMarkdownTableLanguage(MarkdownTableLanguage language)
    {
        MarkdownTableLanguage = language;
    }

    private static void SetMarkdownNumberFormat(MarkdownNumberFormat numberFormat)
    {
        MarkdownNumberFormat = numberFormat;
    }

    private static void SetInstantBuildMode(bool enabled)
    {
        if (InstantBuildModeEnabled == enabled)
            return;

        InstantBuildModeEnabled = enabled;
        try { InstantBuildModeChanged?.Invoke(enabled); }
        catch (Exception ex) { s_log.Warning($"Instant build mode change handler failed: {ex.Message}"); }
    }

    private static void SetLegacyBeltConfigurations(bool enabled)
    {
        LegacyBeltConfigurationsEnabled = enabled;
    }

    public static void SetThroughputOverlayEnabled(bool enabled)
    {
        if (ThroughputOverlayEnabled == enabled)
            return;

        ThroughputOverlayEnabled = enabled;
        try { ThroughputOverlayEnabledChanged?.Invoke(enabled); }
        catch (Exception ex) { s_log.Warning($"Throughput overlay visibility change handler failed: {ex.Message}"); }
    }

    public static void SetThroughputGlowEnabled(bool enabled)
    {
        ThroughputGlowEnabled = enabled;
    }

    public static void SetHeightFilterMaxVisibleLevel(int level)
    {
        if (HeightFilterMaxVisibleLevel == level)
            return;

        HeightFilterMaxVisibleLevel = level;
        try { HeightFilterMaxVisibleLevelChanged?.Invoke(level); }
        catch (Exception ex) { s_log.Warning($"Height filter max visible level change handler failed: {ex.Message}"); }
    }

    private static UiComponent HeightFilterDropdownOption(int level, int index, bool isInDropdown)
    {
        string labelText = level == 6 ? "All" : level.ToString();
        return new Label(labelText.AsLoc());
    }

    private static void SaveGlobalHotkey(string primaryKey, string secondaryKey, BdtHotkey hotkey)
    {
        if (s_config == null || string.IsNullOrWhiteSpace(s_modDirectory))
            return;

        try
        {
            string error;
            if (!s_config.TrySetValue(primaryKey, hotkey.PrimaryConfigString(), out error))
            {
                s_log.Warning($"Failed to set config value {primaryKey}: {error}");
                return;
            }
            if (!s_config.TrySetValue(secondaryKey, hotkey.SecondaryConfigString(), out error))
            {
                s_log.Warning($"Failed to set config value {secondaryKey}: {error}");
                return;
            }

            string path = Path.Combine(s_modDirectory, "config.json");
            string json = File.ReadAllText(path);
            string updated = TryReplaceHotkeyConfigDefaults(json, hotkey, primaryKey, secondaryKey, out bool updatedOK);
            if (updatedOK)
            {
                File.WriteAllText(path, updated, new System.Text.UTF8Encoding(false));
                s_log.Info($"Successfully wrote global hotkey {primaryKey} to config.json");
            }
            else
            {
                s_log.Warning($"Could not find default value for {primaryKey}/{secondaryKey} in config.json to replace.");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to save hotkey {primaryKey} to config.json: {ex.Message}");
        }
    }

    private static bool TrySetHotkeyConfig(
        ModJsonConfig config,
        BdtHotkey hotkey,
        string primaryKey,
        string secondaryKey,
        out string error)
    {
        if (!config.TrySetValue(primaryKey, hotkey.PrimaryConfigString(), out error))
            return false;
        if (!config.TrySetValue(secondaryKey, hotkey.SecondaryConfigString(), out error))
            return false;

        return true;
    }

    private static BdtHotkey HotkeyFromConfig(
        ModJsonConfig config,
        string primaryKey,
        string secondaryKey,
        string legacyKeyKey,
        string legacyCtrlKey,
        string legacyAltKey,
        string legacyShiftKey,
        BdtHotkey fallback)
    {
        string primary = config.GetString(primaryKey, fallback.PrimaryConfigString());
        string secondary = config.GetString(secondaryKey, fallback.SecondaryConfigString());
        BdtHotkey fromStrings = BdtHotkey.FromConfigStrings(primary, secondary, fallback);

        if (primary != fallback.PrimaryConfigString() || secondary != fallback.SecondaryConfigString())
            return fromStrings;

        if (!ConfigHasValue(config, legacyKeyKey))
            return fromStrings;

        return BdtHotkey.FromLegacy(
            KeyCodeFromInt(config.GetInt(legacyKeyKey, 0), KeyCode.None),
            config.GetBool(legacyCtrlKey, false),
            config.GetBool(legacyAltKey, false),
            config.GetBool(legacyShiftKey, false));
    }

    private static string TryReplaceHotkeyConfigDefaults(
        string json,
        BdtHotkey hotkey,
        string primaryKey,
        string secondaryKey,
        out bool updated)
    {
        string result = TryReplaceConfigDefault(json, primaryKey, hotkey.PrimaryConfigString(), out bool primaryUpdated);
        result = TryReplaceConfigDefault(result, secondaryKey, hotkey.SecondaryConfigString(), out bool secondaryUpdated);
        updated = primaryUpdated && secondaryUpdated;
        return result;
    }

    private static string TryReplaceConfigDefault(string json, string key, int value, out bool updated)
    {
        string pattern = "(\"" + key + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*)-?\\d+";
        updated = Regex.IsMatch(json, pattern, RegexOptions.Singleline);
        if (!updated)
            return json;
        return Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RegexOptions.Singleline);
    }

    private static string TryReplaceConfigDefault(string json, string key, bool value, out bool updated)
    {
        string pattern = "(\"" + key + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*)(true|false|0|1)";
        updated = Regex.IsMatch(json, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!updated)
            return json;
        return Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + (value ? "true" : "false"),
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string TryReplaceConfigDefault(string json, string key, string value, out bool updated)
    {
        string pattern = "(\"" + key + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*)\"(?:\\\\.|[^\"])*\"";
        updated = Regex.IsMatch(json, pattern, RegexOptions.Singleline);
        if (!updated)
            return json;
        return Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + "\"" + EscapeJsonString(value) + "\"",
            RegexOptions.Singleline);
    }

    private static string EscapeJsonString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool ConfigHasValue(ModJsonConfig config, string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(s_modDirectory))
                return false;

            string json = File.ReadAllText(Path.Combine(s_modDirectory, "config.json"));
            return Regex.IsMatch(json, "\"" + Regex.Escape(key) + "\"\\s*:");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to inspect BDT config for legacy hotkey '{key}': {ex.Message}");
            return false;
        }
    }

    private static void LoadFromJsonStore(
        IModStateJsonStore store,
        MarkdownTableLanguage initialLanguage,
        MarkdownNumberFormat initialNumberFormat,
        bool initialInstantBuildMode,
        bool initialLegacyBeltConfigurations,
        bool initialThroughputOverlayEnabled,
        bool initialThroughputGlowEnabled,
        ThroughputHeatmapMode initialThroughputHeatmapMode,
        bool initialThroughputColorblindMode,
        bool initialThroughputShowAsPercent,
        bool initialPollutionOverlayEnabled,
        bool initialPollutionGlowEnabled,
        int initialPollutionDaysToAverage,
        bool initialPollutionShowAir,
        bool initialPollutionShowGround,
        bool initialPollutionShowVehicle,
        bool initialPollutionShowShip,
        bool initialLayoutBoxModeEnabled,
        bool initialUseRecycleBin,
        string initialRecycleBinFolderName,
        int initialBlueprintSpacing)
    {
        MarkdownTableLanguage = initialLanguage;
        MarkdownNumberFormat = initialNumberFormat;
        InstantBuildModeEnabled = initialInstantBuildMode;
        LegacyBeltConfigurationsEnabled = initialLegacyBeltConfigurations;
        ThroughputOverlayEnabled = initialThroughputOverlayEnabled;
        ThroughputGlowEnabled = initialThroughputGlowEnabled;
        ThroughputHeatmapMode = initialThroughputHeatmapMode;
        ThroughputColorblindMode = initialThroughputColorblindMode;
        ThroughputShowAsPercent = initialThroughputShowAsPercent;
        PollutionOverlayEnabled = initialPollutionOverlayEnabled;
        PollutionGlowEnabled = initialPollutionGlowEnabled;
        PollutionDaysToAverage = initialPollutionDaysToAverage;
        PollutionShowAir = initialPollutionShowAir;
        PollutionShowGround = initialPollutionShowGround;
        PollutionShowVehicle = initialPollutionShowVehicle;
        PollutionShowShip = initialPollutionShowShip;
        LayoutBoxModeEnabled = initialLayoutBoxModeEnabled;
        UseRecycleBin = initialUseRecycleBin;
        RecycleBinFolderName = initialRecycleBinFolderName;
        BlueprintSpacing = initialBlueprintSpacing;

        string json = store.LoadJson();
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root))
                return;

            if (!TryGetInt(root, "schemaVersion", out int schemaVersion)
                || schemaVersion != SETTINGS_SCHEMA_VERSION)
                return;

            if (TryGetInt(root, "markdownTableLanguage", out int language))
                MarkdownTableLanguage = FromInt(language);
            if (TryGetInt(root, "markdownNumberFormat", out int numberFormat))
                MarkdownNumberFormat = NumberFormatFromInt(numberFormat);
            if (TryGetBool(root, "instantBuildMode", out bool instantBuildMode))
                InstantBuildModeEnabled = instantBuildMode;
            if (TryGetBool(root, "legacyBeltConfigurations", out bool legacyBeltConfigurations))
                LegacyBeltConfigurationsEnabled = legacyBeltConfigurations;
            if (TryGetInt(root, "heightFilterMaxVisibleLevel", out int heightFilterMaxVisibleLevel))
                HeightFilterMaxVisibleLevel = heightFilterMaxVisibleLevel;
            if (TryGetBool(root, "throughputOverlayEnabled", out bool throughputOverlayEnabled))
                ThroughputOverlayEnabled = throughputOverlayEnabled;
            if (TryGetBool(root, "throughputGlowEnabled", out bool throughputGlowEnabled))
                ThroughputGlowEnabled = throughputGlowEnabled;
            if (TryGetInt(root, "throughputHeatmapMode", out int heatmapMode))
                ThroughputHeatmapMode = HeatmapModeFromInt(heatmapMode);
            if (TryGetBool(root, "throughputColorblindMode", out bool colorblindMode))
                ThroughputColorblindMode = colorblindMode;
             if (TryGetBool(root, "throughputShowAsPercent", out bool showAsPercent))
                ThroughputShowAsPercent = showAsPercent;
            if (TryGetBool(root, "pollutionOverlayEnabled", out bool pollutionOverlayEnabled))
                PollutionOverlayEnabled = pollutionOverlayEnabled;
            if (TryGetBool(root, "pollutionGlowEnabled", out bool pollutionGlowEnabled))
                PollutionGlowEnabled = pollutionGlowEnabled;
            if (TryGetInt(root, "pollutionDaysToAverage", out int pollutionDaysToAverage))
                PollutionDaysToAverage = pollutionDaysToAverage;
            if (TryGetBool(root, "pollutionShowAir", out bool pollutionShowAir))
                PollutionShowAir = pollutionShowAir;
            if (TryGetBool(root, "pollutionShowGround", out bool pollutionShowGround))
                PollutionShowGround = pollutionShowGround;
            if (TryGetBool(root, "pollutionShowVehicle", out bool pollutionShowVehicle))
                PollutionShowVehicle = pollutionShowVehicle;
            if (TryGetBool(root, "pollutionShowShip", out bool pollutionShowShip))
                PollutionShowShip = pollutionShowShip;
            if (TryGetBool(root, "layoutBoxModeEnabled", out bool layoutBoxModeEnabled))
                LayoutBoxModeEnabled = layoutBoxModeEnabled;
            if (TryGetBool(root, "useRecycleBin", out bool useRecycleBin))
                UseRecycleBin = useRecycleBin;
            if (TryGetString(root, "recycleBinFolderName", out string recycleBinFolderName))
                RecycleBinFolderName = recycleBinFolderName;
            if (TryGetInt(root, "blueprintSpacing", out int blueprintSpacing))
                BlueprintSpacing = blueprintSpacing;
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load BDT settings state from {store.StorageKind}: {ex.Message}");
        }
    }

    private static string BuildStateJson()
    {
        var writer = new JsonWriter(128);
        writer.AppendStartObject();
        writer.AppendNumberField("schemaVersion", SETTINGS_SCHEMA_VERSION);
        writer.AppendNumberField("markdownTableLanguage", (int)MarkdownTableLanguage);
        writer.AppendNumberField("markdownNumberFormat", (int)MarkdownNumberFormat);
        writer.AppendBoolField("instantBuildMode", InstantBuildModeEnabled);
        writer.AppendBoolField("legacyBeltConfigurations", LegacyBeltConfigurationsEnabled);
        writer.AppendNumberField("heightFilterMaxVisibleLevel", HeightFilterMaxVisibleLevel);
        writer.AppendBoolField("throughputOverlayEnabled", ThroughputOverlayEnabled);
        writer.AppendBoolField("throughputGlowEnabled", ThroughputGlowEnabled);
        writer.AppendNumberField("throughputHeatmapMode", (int)ThroughputHeatmapMode);
        writer.AppendBoolField("throughputColorblindMode", ThroughputColorblindMode);
        writer.AppendBoolField("throughputShowAsPercent", ThroughputShowAsPercent);
        writer.AppendBoolField("pollutionOverlayEnabled", PollutionOverlayEnabled);
        writer.AppendBoolField("pollutionGlowEnabled", PollutionGlowEnabled);
        writer.AppendNumberField("pollutionDaysToAverage", PollutionDaysToAverage);
        writer.AppendBoolField("pollutionShowAir", PollutionShowAir);
        writer.AppendBoolField("pollutionShowGround", PollutionShowGround);
        writer.AppendBoolField("pollutionShowVehicle", PollutionShowVehicle);
        writer.AppendBoolField("pollutionShowShip", PollutionShowShip);
        writer.AppendBoolField("layoutBoxModeEnabled", LayoutBoxModeEnabled);
        writer.AppendBoolField("useRecycleBin", UseRecycleBin);
        writer.AppendStringField("recycleBinFolderName", RecycleBinFolderName);
        writer.AppendNumberField("blueprintSpacing", BlueprintSpacing);
        writer.AppendEndObject();
        return writer.GetJsonAndClear();
    }

    private static ThroughputHeatmapMode HeatmapModeFromInt(int value)
    {
        switch (value)
        {
            case (int)ThroughputHeatmapMode.Relative:
                return ThroughputHeatmapMode.Relative;
            case (int)ThroughputHeatmapMode.Capacity:
                return ThroughputHeatmapMode.Capacity;
            default:
                return ThroughputHeatmapMode.None;
        }
    }

    private static UiComponent HeatmapDropdownOption(
        ThroughputHeatmapMode mode,
        int index,
        bool isInDropdown)
    {
        return new Label(HeatmapLabel(mode));
    }

    private static LocStrFormatted HeatmapLabel(ThroughputHeatmapMode mode)
    {
        switch (mode)
        {
            case ThroughputHeatmapMode.Relative:
                return "Relative".AsLoc();
            case ThroughputHeatmapMode.Capacity:
                return "Capacity".AsLoc();
            default:
                return "None".AsLoc();
        }
    }

    private static void AppendHotkeyFields(
        JsonWriter writer,
        BdtHotkey hotkey,
        string primaryKey,
        string secondaryKey)
    {
        writer.AppendStringField(primaryKey, hotkey.PrimaryConfigString());
        writer.AppendStringField(secondaryKey, hotkey.SecondaryConfigString());
    }

    private static BdtHotkey HotkeyFromState(
        Dict<string, object> root,
        string primaryKey,
        string secondaryKey,
        string legacyKeyKey,
        string legacyCtrlKey,
        string legacyAltKey,
        string legacyShiftKey,
        BdtHotkey fallback)
    {
        bool hasPrimary = TryGetString(root, primaryKey, out string primary);
        bool hasSecondary = TryGetString(root, secondaryKey, out string secondary);
        if (hasPrimary || hasSecondary)
            return BdtHotkey.FromConfigStrings(
                hasPrimary ? primary : fallback.PrimaryConfigString(),
                hasSecondary ? secondary : fallback.SecondaryConfigString(),
                fallback);

        if (!TryGetInt(root, legacyKeyKey, out int keyValue))
            return fallback;

        return BdtHotkey.FromLegacy(
            KeyCodeFromInt(keyValue, KeyCode.None),
            TryGetBool(root, legacyCtrlKey, out bool ctrl) && ctrl,
            TryGetBool(root, legacyAltKey, out bool alt) && alt,
            TryGetBool(root, legacyShiftKey, out bool shift) && shift);
    }

    private static MarkdownNumberFormat NumberFormatFromInt(int value)
    {
        switch (value)
        {
            case (int)MarkdownNumberFormat.English:
                return MarkdownNumberFormat.English;
            case (int)MarkdownNumberFormat.Local:
                return MarkdownNumberFormat.Local;
            default:
                return MarkdownNumberFormat.Auto;
        }
    }

    private static MarkdownTableLanguage FromInt(int value)
    {
        switch (value)
        {
            case (int)MarkdownTableLanguage.Local:
                return MarkdownTableLanguage.Local;
            case (int)MarkdownTableLanguage.Both:
                return MarkdownTableLanguage.Both;
            case (int)MarkdownTableLanguage.Hybrid:
                return MarkdownTableLanguage.Hybrid;
            default:
                return MarkdownTableLanguage.English;
        }
    }

    private static bool TryGetInt(Dict<string, object> obj, string key, out int value)
    {
        value = 0;
        if (!obj.TryGetValue(key, out object raw))
            return false;

        if (raw is int intValue)
        {
            value = intValue;
            return true;
        }

        if (raw is double doubleValue)
        {
            value = (int)doubleValue;
            return true;
        }

        if (raw is long longValue)
        {
            value = (int)longValue;
            return true;
        }

        return false;
    }

    private static bool TryGetBool(Dict<string, object> obj, string key, out bool value)
    {
        value = false;
        if (!obj.TryGetValue(key, out object raw))
            return false;

        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        if (raw is int intValue)
        {
            value = intValue != 0;
            return true;
        }

        if (raw is double doubleValue)
        {
            value = Math.Abs(doubleValue) > double.Epsilon;
            return true;
        }

        if (raw is long longValue)
        {
            value = longValue != 0;
            return true;
        }

        return false;
    }

    private static bool TryGetString(Dict<string, object> obj, string key, out string value)
    {
        value = string.Empty;
        if (!obj.TryGetValue(key, out object raw))
            return false;

        if (raw is string stringValue)
        {
            value = stringValue;
            return true;
        }

        return false;
    }

    private static KeyCode KeyCodeFromInt(int value, KeyCode fallback)
    {
        if (Enum.IsDefined(typeof(KeyCode), value))
            return (KeyCode)value;

        return fallback;
    }

    private static UiComponent LanguageDropdownOption(
        MarkdownTableLanguage language,
        int index,
        bool isInDropdown)
    {
        return new Label(LanguageLabel(language));
    }

    private static LocStrFormatted LanguageLabel(MarkdownTableLanguage language)
    {
        switch (language)
        {
            case MarkdownTableLanguage.Local:
                return BdtLocalization.SettingsLanguageLocal.AsFormatted;
            case MarkdownTableLanguage.Both:
                return BdtLocalization.SettingsLanguageBoth.AsFormatted;
            case MarkdownTableLanguage.Hybrid:
                return BdtLocalization.SettingsLanguageHybrid.AsFormatted;
            default:
                return BdtLocalization.SettingsLanguageEnglish.AsFormatted;
        }
    }

    private static UiComponent NumberFormatDropdownOption(
        MarkdownNumberFormat numberFormat,
        int index,
        bool isInDropdown)
    {
        return new Label(NumberFormatLabel(numberFormat));
    }

    private static LocStrFormatted NumberFormatLabel(MarkdownNumberFormat numberFormat)
    {
        switch (numberFormat)
        {
            case MarkdownNumberFormat.English:
                return BdtLocalization.SettingsNumberFormatEnglish.AsFormatted;
            case MarkdownNumberFormat.Local:
                return BdtLocalization.SettingsNumberFormatLocal.AsFormatted;
            default:
                return BdtLocalization.SettingsNumberFormatAuto.AsFormatted;
        }
    }
}
