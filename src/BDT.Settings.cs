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
using Mafi.Unity.InputControl;

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

internal enum HeightFilterTransportVisibility
{
    Low = 0,
    Medium = 1,
    High = 2,
}

internal enum HeightFilterPillarVisibility
{
    Detached = 0,
    Attached = 1,
    Top = 2,
    Off = 3,
}

internal static class DesignerToolkitSettings
{
    internal const string SettingsStateConfigKey = "dtkSettingsStateJson";

    private const string MARKDOWN_TABLE_LANGUAGE_KEY = "markdown_table_language";
    private const string MARKDOWN_NUMBER_FORMAT_KEY = "markdown_number_format";
    private const string INSTANT_BUILD_MODE_KEY = "instant_build_mode";
    private const string LEGACY_BELT_CONFIGURATIONS_KEY = "legacy_belt_configurations";

    private static Mafi.Core.Game.GameDifficultyConfig? s_difficultyConfig;

    public static void SetDifficultyConfig(Mafi.Core.Game.GameDifficultyConfig config)
    {
        s_difficultyConfig = config;
    }

    public static bool IsSandbox => s_difficultyConfig != null && s_difficultyConfig.IsSandbox;
    
    private const string THROUGHPUT_OVERLAY_ENABLED_KEY = "throughput_overlay_enabled";
    private const string THROUGHPUT_GLOW_ENABLED_KEY = "throughput_glow_enabled";
    private const string THROUGHPUT_HEATMAP_MODE_KEY = "throughput_heatmap_mode";
    private const string THROUGHPUT_COLORBLIND_MODE_KEY = "throughput_colorblind_mode";
    private const string THROUGHPUT_SHOW_AS_PERCENT_KEY = "throughput_show_as_percent";
    private const string POLLUTION_OVERLAY_ENABLED_KEY = "pollution_overlay_enabled";
    private const string POLLUTION_GLOW_ENABLED_KEY = "pollution_glow_enabled";
    private const string POLLUTION_GLOW_COLOR_KEY = "pollution_glow_color";
    private const string POLLUTION_DAYS_TO_AVERAGE_KEY = "pollution_days_to_average";
    private const string POLLUTION_SHOW_AIR_KEY = "pollution_show_air";
    private const string POLLUTION_SHOW_GROUND_KEY = "pollution_show_ground";
    private const string POLLUTION_SHOW_SOLID_WASTE_KEY = "pollution_show_solid_waste";
    private const string POLLUTION_SHOW_VEHICLE_KEY = "pollution_show_vehicle";
    private const string POLLUTION_SHOW_SHIP_KEY = "pollution_show_ship";
    private const string RADIATION_OVERLAY_ENABLED_KEY = "radiation_overlay_enabled";
    private const string RADIATION_GLOW_ENABLED_KEY = "radiation_glow_enabled";
    private const string RADIATION_DAYS_TO_AVERAGE_KEY = "radiation_days_to_average";
    private const string LAYOUT_BOX_MODE_ENABLED_KEY = "layout_box_mode_enabled";
    private const string USE_RECYCLE_BIN_KEY = "use_recycle_bin";
    private const string RECYCLE_BIN_FOLDER_NAME_KEY = "recycle_bin_folder_name";
    private const string BLUEPRINT_SPACING_KEY = "blueprint_spacing";
    private const string PRE_COLOR_PIPES_KEY = "pre_color_pipes";
    private const string HEIGHT_FILTER_TRANSPORT_VISIBILITY_KEY = "height_filter_transport_visibility";
    private const string HEIGHT_FILTER_PILLAR_VISIBILITY_KEY = "height_filter_pillar_visibility";

    private const int SETTINGS_SCHEMA_VERSION = 1;
    private const string SETTINGS_TAB_ICON_ASSET =
        "Assets/Unity/UserInterface/Toolbar/Blueprints.svg";
    private static readonly Percent SETTINGS_LABEL_WIDTH = 34.Percent();
    private static readonly Percent SETTINGS_COLUMN_WIDTH = 96.Percent();
    private static readonly Px SETTINGS_SECTION_INDENT = 4.pt();
    private static readonly Px SETTINGS_OPTIONS_GAP = 2.pt();
    private static readonly Px SETTINGS_CONTROL_WIDTH = 140.px();

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
    public static HeightFilterTransportVisibility HeightFilterTransportVisibility { get; private set; } = HeightFilterTransportVisibility.Medium;
    public static HeightFilterPillarVisibility HeightFilterPillarVisibility { get; private set; } = HeightFilterPillarVisibility.Detached;
    public static bool ThroughputOverlayEnabled { get; private set; } = true;
    public static bool ThroughputGlowEnabled { get; private set; } = true;
    public static ThroughputHeatmapMode ThroughputHeatmapMode { get; private set; } = ThroughputHeatmapMode.Capacity;
    public static bool ThroughputColorblindMode { get; private set; } = false;
    public static bool ThroughputShowAsPercent { get; private set; } = false;
    public static bool PollutionOverlayEnabled { get; private set; } = false;
    public static bool PollutionGlowEnabled { get; private set; } = false;
    public static ColorRgba PollutionGlowColor { get; private set; } = ColorRgba.White;
    public static int PollutionDaysToAverage { get; private set; } = 360;
    public static bool PollutionShowAir { get; private set; } = true;
    public static bool PollutionShowGround { get; private set; } = true;
    public static bool PollutionShowSolidWaste { get; private set; } = true;
    public static bool PollutionShowVehicle { get; private set; } = true;
    public static bool PollutionShowShip { get; private set; } = true;
    public static bool RadiationOverlayEnabled { get; private set; } = false;
    public static bool RadiationGlowEnabled { get; private set; } = false;
    public static int RadiationDaysToAverage { get; private set; } = 30;
    public static bool LayoutBoxModeEnabled { get; private set; } = false;
    public static bool UseRecycleBin { get; private set; } = true;
    public static string RecycleBinFolderName { get; private set; } = "Recycle Bin";
    public static int BlueprintSpacing { get; private set; } = 6;
    public static bool PreColorPipesEnabled { get; private set; } = true;
    public static bool IsFirstUnpausePending { get; set; } = true;

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


    public static event Action<bool>? InstantBuildModeChanged;
    public static event Action<int>? HeightFilterMaxVisibleLevelChanged;
    public static event Action<HeightFilterTransportVisibility>? HeightFilterTransportVisibilityChanged;
    public static event Action<HeightFilterPillarVisibility>? HeightFilterPillarVisibilityChanged;
    public static event Action<bool>? ThroughputOverlayEnabledChanged;
    public static event Action<bool>? PollutionOverlayEnabledChanged;
    public static event Action<int>? PollutionDaysToAverageChanged;
    public static event Action<bool>? PreColorPipesEnabledChanged;

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

