// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using Mafi.Localization;

namespace CoIDesignerToolkit;

internal static class BdtLocalization
{
    public static LocStr ModName =
        Loc.Str("dtk.mod.name", "Blueprint Designer's Toolkit", "Blueprint Designer's Toolkit mod name.");

    public static LocStr SettingsTabMarkdown =
        Loc.Str("dtk.settings.tab.markdown", "Settings", "Settings tab title for BDT settings.");
    public static LocStr SettingsMarkdownCopyHeading =
        Loc.Str("dtk.settings.markdown_copy.heading", "Markdown Copy", "Settings section heading for Markdown copy/export settings.");
    public static LocStr SettingsMarkdownTableLanguage =
        Loc.Str("dtk.settings.markdown_table_language.label", "Markdown table language", "Settings row label for Markdown table language.");
    public static LocStr SettingsMarkdownTableLanguageDescription =
        Loc.Str("dtk.settings.markdown_table_language.description", "Controls which language Markdown table headers and product/entity names should use.", "Settings row description for Markdown table language.");
    public static LocStr SettingsMarkdownTableLanguagePending =
        Loc.Str("dtk.settings.markdown_table_language.pending", "English, Local, Both, and Hybrid are wired into Markdown export.", "Settings note for Markdown table language implementation status.");
    public static LocStr SettingsMarkdownNumberFormat =
        Loc.Str("dtk.settings.markdown_number_format.label", "Markdown number format", "Settings row label for Markdown number formatting.");
    public static LocStr SettingsMarkdownNumberFormatDescription =
        Loc.Str("dtk.settings.markdown_number_format.description", "Controls decimal and thousands separators in Markdown exports. Auto follows the rendered table language: English tables use en-US separators and local tables use the current game locale.", "Settings row description for Markdown number formatting.");
    public static LocStr SettingsLanguageEnglish =
        Loc.Str("dtk.settings.language.english", "English", "Dropdown option for English Markdown table language.");
    public static LocStr SettingsLanguageLocal =
        Loc.Str("dtk.settings.language.local", "Local", "Dropdown option for local Markdown table language.");
    public static LocStr SettingsLanguageBoth =
        Loc.Str("dtk.settings.language.both", "Both", "Dropdown option for bilingual Markdown table language.");
    public static LocStr SettingsLanguageHybrid =
        Loc.Str("dtk.settings.language.hybrid", "Hybrid", "Dropdown option for hybrid Markdown table language.");
    public static LocStr SettingsNumberFormatAuto =
        Loc.Str("dtk.settings.number_format.auto", "Auto", "Dropdown option for automatic Markdown number formatting.");
    public static LocStr SettingsNumberFormatEnglish =
        Loc.Str("dtk.settings.number_format.english", "English separators", "Dropdown option for en-US Markdown number formatting.");
    public static LocStr SettingsNumberFormatLocal =
        Loc.Str("dtk.settings.number_format.local", "Local separators", "Dropdown option for local Markdown number formatting.");
    public static LocStr SettingsInstantBuildHeading =
        Loc.Str("dtk.settings.instant_build.heading", "INSTANT BUILD", "Settings section heading for automatic construction tools.");
    public static LocStr SettingsInstantBuildMode =
        Loc.Str("dtk.settings.instant_build_mode.label", "Instant build mode", "Settings row label for instant build mode.");
    public static LocStr SettingsInstantBuildModeDescription =
        Loc.Str("dtk.settings.instant_build_mode.description", "Automatically and instantly completes construction, deconstruction, upgrades, and downgrades without materials, workers, or unity (even while paused). Enabling this also turns off the game's insta-build toggle.", "Settings row description for instant build mode.");
    public static LocStr SettingsInstantBuildModeSandboxOnly =
        Loc.Str("dtk.settings.instant_build_mode.sandbox_only", "Sod off to the sandbox, you lazy git!", "Tooltip for instant build mode when the game is not in sandbox mode.");
    public static LocStr SettingsLegacyBeltConfigurations =
        Loc.Str("dtk.settings.legacy_belt_configurations.label", "Allow curvy incline belts", "Settings row label for allowing legacy belt configurations (curvy incline belts).");
    public static LocStr SettingsLegacyBeltConfigurationsDescription =
        Loc.Str("dtk.settings.legacy_belt_configurations.description", "Enables Update 1 style transport construction, allowing transports to turn and incline/decline on the same tile (making curvy incline belts possible to construct directly).", "Settings row description for allowing legacy belt configurations (curvy incline belts).");

