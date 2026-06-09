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
    public static LocStr SettingsToolsHeading =
        Loc.Str("dtk.settings.tools.heading", "TOOLS", "Settings section heading for tool hotkeys.");

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
}