    public static bool TrySetPollutionGlowColor(string? value, out string error)
    {
        if (!TryParsePollutionGlowColor(value, out ColorRgba parsed))
        {
            error = $"Invalid pollution glow color '{value}'. Use white, brown, purple, or #RRGGBB.";
            return false;
        }

        PollutionGlowColor = parsed;
        error = string.Empty;
        return true;
    }

    public static string FormatPollutionGlowColor()
    {
        return "#" + PollutionGlowColor.ToHexRgb();
    }

    private static ColorRgba ReadPollutionGlowColor(string value)
    {
        if (TryParsePollutionGlowColor(value, out ColorRgba parsed))
            return parsed;

        s_log.Warning(
            $"Invalid {POLLUTION_GLOW_COLOR_KEY} value '{value}' in config.json. "
            + "Use white, brown, purple, or #RRGGBB. Using white.");
        return ColorRgba.White;
    }

    private static bool TryParsePollutionGlowColor(string? value, out ColorRgba color)
    {
        string normalized = (value ?? string.Empty).Trim();
        switch (normalized.ToLowerInvariant())
        {
            case "white":
                color = ColorRgba.White;
                return true;
            case "brown":
                color = ColorRgba.Brown;
                return true;
            case "purple":
                color = ColorRgba.Purple;
                return true;
            default:
                return ColorRgba.TryParseHex(normalized, out color);
        }
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
    public static void SetPollutionShowSolidWaste(bool enabled) { PollutionShowSolidWaste = enabled; }
    public static void SetPollutionShowVehicle(bool enabled) { PollutionShowVehicle = enabled; }
    public static void SetPollutionShowShip(bool enabled) { PollutionShowShip = enabled; }

    public static void SetRadiationOverlayEnabled(bool enabled) { RadiationOverlayEnabled = enabled; }
    public static void SetRadiationGlowEnabled(bool enabled) { RadiationGlowEnabled = enabled; }
    public static void SetRadiationDaysToAverage(int days) { RadiationDaysToAverage = Math.Max(0, Math.Min(360, days)); }

    public static void SetLayoutBoxModeEnabled(bool enabled)
    {
        LayoutBoxModeEnabled = enabled;
    }


    public static void Initialize(ModJsonConfig config, IModStateJsonStore store, string modDirectory, bool gameWasLoaded)
    {
        s_config = config;
        s_store = store;
        s_modDirectory = modDirectory;
        IsFirstUnpausePending = !gameWasLoaded;
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
        bool initialPollutionShowSolidWaste = config.GetBool(POLLUTION_SHOW_SOLID_WASTE_KEY, true);
        bool initialPollutionShowVehicle = config.GetBool(POLLUTION_SHOW_VEHICLE_KEY, true);
        bool initialPollutionShowShip = config.GetBool(POLLUTION_SHOW_SHIP_KEY, true);
        bool initialRadiationOverlayEnabled = config.GetBool(RADIATION_OVERLAY_ENABLED_KEY, false);
        bool initialRadiationGlowEnabled = config.GetBool(RADIATION_GLOW_ENABLED_KEY, false);
        int initialRadiationDaysToAverage = config.GetInt(RADIATION_DAYS_TO_AVERAGE_KEY, 30);
        bool initialLayoutBoxModeEnabled = config.GetBool(LAYOUT_BOX_MODE_ENABLED_KEY, false);
        bool initialUseRecycleBin = config.GetBool(USE_RECYCLE_BIN_KEY, true);
        string initialRecycleBinFolderName = config.GetString(RECYCLE_BIN_FOLDER_NAME_KEY, "Recycle Bin");
        int initialBlueprintSpacing = config.GetInt(BLUEPRINT_SPACING_KEY, 6);
        HeightFilterTransportVisibility initialTransportVisibility = TransportVisibilityFromInt(config.GetInt(HEIGHT_FILTER_TRANSPORT_VISIBILITY_KEY, (int)HeightFilterTransportVisibility.Medium));
        HeightFilterPillarVisibility initialPillarVisibility = PillarVisibilityFromInt(config.GetInt(HEIGHT_FILTER_PILLAR_VISIBILITY_KEY, (int)HeightFilterPillarVisibility.Detached));

        LoadFromStore();
    }

    public static void LoadFromStore()
    {
        if (s_store == null || s_config == null)
            return;

        MarkdownTableLanguage initialLanguage = FromInt(s_config.GetInt(MARKDOWN_TABLE_LANGUAGE_KEY, 0));
        MarkdownNumberFormat initialNumberFormat = NumberFormatFromInt(s_config.GetInt(MARKDOWN_NUMBER_FORMAT_KEY, 0));
        bool initialInstantBuildMode = s_config.GetBool(INSTANT_BUILD_MODE_KEY, false);
        bool initialLegacyBeltConfigurations = s_config.GetBool(LEGACY_BELT_CONFIGURATIONS_KEY, true);
        bool initialThroughputOverlayEnabled = s_config.GetBool(THROUGHPUT_OVERLAY_ENABLED_KEY, true);
        bool initialThroughputGlowEnabled = s_config.GetBool(THROUGHPUT_GLOW_ENABLED_KEY, true);
        ThroughputHeatmapMode initialThroughputHeatmapMode = HeatmapModeFromInt(s_config.GetInt(THROUGHPUT_HEATMAP_MODE_KEY, (int)ThroughputHeatmapMode.Capacity));
        bool initialThroughputColorblindMode = s_config.GetBool(THROUGHPUT_COLORBLIND_MODE_KEY, false);
        bool initialThroughputShowAsPercent = s_config.GetBool(THROUGHPUT_SHOW_AS_PERCENT_KEY, false);
        bool initialPollutionOverlayEnabled = s_config.GetBool(POLLUTION_OVERLAY_ENABLED_KEY, false);
        bool initialPollutionGlowEnabled = s_config.GetBool(POLLUTION_GLOW_ENABLED_KEY, false);
        ColorRgba initialPollutionGlowColor = ReadPollutionGlowColor(
            s_config.GetString(POLLUTION_GLOW_COLOR_KEY, "white"));
        int initialPollutionDaysToAverage = s_config.GetInt(POLLUTION_DAYS_TO_AVERAGE_KEY, 360);
        bool initialPollutionShowAir = s_config.GetBool(POLLUTION_SHOW_AIR_KEY, true);
        bool initialPollutionShowGround = s_config.GetBool(POLLUTION_SHOW_GROUND_KEY, true);
        bool initialPollutionShowSolidWaste = s_config.GetBool(POLLUTION_SHOW_SOLID_WASTE_KEY, true);
        bool initialPollutionShowVehicle = s_config.GetBool(POLLUTION_SHOW_VEHICLE_KEY, true);
        bool initialPollutionShowShip = s_config.GetBool(POLLUTION_SHOW_SHIP_KEY, true);
        bool initialRadiationOverlayEnabled = s_config.GetBool(RADIATION_OVERLAY_ENABLED_KEY, false);
        bool initialRadiationGlowEnabled = s_config.GetBool(RADIATION_GLOW_ENABLED_KEY, false);
        int initialRadiationDaysToAverage = s_config.GetInt(RADIATION_DAYS_TO_AVERAGE_KEY, 30);
        bool initialLayoutBoxModeEnabled = s_config.GetBool(LAYOUT_BOX_MODE_ENABLED_KEY, false);
        bool initialUseRecycleBin = s_config.GetBool(USE_RECYCLE_BIN_KEY, true);
        string initialRecycleBinFolderName = s_config.GetString(RECYCLE_BIN_FOLDER_NAME_KEY, "Recycle Bin");
        int initialBlueprintSpacing = s_config.GetInt(BLUEPRINT_SPACING_KEY, 6);
        bool initialPreColorPipesEnabled = s_config.GetBool(PRE_COLOR_PIPES_KEY, true);
        HeightFilterTransportVisibility initialTransportVisibility = TransportVisibilityFromInt(s_config.GetInt(HEIGHT_FILTER_TRANSPORT_VISIBILITY_KEY, (int)HeightFilterTransportVisibility.Medium));
        HeightFilterPillarVisibility initialPillarVisibility = PillarVisibilityFromInt(s_config.GetInt(HEIGHT_FILTER_PILLAR_VISIBILITY_KEY, (int)HeightFilterPillarVisibility.Detached));

        LoadFromJsonStore(
            s_store,
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
            initialPollutionGlowColor,
            initialPollutionDaysToAverage,
            initialPollutionShowAir,
            initialPollutionShowGround,
            initialPollutionShowSolidWaste,
            initialPollutionShowVehicle,
            initialPollutionShowShip,
            initialRadiationOverlayEnabled,
            initialRadiationGlowEnabled,
            initialRadiationDaysToAverage,
            initialLayoutBoxModeEnabled,
            initialUseRecycleBin,
            initialRecycleBinFolderName,
            initialBlueprintSpacing,
            initialPreColorPipesEnabled,
            initialTransportVisibility,
            initialPillarVisibility);
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
                .SetOptions(
                    MarkdownTableLanguage.English,
                    MarkdownTableLanguage.Local,
                    MarkdownTableLanguage.Both,
                    MarkdownTableLanguage.Hybrid)
                .SetValue(MarkdownTableLanguage)
                .OnValueChanged((language, _) => SetMarkdownTableLanguage(language));
        languageDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsMarkdownTableLanguage.AsFormatted,
            new LocStrFormatted(
                BdtLocalization.SettingsMarkdownTableLanguageDescription.TranslatedString
                + "\n\n"
                + BdtLocalization.SettingsMarkdownTableLanguagePending.TranslatedString),
            languageDropdown));