    public static LocStr RateLimitTitle =
        Loc.Str("dtk.rate_limit.title", "Throughput Limiter", "Panel title for the throughput limiter inspector.");
    public static LocStr RateLimitEnable =
        Loc.Str("dtk.rate_limit.enable", "Limit throughput", "Checkbox label to enable the throughput limiter.");
    public static LocStr RateLimitSandboxOnly =
        Loc.Str("dtk.rate_limit.sandbox_only", "Throughput limiting is only available in sandbox mode.", "Tooltip explaining that the throughput limiter requires sandbox mode.");
    public static LocStr RateLimitItemsPerMin =
        Loc.Str("dtk.rate_limit.items_per_min", "items/min", "Unit label for items per minute in the throughput limiter.");

    public static LocStr SettingsTransportConstructionHeading =
        Loc.Str("dtk.settings.transport_construction.heading", "TRANSPORT CONSTRUCTION", "Settings section heading for transport construction settings.");

    public static LocStr SettingsTransportCleanupHeading =
        Loc.Str("dtk.settings.transport_cleanup.heading", "TRANSPORT CLEANUP", "Settings section heading for transport cleanup settings.");

    public static LocStr SettingsTransportCleanupHotkey =
        Loc.Str("dtk.settings.transport_cleanup_hotkey.label", "Transport cleanup hotkey", "Settings row label for the transport cleanup hotkey.");
    public static LocStr SettingsHeightFilterHeading =
        Loc.Str("dtk.settings.height_filter.heading", "HEIGHT FILTER", "Settings section heading for height filter settings.");
    public static LocStr SettingsHeightFilterMaxVisible =
        Loc.Str("dtk.settings.height_filter_max_visible.label", "Max visible layer", "Settings row label for max visible layer.");
    public static LocStr SettingsHeightFilterMaxVisibleDescription =
        Loc.Str("dtk.settings.height_filter_max_visible.description", "Limits the height level of transports, structures, and pillars rendered in the world.", "Settings row description for max visible layer.");
    public static LocStr SettingsHeightFilterShowHotkey =
        Loc.Str("dtk.settings.height_filter_show_hotkey.label", "Show layer hotkey", "Settings row label for height filter show layer hotkey.");
    public static LocStr SettingsHeightFilterHideHotkey =
        Loc.Str("dtk.settings.height_filter_hide_hotkey.label", "Hide layer hotkey", "Settings row label for height filter hide layer hotkey.");
    public static LocStr SettingsGlobalHotkeyTooltip =
        Loc.Str("dtk.settings.global_hotkey.description", "Configures the keyboard shortcut that arms or triggers this tool. This is a global setting that applies to all saves and is saved directly to config.json.", "Settings row description for global hotkeys.");
    public static LocStr SettingsRestoreDefaults =
        Loc.Str("dtk.settings.action.restore_defaults", "Restore defaults", "Button label for restoring default settings.");
    public static LocStr SettingsRestoreDefaultsTooltip =
        Loc.Str("dtk.settings.action.restore_defaults.tooltip", "Restore the global mod defaults for all settings.", "Tooltip for restoring default settings.");
    public static LocStr SettingsSaveAsGlobal =
        Loc.Str("dtk.settings.action.save_as_global", "Save as config", "Button label for saving settings as config default.");
    public static LocStr SettingsSaveAsGlobalTooltip =
        Loc.Str("dtk.settings.action.save_as_global.tooltip", "Save these settings to config.json. They will be used as the defaults for all new games.", "Tooltip for saving settings as config default.");
    public static LocStr SettingsRestoredDefaults =
        Loc.Str("dtk.settings.status.restored_defaults", "Restored built-in defaults in memory.", "Status message after settings are restored to defaults.");
    public static LocStr SettingsSavedToConfig =
        Loc.Str("dtk.settings.status.saved_to_config", "Saved to config.json.", "Status message after settings are saved.");
    public static LocStr SettingsSaveFailed =
        Loc.Str("dtk.settings.status.save_failed", "Save failed: {0}", "Status message after settings save fails. {0} = error message.");
    public static LocStr SettingsStoreNotInitialized =
        Loc.Str("dtk.settings.status.store_not_initialized", "Save failed: settings store is not initialized.", "Status message when settings store is not initialized.");

    public static LocStr CopyAsMarkdownButton =
        Loc.Str("dtk.blueprint.copy_as_markdown.button", "Copy as Markdown", "Button label for copying blueprint information as Markdown.");
    public static LocStr CopyBlueprintMarkdownTooltip =
        Loc.Str("dtk.blueprint.copy_as_markdown.tooltip", "Copy blueprint stats as a Markdown table to the clipboard, ready to paste into the Hub.", "Tooltip for copying one blueprint as Markdown.");
    public static LocStr CopyFolderMarkdownTooltip =
        Loc.Str("dtk.blueprint.copy_folder_as_markdown.tooltip", "Copy folder blueprint list as a Markdown table to the clipboard.", "Tooltip for copying a blueprint folder as Markdown.");

    public static LocStr MarkdownComponentsHeading =
        Loc.Str("dtk.markdown.components.heading", "Components", "Markdown section heading for component counts.");
    public static LocStr MarkdownConstructionHeading =
        Loc.Str("dtk.markdown.construction.heading", "Construction", "Markdown section heading for construction costs.");
    public static LocStr MarkdownOperationalHeading =
        Loc.Str("dtk.markdown.operational.heading", "Operational", "Markdown section heading for operational stats.");
    public static LocStr MarkdownEntityHeader =
        Loc.Str("dtk.markdown.entity.header", "Entity", "Markdown table header for entity names.");
    public static LocStr MarkdownCountHeader =
        Loc.Str("dtk.markdown.count.header", "Count", "Markdown table header for counts.");
    public static LocStr MarkdownProductHeader =
        Loc.Str("dtk.markdown.product.header", "Product", "Markdown table header for product names.");
    public static LocStr MarkdownQuantityHeader =
        Loc.Str("dtk.markdown.quantity.header", "Quantity", "Markdown table header for quantities.");
    public static LocStr MarkdownPropertyHeader =
        Loc.Str("dtk.markdown.property.header", "Property", "Markdown table header for property names.");
    public static LocStr MarkdownValueHeader =
        Loc.Str("dtk.markdown.value.header", "Value", "Markdown table header for values.");
    public static LocStr MarkdownBlueprintHeader =
        Loc.Str("dtk.markdown.blueprint.header", "Blueprint", "Markdown table header for blueprint names.");
    public static LocStr MarkdownFolderHeader =
        Loc.Str("dtk.markdown.folder.header", "Folder", "Markdown table header for folder names.");
    public static LocStr MarkdownEntitiesStat =
        Loc.Str("dtk.markdown.entities.stat", "Entities", "Markdown operational stat label for entity count.");
    public static LocStr MarkdownPerMonthSuffix =
        Loc.Str("dtk.markdown.per_month.suffix", "/ mo", "Markdown suffix for monthly maintenance columns.");

    public static LocStr ThroughputTitle =
        Loc.Str("dtk.throughput.title", "Throughput", "Panel title for the throughput inspector.");
    public static LocStr ThroughputDisplay =
        Loc.Str("dtk.throughput.display", "Display throughput", "Checkbox label to enable display of throughput in the world.");
    public static LocStr ThroughputDaysToAverage =
        Loc.Str("dtk.throughput.days_to_average", "days to average", "Label for the days to average setting.");