        Dropdown<MarkdownNumberFormat> numberFormatDropdown =
            new Dropdown<MarkdownNumberFormat>(NumberFormatDropdownOption)
                .SetOptions(
                    MarkdownNumberFormat.Auto,
                    MarkdownNumberFormat.English,
                    MarkdownNumberFormat.Local)
                .SetValue(MarkdownNumberFormat)
                .OnValueChanged((numberFormat, _) => SetMarkdownNumberFormat(numberFormat));
        numberFormatDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsMarkdownNumberFormat.AsFormatted,
            BdtLocalization.SettingsMarkdownNumberFormatDescription.AsFormatted,
            numberFormatDropdown));
        root.Add(new Title(BdtLocalization.SettingsBuildBehaviorsHeading.AsFormatted)
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

        Toggle preColorPipesToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPreColorPipes.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPreColorPipesDescription.AsFormatted)
            .Value(PreColorPipesEnabled)
            .OnValueChanged(SetPreColorPipesEnabled);
        root.Add(preColorPipesToggle);

        Toggle legacyBeltConfigurationsToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsLegacyBeltConfigurations.AsFormatted)
            .Tooltip(BdtLocalization.SettingsLegacyBeltConfigurationsDescription.AsFormatted)
            .Value(LegacyBeltConfigurationsEnabled)
            .OnValueChanged(SetLegacyBeltConfigurations);
        root.Add(legacyBeltConfigurationsToggle);

        root.Add(new Title(BdtLocalization.SettingsHeightFilterHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Dropdown<int> heightFilterDropdown = new Dropdown<int>(HeightFilterDropdownOption)
            .SetOptions(0, 1, 2, 3, 4, 5, 6)
            .SetValue(HeightFilterMaxVisibleLevel)
            .OnValueChanged((level, _) => SetHeightFilterMaxVisibleLevel(level));
        heightFilterDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsHeightFilterMaxVisible.AsFormatted,
            new LocStrFormatted(
                BdtLocalization.SettingsHeightFilterMaxVisibleDescription.TranslatedString
                + "\n\n"
                + BdtLocalization.SettingsHeightFilterControlsTooltip.TranslatedString),
            heightFilterDropdown,
            row => AddDualHotkeyBadges(row, HotkeysRegistry.HeightFilterHideLayer, HotkeysRegistry.HeightFilterShowLayer)));

        Dropdown<HeightFilterTransportVisibility> transportVisibilityDropdown =
            new Dropdown<HeightFilterTransportVisibility>(TransportVisibilityDropdownOption)
                .SetOptions(
                    HeightFilterTransportVisibility.Low,
                    HeightFilterTransportVisibility.Medium,
                    HeightFilterTransportVisibility.High)
                .SetValue(HeightFilterTransportVisibility)
                .OnValueChanged((mode, _) => SetHeightFilterTransportVisibility(mode));
        transportVisibilityDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsHeightFilterTransportVisibility.AsFormatted,
            new LocStrFormatted(
                BdtLocalization.SettingsHeightFilterTransportVisibilityDescription.TranslatedString
                + "\n\n"
                + BdtLocalization.SettingsHeightFilterTransportVisibilityTooltip.TranslatedString
                + "\n\n"
                + BdtLocalization.SettingsHeightFilterControlsTooltip.TranslatedString),
            transportVisibilityDropdown,
            row => AddDualHotkeyBadges(row, HotkeysRegistry.HeightFilterTransportVisibilityLow, HotkeysRegistry.HeightFilterTransportVisibilityHigh)));

        Dropdown<HeightFilterPillarVisibility> pillarVisibilityDropdown =
            new Dropdown<HeightFilterPillarVisibility>(PillarVisibilityDropdownOption)
                .SetOptions(
                    HeightFilterPillarVisibility.Detached,
                    HeightFilterPillarVisibility.Attached,
                    HeightFilterPillarVisibility.Top,
                    HeightFilterPillarVisibility.Off)
                .SetValue(HeightFilterPillarVisibility)
                .OnValueChanged((mode, _) => SetHeightFilterPillarVisibility(mode));
        pillarVisibilityDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsHeightFilterPillarVisibility.AsFormatted,
            BdtLocalization.SettingsHeightFilterPillarVisibilityDescription.AsFormatted,
            pillarVisibilityDropdown));

        root.Add(new Title(BdtLocalization.SettingsThroughputHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle throughputOverlayToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputToggle.AsFormatted)
            .Tooltip(new LocStrFormatted(BdtLocalization.SettingsThroughputToggleDescription.TranslatedString + "\n\n" + BdtLocalization.SettingsGlobalHotkeyTooltip.TranslatedString))
            .Value(ThroughputOverlayEnabled)
            .OnValueChanged(SetThroughputOverlayEnabled);

        var throughputOverlayRow = new Row().AlignItemsCenter();
        throughputOverlayRow.Add(throughputOverlayToggle);
        AddHotkeyBadges(throughputOverlayRow, HotkeysRegistry.ThroughputOverlayToggle);
        root.Add(throughputOverlayRow);

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
                .SetOptions(
                    ThroughputHeatmapMode.None,
                    ThroughputHeatmapMode.Relative,
                    ThroughputHeatmapMode.Capacity)
                .SetValue(ThroughputHeatmapMode)
                .OnValueChanged((mode, _) => {
                    SetThroughputHeatmapMode(mode);
                    colorblindToggle.Enabled(mode != ThroughputHeatmapMode.None);
                });
        heatmapDropdown.Width(SETTINGS_CONTROL_WIDTH);

        root.Add(BuildControlRow(
            BdtLocalization.SettingsThroughputHeatmap.AsFormatted,
            null,
            heatmapDropdown));
        root.Add(colorblindToggle);

        Toggle showAsPercentToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsThroughputShowAsPercent.AsFormatted)
            .Tooltip(BdtLocalization.SettingsThroughputShowAsPercentDescription.AsFormatted)
            .Value(ThroughputShowAsPercent)
            .OnValueChanged(SetThroughputShowAsPercent);
        root.Add(showAsPercentToggle);

        root.Add(new Title(BdtLocalization.SettingsPollutionHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle pollutionOverlayToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionToggle.AsFormatted)
            .Tooltip(new LocStrFormatted(BdtLocalization.SettingsPollutionToggleDescription.TranslatedString + "\n\n" + BdtLocalization.SettingsGlobalHotkeyTooltip.TranslatedString))
            .Value(PollutionOverlayEnabled)
            .OnValueChanged(SetPollutionOverlayEnabled);

        var pollutionOverlayRow = new Row().AlignItemsCenter();
        pollutionOverlayRow.Add(pollutionOverlayToggle);
        AddHotkeyBadges(pollutionOverlayRow, HotkeysRegistry.PollutionOverlayToggle);
        root.Add(pollutionOverlayRow);

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

        Toggle showSolidWasteToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsPollutionShowSolidWaste.AsFormatted)
            .Tooltip(BdtLocalization.SettingsPollutionShowSolidWasteDescription.AsFormatted)
            .Value(PollutionShowSolidWaste)
            .OnValueChanged(SetPollutionShowSolidWaste);
        root.Add(showSolidWasteToggle);

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

        root.Add(new Title(BdtLocalization.SettingsRadiationHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle radiationOverlayToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsRadiationToggle.AsFormatted)
            .Tooltip(new LocStrFormatted(BdtLocalization.SettingsRadiationToggleDescription.TranslatedString + "\n\n" + BdtLocalization.SettingsGlobalHotkeyTooltip.TranslatedString))
            .Value(RadiationOverlayEnabled)
            .OnValueChanged(SetRadiationOverlayEnabled);

        var radiationOverlayRow = new Row().AlignItemsCenter();
        radiationOverlayRow.Add(radiationOverlayToggle);
        AddHotkeyBadges(radiationOverlayRow, HotkeysRegistry.RadiationOverlayToggle);
        root.Add(radiationOverlayRow);

        Toggle radiationGlowToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsRadiationGlow.AsFormatted)
            .Tooltip(BdtLocalization.SettingsRadiationGlowDescription.AsFormatted)
            .Value(RadiationGlowEnabled)
            .OnValueChanged(SetRadiationGlowEnabled);
        root.Add(radiationGlowToggle);

        // --- RADIATION DAYS TO AVERAGE ---
        var radiationDaysRow = new Row(2.pt()).AlignItemsCenter();
        var radiationDaysLabel = new Label(BdtLocalization.SettingsRadiationDaysToAverage.AsFormatted)
            .Tooltip(BdtLocalization.SettingsRadiationDaysToAverageDescription.AsFormatted)
            .Width(SETTINGS_LABEL_WIDTH);
        radiationDaysRow.Add(radiationDaysLabel);

        var radiationDaysSpacer = new UiComponent().FlexGrow(1f);
        radiationDaysRow.Add(radiationDaysSpacer);

        var radiationDaysControlRow = new Row(2.pt()).AlignItemsCenter();

        var radiationDaysMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var radiationDaysPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        TextField radiationDaysInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(45.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(radiationDaysInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        radiationDaysInput.Text(RadiationDaysToAverage.ToString());

        radiationDaysControlRow.Add(radiationDaysMinusBtn);
        radiationDaysControlRow.Add(radiationDaysInput);
        radiationDaysControlRow.Add(radiationDaysPlusBtn);
        radiationDaysRow.Add(radiationDaysControlRow);
        root.Add(radiationDaysRow);

        Action<int> updateRadiationDays = (val) =>
        {
            SetRadiationDaysToAverage(val);
            radiationDaysInput.Text(RadiationDaysToAverage.ToString());
        };

        radiationDaysInput.OnValueChanged((text) =>
        {
            if (int.TryParse(text, out int val))
            {
                updateRadiationDays(val);
            }
        });

        Action<int> adjustRadiationDays = (sign) =>
        {
            if (int.TryParse(radiationDaysInput.GetText(), out int current))
            {
                int step = 1;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 10;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 5;

                int next = Math.Max(0, Math.Min(360, current + sign * step));
                updateRadiationDays(next);
            }
        };

        radiationDaysMinusBtn.OnClick(() => adjustRadiationDays(-1), allowKeyPresses: true);
        radiationDaysPlusBtn.OnClick(() => adjustRadiationDays(1), allowKeyPresses: true);

        root.Add(new Title(BdtLocalization.SettingsLayoutBoxModeHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Toggle layoutBoxModeToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.SettingsLayoutBoxModeToggle.AsFormatted)
            .Tooltip(new LocStrFormatted(BdtLocalization.SettingsLayoutBoxModeDescription.TranslatedString + "\n\n" + BdtLocalization.SettingsGlobalHotkeyTooltip.TranslatedString))
            .Value(LayoutBoxModeEnabled)
            .OnValueChanged(SetLayoutBoxModeEnabled);

        var layoutBoxRow = new Row().AlignItemsCenter();
        layoutBoxRow.Add(layoutBoxModeToggle);
        AddHotkeyBadges(layoutBoxRow, HotkeysRegistry.LayoutBoxModeToggle);

        root.Add(layoutBoxRow);


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
            .CharLimit(60)
            .Text(RecycleBinFolderName)
            .Width(SETTINGS_CONTROL_WIDTH)
            .OnEditEnd(name => {
                bool isValid = !string.IsNullOrWhiteSpace(name) && name.Length <= 60;
                if (isValid)
                {
                    SetRecycleBinFolderName(name);
                }
                recycleBinFolderNameField!.MarkAsError(!isValid, BdtLocalization.SettingsRecycleBinFolderNameInvalid.AsFormatted);
            });

        root.Add(BuildControlRow(
            BdtLocalization.SettingsRecycleBinFolderName.AsFormatted,
            BdtLocalization.SettingsRecycleBinFolderNameDescription.AsFormatted,
            recycleBinFolderNameField));

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

        root.Add(BuildFooter(() =>
        {
            languageDropdown.SetValue(MarkdownTableLanguage);
            numberFormatDropdown.SetValue(MarkdownNumberFormat);
            instantBuildToggle.Value(InstantBuildModeEnabled);
            legacyBeltConfigurationsToggle.Value(LegacyBeltConfigurationsEnabled);
            preColorPipesToggle.Value(PreColorPipesEnabled);
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
            showSolidWasteToggle.Value(PollutionShowSolidWaste);
            showVehicleToggle.Value(PollutionShowVehicle);
            showShipToggle.Value(PollutionShowShip);
            radiationOverlayToggle.Value(RadiationOverlayEnabled);
            radiationGlowToggle.Value(RadiationGlowEnabled);
            radiationDaysInput.Text(RadiationDaysToAverage.ToString());
            layoutBoxModeToggle.Value(LayoutBoxModeEnabled);
            heightFilterDropdown.SetValue(HeightFilterMaxVisibleLevel);
            transportVisibilityDropdown.SetValue(HeightFilterTransportVisibility);
            pillarVisibilityDropdown.SetValue(HeightFilterPillarVisibility);
            recycleBinToggle.Value(UseRecycleBin);
            recycleBinFolderNameField.Text(RecycleBinFolderName);
            recycleBinFolderNameField.MarkAsError(false);
            spacingInput.Text(BlueprintSpacing.ToString());
//             layoutBoxModePrimaryField.Refresh();
//             layoutBoxModeSecondaryField.Refresh();
        }));

        return root;
    }

    private static Row BuildControlRow(LocStrFormatted label, LocStrFormatted? tooltip, UiComponent control, Action<Row>? addBadges = null)
    {
        var row = new Row().AlignItemsCenter();
        var labelComp = new Label(label);
        if (tooltip.HasValue)
        {
            labelComp.Tooltip(tooltip.Value);
        }
        row.Add(labelComp);
        addBadges?.Invoke(row);
        row.Add(new UiComponent().FlexGrow(1f));
        row.Add(control);
        return row;
    }

    private static void AddDualHotkeyBadges(Row row, KeyBindings first, KeyBindings second)
    {
        AddHotkeyBadges(row, first);
        row.Add(new Label("/".AsLoc())
            .Color(Mafi.Unity.UiToolkit.Theme.InactiveColor)
            .MarginLeft(3.pt()));
        AddHotkeyBadges(row, second, marginLeft: 3.pt());
    }



    private static void AddHotkeyBadges(Row row, KeyBindings bindings, Px? marginLeft = null)
    {
        Px primaryMargin = marginLeft ?? 6.pt();
        if (!bindings.Primary.IsEmpty)
        {
            row.Add(new KeyBindUi().SetKeys(bindings.Primary.Keys.ToArray()).MarginLeft(primaryMargin));
        }
        if (!bindings.Primary.IsEmpty && !bindings.Secondary.IsEmpty)
        {
            row.Add(new Label(BdtLocalization.SettingsHotkeyOr.AsFormatted)
                .Color(Mafi.Unity.UiToolkit.Theme.InactiveColor)
                .MarginLeft(4.pt()));
        }
        if (!bindings.Secondary.IsEmpty)
        {
            row.Add(new KeyBindUi().SetKeys(bindings.Secondary.Keys.ToArray()).MarginLeft(4.pt()));
        }
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
            SetPreColorPipesEnabled(true);
            SetHeightFilterMaxVisibleLevel(6);
            SetHeightFilterTransportVisibility(HeightFilterTransportVisibility.Medium);
            SetHeightFilterPillarVisibility(HeightFilterPillarVisibility.Detached);
            SetThroughputOverlayEnabled(true);
            SetThroughputGlowEnabled(true);
            SetThroughputHeatmapMode(ThroughputHeatmapMode.Capacity);
            SetThroughputColorblindMode(false);
            SetThroughputShowAsPercent(false);
            SetPollutionOverlayEnabled(false);
            SetPollutionGlowEnabled(false);
            PollutionGlowColor = ColorRgba.White;
            SetPollutionDaysToAverage(30);
            SetPollutionShowAir(true);
            SetPollutionShowGround(true);
            SetPollutionShowSolidWaste(true);
            SetPollutionShowVehicle(true);
            SetPollutionShowShip(true);
            SetRadiationOverlayEnabled(false);
            SetRadiationGlowEnabled(false);
            SetRadiationDaysToAverage(30);
            SetUseRecycleBin(true);
            SetRecycleBinFolderName("Recycle Bin");
            SetBlueprintSpacing(6);
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
            if (s_config != null && !s_config.TrySetValue(PRE_COLOR_PIPES_KEY, PreColorPipesEnabled, out error))
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
            if (s_config != null && !s_config.TrySetValue(POLLUTION_GLOW_COLOR_KEY, FormatPollutionGlowColor(), out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_DAYS_TO_AVERAGE_KEY, PollutionDaysToAverage, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_AIR_KEY, PollutionShowAir, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_GROUND_KEY, PollutionShowGround, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_SOLID_WASTE_KEY, PollutionShowSolidWaste, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_VEHICLE_KEY, PollutionShowVehicle, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(POLLUTION_SHOW_SHIP_KEY, PollutionShowShip, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(RADIATION_OVERLAY_ENABLED_KEY, RadiationOverlayEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(RADIATION_GLOW_ENABLED_KEY, RadiationGlowEnabled, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(RADIATION_DAYS_TO_AVERAGE_KEY, RadiationDaysToAverage, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(USE_RECYCLE_BIN_KEY, UseRecycleBin, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(RECYCLE_BIN_FOLDER_NAME_KEY, RecycleBinFolderName, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(BLUEPRINT_SPACING_KEY, BlueprintSpacing, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(HEIGHT_FILTER_TRANSPORT_VISIBILITY_KEY, (int)HeightFilterTransportVisibility, out error))
                return false;
            if (s_config != null && !s_config.TrySetValue(HEIGHT_FILTER_PILLAR_VISIBILITY_KEY, (int)HeightFilterPillarVisibility, out error))
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
            updated = TryReplaceConfigDefault(updated, PRE_COLOR_PIPES_KEY, PreColorPipesEnabled, out bool preColorPipesUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_OVERLAY_ENABLED_KEY, ThroughputOverlayEnabled, out bool throughputOverlayEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_GLOW_ENABLED_KEY, ThroughputGlowEnabled, out bool throughputGlowEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_HEATMAP_MODE_KEY, (int)ThroughputHeatmapMode, out bool throughputHeatmapModeUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_COLORBLIND_MODE_KEY, ThroughputColorblindMode, out bool throughputColorblindModeUpdated);
            updated = TryReplaceConfigDefault(updated, THROUGHPUT_SHOW_AS_PERCENT_KEY, ThroughputShowAsPercent, out bool throughputShowAsPercentUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_OVERLAY_ENABLED_KEY, PollutionOverlayEnabled, out bool pollutionOverlayEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_GLOW_ENABLED_KEY, PollutionGlowEnabled, out bool pollutionGlowEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_GLOW_COLOR_KEY, FormatPollutionGlowColor(), out bool pollutionGlowColorUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_DAYS_TO_AVERAGE_KEY, PollutionDaysToAverage, out bool pollutionDaysToAverageUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_AIR_KEY, PollutionShowAir, out bool pollutionShowAirUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_GROUND_KEY, PollutionShowGround, out bool pollutionShowGroundUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_SOLID_WASTE_KEY, PollutionShowSolidWaste, out bool pollutionShowSolidWasteUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_VEHICLE_KEY, PollutionShowVehicle, out bool pollutionShowVehicleUpdated);
            updated = TryReplaceConfigDefault(updated, POLLUTION_SHOW_SHIP_KEY, PollutionShowShip, out bool pollutionShowShipUpdated);
            updated = TryReplaceConfigDefault(updated, RADIATION_OVERLAY_ENABLED_KEY, RadiationOverlayEnabled, out bool radiationOverlayEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, RADIATION_GLOW_ENABLED_KEY, RadiationGlowEnabled, out bool radiationGlowEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, RADIATION_DAYS_TO_AVERAGE_KEY, RadiationDaysToAverage, out bool radiationDaysToAverageUpdated);
            updated = TryReplaceConfigDefault(updated, LAYOUT_BOX_MODE_ENABLED_KEY, LayoutBoxModeEnabled, out bool layoutBoxModeEnabledUpdated);
            updated = TryReplaceConfigDefault(updated, USE_RECYCLE_BIN_KEY, UseRecycleBin, out bool useRbUpdated);
            updated = TryReplaceConfigDefault(updated, RECYCLE_BIN_FOLDER_NAME_KEY, RecycleBinFolderName, out bool rbNameUpdated);
            updated = TryReplaceConfigDefault(updated, BLUEPRINT_SPACING_KEY, BlueprintSpacing, out bool blueprintSpacingUpdated);
            updated = TryReplaceConfigDefault(updated, HEIGHT_FILTER_TRANSPORT_VISIBILITY_KEY, (int)HeightFilterTransportVisibility, out bool transportVisibilityUpdated);
            updated = TryReplaceConfigDefault(updated, HEIGHT_FILTER_PILLAR_VISIBILITY_KEY, (int)HeightFilterPillarVisibility, out bool pillarVisibilityUpdated);

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
            if (!preColorPipesUpdated)
            {
                error = "Could not find pre_color_pipes default in config.json.";
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
            if (!pollutionGlowColorUpdated)
            {
                error = "Could not find pollution_glow_color default in config.json.";
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
            if (!radiationOverlayEnabledUpdated)
            {
                error = "Could not find radiation_overlay_enabled default in config.json.";
                return false;
            }
            if (!radiationGlowEnabledUpdated)
            {
                error = "Could not find radiation_glow_enabled default in config.json.";
                return false;
            }
            if (!radiationDaysToAverageUpdated)
            {
                error = "Could not find radiation_days_to_average default in config.json.";
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
            if (!transportVisibilityUpdated)
            {
                error = "Could not find height_filter_transport_visibility default in config.json.";
                return false;
            }
            if (!pillarVisibilityUpdated)
            {
                error = "Could not find height_filter_pillar_visibility default in config.json.";
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

    private static void SetPreColorPipesEnabled(bool enabled)
    {
        if (PreColorPipesEnabled == enabled)
            return;

        PreColorPipesEnabled = enabled;
        try { PreColorPipesEnabledChanged?.Invoke(enabled); }
        catch (Exception ex) { s_log.Warning($"Pre-color pipes change handler failed: {ex.Message}"); }
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

    public static void SetHeightFilterTransportVisibility(HeightFilterTransportVisibility mode)
    {
        if (HeightFilterTransportVisibility == mode)
            return;

        HeightFilterTransportVisibility = mode;
        try { HeightFilterTransportVisibilityChanged?.Invoke(mode); }
        catch (Exception ex) { s_log.Warning($"Height filter transport visibility change handler failed: {ex.Message}"); }
    }

    public static void SetHeightFilterPillarVisibility(HeightFilterPillarVisibility mode)
    {
        if (HeightFilterPillarVisibility == mode)
            return;

        HeightFilterPillarVisibility = mode;
        try { HeightFilterPillarVisibilityChanged?.Invoke(mode); }
        catch (Exception ex) { s_log.Warning($"Height filter pillar visibility change handler failed: {ex.Message}"); }
    }

    private static UiComponent HeightFilterDropdownOption(int level, int index, bool isInDropdown)
    {
        string labelText = level == 6 ? BdtLocalization.SettingsHeightFilterAll.TranslatedString : level.ToString();
        return new Label(labelText.AsLoc());
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
        ColorRgba initialPollutionGlowColor,
        int initialPollutionDaysToAverage,
        bool initialPollutionShowAir,
        bool initialPollutionShowGround,
        bool initialPollutionShowSolidWaste,
        bool initialPollutionShowVehicle,
        bool initialPollutionShowShip,
        bool initialRadiationOverlayEnabled,
        bool initialRadiationGlowEnabled,
        int initialRadiationDaysToAverage,
        bool initialLayoutBoxModeEnabled,
        bool initialUseRecycleBin,
        string initialRecycleBinFolderName,
        int initialBlueprintSpacing,
        bool initialPreColorPipesEnabled,
        HeightFilterTransportVisibility initialTransportVisibility,
        HeightFilterPillarVisibility initialPillarVisibility)
    {
        MarkdownTableLanguage = initialLanguage;
        MarkdownNumberFormat = initialNumberFormat;
        InstantBuildModeEnabled = initialInstantBuildMode;
        LegacyBeltConfigurationsEnabled = initialLegacyBeltConfigurations;
        HeightFilterTransportVisibility = initialTransportVisibility;
        HeightFilterPillarVisibility = initialPillarVisibility;
        ThroughputOverlayEnabled = initialThroughputOverlayEnabled;
        ThroughputGlowEnabled = initialThroughputGlowEnabled;
        ThroughputHeatmapMode = initialThroughputHeatmapMode;
        ThroughputColorblindMode = initialThroughputColorblindMode;
        ThroughputShowAsPercent = initialThroughputShowAsPercent;
        PollutionOverlayEnabled = initialPollutionOverlayEnabled;
        PollutionGlowEnabled = initialPollutionGlowEnabled;
        PollutionGlowColor = initialPollutionGlowColor;
        PollutionDaysToAverage = initialPollutionDaysToAverage;
        PollutionShowAir = initialPollutionShowAir;
        PollutionShowGround = initialPollutionShowGround;
        PollutionShowSolidWaste = initialPollutionShowSolidWaste;
        PollutionShowVehicle = initialPollutionShowVehicle;
        PollutionShowShip = initialPollutionShowShip;
        RadiationOverlayEnabled = initialRadiationOverlayEnabled;
        RadiationGlowEnabled = initialRadiationGlowEnabled;
        RadiationDaysToAverage = Math.Max(0, Math.Min(360, initialRadiationDaysToAverage));
        LayoutBoxModeEnabled = initialLayoutBoxModeEnabled;
        UseRecycleBin = initialUseRecycleBin;
        RecycleBinFolderName = initialRecycleBinFolderName;
        BlueprintSpacing = initialBlueprintSpacing;
        PreColorPipesEnabled = initialPreColorPipesEnabled;

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
            if (TryGetString(root, "pollutionGlowColor", out string pollutionGlowColor))
            {
                if (TryParsePollutionGlowColor(pollutionGlowColor, out ColorRgba parsedPollutionGlowColor))
                    PollutionGlowColor = parsedPollutionGlowColor;
                else
                    s_log.Warning(
                        $"Invalid pollutionGlowColor '{pollutionGlowColor}' in saved BDT settings. "
                        + "Using the configured default color.");
            }
            if (TryGetInt(root, "pollutionDaysToAverage", out int pollutionDaysToAverage))
                PollutionDaysToAverage = pollutionDaysToAverage;
            if (TryGetBool(root, "pollutionShowAir", out bool pollutionShowAir))
                PollutionShowAir = pollutionShowAir;
            if (TryGetBool(root, "pollutionShowGround", out bool pollutionShowGround))
                PollutionShowGround = pollutionShowGround;
            if (TryGetBool(root, "pollutionShowSolidWaste", out bool pollutionShowSolidWaste))
                PollutionShowSolidWaste = pollutionShowSolidWaste;
            if (TryGetBool(root, "pollutionShowVehicle", out bool pollutionShowVehicle))
                PollutionShowVehicle = pollutionShowVehicle;
            if (TryGetBool(root, "pollutionShowShip", out bool pollutionShowShip))
                PollutionShowShip = pollutionShowShip;
            if (TryGetBool(root, "radiationOverlayEnabled", out bool radiationOverlayEnabled))
                RadiationOverlayEnabled = radiationOverlayEnabled;
            if (TryGetBool(root, "radiationGlowEnabled", out bool radiationGlowEnabled))
                RadiationGlowEnabled = radiationGlowEnabled;
            if (TryGetInt(root, "radiationDaysToAverage", out int radiationDaysToAverage))
                RadiationDaysToAverage = Math.Max(0, Math.Min(360, radiationDaysToAverage));
            if (TryGetBool(root, "layoutBoxModeEnabled", out bool layoutBoxModeEnabled))
                LayoutBoxModeEnabled = layoutBoxModeEnabled;
            if (TryGetBool(root, "useRecycleBin", out bool useRecycleBin))
                UseRecycleBin = useRecycleBin;
            if (TryGetString(root, "recycleBinFolderName", out string recycleBinFolderName))
                RecycleBinFolderName = recycleBinFolderName;
            if (TryGetInt(root, "blueprintSpacing", out int blueprintSpacing))
                BlueprintSpacing = blueprintSpacing;
            if (TryGetBool(root, "preColorPipesEnabled", out bool preColorPipesEnabled))
                PreColorPipesEnabled = preColorPipesEnabled;
            if (TryGetInt(root, "heightFilterTransportVisibility", out int transportVisibility))
                HeightFilterTransportVisibility = TransportVisibilityFromInt(transportVisibility);
            if (TryGetInt(root, "heightFilterPillarVisibility", out int pillarVisibility))
                HeightFilterPillarVisibility = PillarVisibilityFromInt(pillarVisibility);
            if (TryGetBool(root, "isFirstUnpausePending", out bool firstUnpausePending))
                IsFirstUnpausePending = firstUnpausePending;
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
        writer.AppendStringField("pollutionGlowColor", FormatPollutionGlowColor());
        writer.AppendNumberField("pollutionDaysToAverage", PollutionDaysToAverage);
        writer.AppendBoolField("pollutionShowAir", PollutionShowAir);
        writer.AppendBoolField("pollutionShowGround", PollutionShowGround);
        writer.AppendBoolField("pollutionShowSolidWaste", PollutionShowSolidWaste);
        writer.AppendBoolField("pollutionShowVehicle", PollutionShowVehicle);
        writer.AppendBoolField("pollutionShowShip", PollutionShowShip);
        writer.AppendBoolField("radiationOverlayEnabled", RadiationOverlayEnabled);
        writer.AppendBoolField("radiationGlowEnabled", RadiationGlowEnabled);
        writer.AppendNumberField("radiationDaysToAverage", RadiationDaysToAverage);
        writer.AppendBoolField("layoutBoxModeEnabled", LayoutBoxModeEnabled);
        writer.AppendBoolField("useRecycleBin", UseRecycleBin);
        writer.AppendStringField("recycleBinFolderName", RecycleBinFolderName);
        writer.AppendNumberField("blueprintSpacing", BlueprintSpacing);
        writer.AppendBoolField("preColorPipesEnabled", PreColorPipesEnabled);
        writer.AppendNumberField("heightFilterTransportVisibility", (int)HeightFilterTransportVisibility);
        writer.AppendNumberField("heightFilterPillarVisibility", (int)HeightFilterPillarVisibility);
        writer.AppendBoolField("isFirstUnpausePending", IsFirstUnpausePending);
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
                return BdtLocalization.SettingsThroughputHeatmapRelative.AsFormatted;
            case ThroughputHeatmapMode.Capacity:
                return BdtLocalization.SettingsThroughputHeatmapCapacity.AsFormatted;
            default:
                return BdtLocalization.SettingsThroughputHeatmapNone.AsFormatted;
        }
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

    private sealed class TransportVisibilityOption
    {
        public readonly HeightFilterTransportVisibility Mode;
        public readonly LocStrFormatted Label;
        public readonly LocStrFormatted Tooltip;

        public TransportVisibilityOption(
            HeightFilterTransportVisibility mode,
            LocStrFormatted label,
            LocStrFormatted tooltip)
        {
            Mode = mode;
            Label = label;
            Tooltip = tooltip;
        }
    }

    private static readonly TransportVisibilityOption[] s_transportVisibilityOptions =
    {
        new TransportVisibilityOption(
            HeightFilterTransportVisibility.Low,
            BdtLocalization.SettingsHeightFilterTransportVisibilityLow.AsFormatted,
            BdtLocalization.SettingsHeightFilterTransportVisibilityLowTooltip.AsFormatted),
        new TransportVisibilityOption(
            HeightFilterTransportVisibility.Medium,
            BdtLocalization.SettingsHeightFilterTransportVisibilityMedium.AsFormatted,
            BdtLocalization.SettingsHeightFilterTransportVisibilityMediumTooltip.AsFormatted),
        new TransportVisibilityOption(
            HeightFilterTransportVisibility.High,
            BdtLocalization.SettingsHeightFilterTransportVisibilityHigh.AsFormatted,
            BdtLocalization.SettingsHeightFilterTransportVisibilityHighTooltip.AsFormatted),
    };

    private static TransportVisibilityOption GetTransportVisibilityOption(
        HeightFilterTransportVisibility mode)
    {
        int index = (int)mode;
        if (index < 0 || index >= s_transportVisibilityOptions.Length)
            index = (int)HeightFilterTransportVisibility.Medium;

        return s_transportVisibilityOptions[index];
    }

    private static HeightFilterTransportVisibility TransportVisibilityFromInt(int value)
    {
        return GetTransportVisibilityOption((HeightFilterTransportVisibility)value).Mode;
    }

    private static UiComponent TransportVisibilityDropdownOption(
        HeightFilterTransportVisibility mode,
        int index,
        bool isInDropdown)
    {
        TransportVisibilityOption option = GetTransportVisibilityOption(mode);
        var label = new Label(option.Label);
        label.Tooltip(option.Tooltip);
        return label;
    }

    private static HeightFilterPillarVisibility PillarVisibilityFromInt(int value)
    {
        switch (value)
        {
            case (int)HeightFilterPillarVisibility.Attached:
                return HeightFilterPillarVisibility.Attached;
            case (int)HeightFilterPillarVisibility.Top:
                return HeightFilterPillarVisibility.Top;
            case (int)HeightFilterPillarVisibility.Off:
                return HeightFilterPillarVisibility.Off;
            default:
                return HeightFilterPillarVisibility.Detached;
        }
    }

    private static UiComponent PillarVisibilityDropdownOption(
        HeightFilterPillarVisibility mode,
        int index,
        bool isInDropdown)
    {
        var label = new Label(PillarVisibilityLabel(mode));
        label.Tooltip(PillarVisibilityTooltip(mode));
        return label;
    }

    private static LocStrFormatted PillarVisibilityLabel(HeightFilterPillarVisibility mode)
    {
        switch (mode)
        {
            case HeightFilterPillarVisibility.Attached:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityAttached.AsFormatted;
            case HeightFilterPillarVisibility.Top:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityTop.AsFormatted;
            case HeightFilterPillarVisibility.Off:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityOff.AsFormatted;
            default:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityDetached.AsFormatted;
        }
    }

    private static LocStrFormatted PillarVisibilityTooltip(HeightFilterPillarVisibility mode)
    {
        switch (mode)
        {
            case HeightFilterPillarVisibility.Attached:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityAttachedTooltip.AsFormatted;
            case HeightFilterPillarVisibility.Top:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityTopTooltip.AsFormatted;
            case HeightFilterPillarVisibility.Off:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityOffTooltip.AsFormatted;
            default:
                return BdtLocalization.SettingsHeightFilterPillarVisibilityDetachedTooltip.AsFormatted;
        }
    }
}