    public static LocStr SettingsThroughputHeading =
        Loc.Str("dtk.settings.throughput.heading", "THROUGHPUT", "Settings section heading for throughput settings.");
    public static LocStr SettingsThroughputToggle =
        Loc.Str("dtk.settings.throughput_toggle.label", "Throughput overlay", "Settings row label for throughput overlay toggle.");
    public static LocStr SettingsThroughputToggleDescription =
        Loc.Str("dtk.settings.throughput_toggle.description", "Enables/disables the in-world throughput overlays. Individual entities' toggle must still be switched on in inspector or by the throughput tool.", "Settings row description for throughput overlay visibility.");
    public static LocStr SettingsThroughputGlow =
        Loc.Str("dtk.settings.throughput_glow.label", "Enable heatmap glow effect", "Settings row label for heatmap glow toggle.");
    public static LocStr SettingsThroughputGlowDescription =
        Loc.Str("dtk.settings.throughput_glow.description", "Cast a glowing light onto the ground matching the heatmap color. Operates independently of the text overlay. Disable if causing lag.", "Settings row description for heatmap glow.");
    public static LocStr SettingsThroughputToggleHotkey =
        Loc.Str("dtk.settings.throughput_toggle_hotkey.label", "Throughput overlay hotkey", "Settings row label for throughput overlay toggle hotkey.");
    public static LocStr SettingsThroughputHeatmap =
        Loc.Str("dtk.settings.throughput_heatmap.label", "Throughput coloring (heat map)", "Settings row label for throughput heat-map.");
    public static LocStr SettingsThroughputColorblind =
        Loc.Str("dtk.settings.throughput_colorblind.label", "Colorblind-friendly colors", "Settings row label for colorblind-friendly heatmap colors.");
    public static LocStr SettingsThroughputColorblindDescription =
        Loc.Str("dtk.settings.throughput_colorblind.description", "Changes the heat-map gradients from Green-Orange-Red to Blue-Yellow-Red for better visibility.", "Settings row description / tooltip for colorblind heatmap colors.");
    public static LocStr SettingsThroughputShowAsPercent =
        Loc.Str("dtk.settings.throughput_show_as_percent.label", "Show throughput as percent", "Settings row label for showing throughput as percentage of capacity.");
    public static LocStr SettingsThroughputShowAsPercentDescription =
        Loc.Str("dtk.settings.throughput_show_as_percent.description", "Switches the in-world throughput display format from absolute numbers (items/min) to a percentage of the entity's maximum capacity.", "Settings row description for showing throughput as percent.");
    
    public static LocStr ThroughputAoEToolName =
        Loc.Str("dtk.throughput.aoe_tool.name", "Throughput Area Tool", "Name of the throughput AoE tool.");
    public static LocStr ThroughputAoEToolTooltip =
        Loc.Str("dtk.throughput.aoe_tool.tooltip", "Click and drag to select an area, then configure throughput display settings for all selected entities by type.", "Tooltip for the throughput AoE tool button.");
    public static LocStr ThroughputAoEToolWindowTitle =
        Loc.Str("dtk.throughput.aoe_tool.window_title", "Configure Throughput in Area", "Title of the throughput configuration tool window.");
    public static LocStr ThroughputAoEToolApply =
        Loc.Str("dtk.throughput.aoe_tool.apply", "Apply", "Button label to apply changes.");
    public static LocStr ThroughputAoEToolClose =
        Loc.Str("dtk.throughput.aoe_tool.close", "Close", "Button label to close window.");
    public static LocStr ThroughputAoEToolNoChange =
        Loc.Str("dtk.throughput.aoe_tool.no_change", "No change", "Dropdown option to keep existing throughput display settings.");
    public static LocStr ThroughputAoEToolEnable =
        Loc.Str("dtk.throughput.aoe_tool.enable", "Enable", "Dropdown option to force enable throughput display.");
    public static LocStr ThroughputAoEToolDisable =
        Loc.Str("dtk.throughput.aoe_tool.disable", "Disable", "Dropdown option to force disable throughput display.");
    public static LocStr ThroughputAoEToolOverrideDaysLabel =
        Loc.Str("dtk.throughput.aoe_tool.override_days_label", "Set averaging period", "Label for setting the throughput averaging period in the AoE tool bulk panel.");
    public static LocStr ThroughputDays =
        Loc.Str("dtk.throughput.days", "days", "Label for days unit.");
    public static LocStr ThroughputAoEToolGlobalActionHeader =
        Loc.Str("dtk.throughput.aoe_tool.global_header", "Batch settings", "Header for the bulk action settings section.");
    public static LocStr ThroughputAoEToolGlobalDisplay =
        Loc.Str("dtk.throughput.aoe_tool.global_display", "Display state: ", "Label for global display state setting.");
    public static LocStr SettingsThroughputAoEToolHotkey =
        Loc.Str("dtk.settings.throughput_aoe_tool_hotkey.label", "Throughput tool hotkey", "Settings row label for throughput AoE tool toggle hotkey.");
    public static LocStr SettingsLayoutBoxModeHeading =
        Loc.Str("dtk.settings.layout_box.heading", "LAYOUT", "Settings section heading for layout box mode.");
    public static LocStr SettingsLayoutBoxModeToggle =
        Loc.Str("dtk.settings.layout_box.label", "Layout box mode", "Settings row label for layout box mode.");
    public static LocStr SettingsLayoutBoxModeDescription =
        Loc.Str("dtk.settings.layout_box.description", "Enables an X-Ray overlay rendering voxel bounding boxes around buildings to show their layout and clearance.", "Settings description for layout box mode.");
    public static LocStr SettingsLayoutBoxModeHotkey =
        Loc.Str("dtk.settings.layout_box_hotkey.label", "Layout box mode hotkey", "Settings row label for layout box mode hotkey.");

    public static LocStr SettingsRecycleBinHeading =
        Loc.Str("dtk.settings.recycle_bin.heading", "RECYCLE BIN", "Settings section heading for Recycle Bin settings.");
    public static LocStr SettingsUseRecycleBin =
        Loc.Str("dtk.settings.use_recycle_bin.label", "Use recycle bin", "Settings checkbox label for enabling recycle bin.");
    public static LocStr SettingsUseRecycleBinDescription =
        Loc.Str("dtk.settings.use_recycle_bin.description", "When enabled, blueprints and folders are copied to the recycle bin before deletion or updates, and the delete confirmation popup is suppressed.", "Settings checkbox description for enabling recycle bin.");
    public static LocStr SettingsRecycleBinFolderName =
        Loc.Str("dtk.settings.recycle_bin_folder_name.label", "Folder name", "Settings text field label for recycle bin folder name.");
    public static LocStr SettingsRecycleBinFolderNameDescription =
        Loc.Str("dtk.settings.recycle_bin_folder_name.description", "The folder name under the blueprint book root to use as the Recycle Bin.", "Settings text field description for recycle bin folder name.");

    public static LocStr SettingsUndoHeading =
        Loc.Str("dtk.settings.undo.heading", "UNDO PLACEMENT", "Settings section heading for Undo settings.");
    public static LocStr SettingsUndoHotkey =
        Loc.Str("dtk.settings.undo_hotkey.label", "Undo placement hotkey", "Settings row label for the undo placement hotkey.");
    public static LocStr UndoSuccessMessage =
        Loc.Str("dtk.undo.success", "Undo: Placement reverted successfully.", "Toast or message after undoing a placement.");
    public static LocStr UndoNoActionMessage =
        Loc.Str("dtk.undo.no_action", "Undo: No placement actions to revert.", "Toast or message when undo queue is empty.");

    public static LocStr SettingsPollutionHeading =
        Loc.Str("dtk.settings.pollution.heading", "POLLUTION OVERLAY", "Settings section heading for pollution overlay settings.");
    public static LocStr SettingsPollutionToggle =
        Loc.Str("dtk.settings.pollution_toggle.label", "Pollution overlay", "Settings row label for pollution overlay toggle.");
    public static LocStr SettingsPollutionToggleDescription =
        Loc.Str("dtk.settings.pollution_toggle.description", "Enables/disables the in-world pollution rate overlay. Displays averages over the configured days. These values include the effects of game difficulty settings and researched pollution-reduction technologies.", "Settings row description for pollution overlay visibility.");
    public static LocStr SettingsPollutionGlow =
        Loc.Str("dtk.settings.pollution_glow.label", "Enable heatmap glow effect", "Settings row label for pollution heatmap glow toggle.");
    public static LocStr SettingsPollutionGlowDescription =
        Loc.Str("dtk.settings.pollution_glow.description", "Cast a glowing light onto polluting entities matching their emission intensity. Disable if causing lag.", "Settings row description for pollution heatmap glow.");
    public static LocStr SettingsPollutionDaysToAverage =
        Loc.Str("dtk.settings.pollution_days.label", "Averaging period", "Settings row label for the days to average pollution.");
    public static LocStr SettingsPollutionDaysToAverageDescription =
        Loc.Str("dtk.settings.pollution_days.description", "Configures the sliding window in game days [0-360] to average emissions. Setting this to 0 completely disables data collection and patches.", "Settings row description for pollution averaging period.");
    public static LocStr SettingsPollutionShowAir =
        Loc.Str("dtk.settings.pollution_show_air.label", "Show air pollution", "Settings checkbox label for showing air pollution.");
    public static LocStr SettingsPollutionShowGround =
        Loc.Str("dtk.settings.pollution_show_ground.label", "Show ground & water pollution", "Settings checkbox label for showing ground & water pollution.");
    public static LocStr SettingsPollutionShowVehicle =
        Loc.Str("dtk.settings.pollution_show_vehicle.label", "Show vehicle & train pollution", "Settings checkbox label for showing vehicle & train pollution.");
    public static LocStr SettingsPollutionShowShip =
        Loc.Str("dtk.settings.pollution_show_ship.label", "Show ship pollution", "Settings checkbox label for showing ship pollution.");
    public static LocStr SettingsPollutionShowShipDescription =
        Loc.Str("dtk.settings.pollution_show_ship.description", "Enables/disables the in-world pollution rate overlay for ships. Displays the predicted pollution rate based on the current cargo route's round-trip time and fuel consumption. These values include the effects of game difficulty settings and researched pollution-reduction technologies.", "Settings checkbox description for showing ship pollution.");
    public static LocStr SettingsPollutionToggleHotkey =
        Loc.Str("dtk.settings.pollution_toggle_hotkey.label", "Pollution overlay hotkey", "Settings row label for pollution overlay toggle hotkey.");

    public static LocStr SettingsPlaceFolderHeading =
        Loc.Str("dtk.settings.place_folder.heading", "PLACE FOLDER", "Settings section heading for place folder settings.");
    public static LocStr SettingsBlueprintSpacingLabel =
        Loc.Str("dtk.settings.blueprint_spacing.label", "Blueprint spacing", "Settings row label for blueprint spacing.");
    public static LocStr SettingsBlueprintSpacingDescription =
        Loc.Str("dtk.settings.blueprint_spacing.description", "Spacing (in tiles) between blueprints placed side-by-side during batch placing [0-12].", "Settings row description for blueprint spacing.");
    public static LocStr PlaceAllButton =
        Loc.Str("dtk.blueprint.place_all.button", "Place all", "Button label for placing ghosts of all designs in the folder.");
    public static LocStr PlaceAllTooltip =
        Loc.Str("dtk.blueprint.place_all.tooltip", "Place all blueprints in this folder.", "Tooltip for placing all blueprints in a folder.");
}

