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

internal static class DesignerToolkitSettings
{
    internal const string SettingsStateConfigKey = "dtkSettingsStateJson";

    private const string MARKDOWN_TABLE_LANGUAGE_KEY = "markdown_table_language";
    private const string MARKDOWN_NUMBER_FORMAT_KEY = "markdown_number_format";
    private const string INSTANT_BUILD_MODE_KEY = "instant_build_mode";
    private const string AREA_UPGRADE_HOTKEY_PRIMARY_KEY = "area_upgrade_hotkey_primary";
    private const string AREA_UPGRADE_HOTKEY_SECONDARY_KEY = "area_upgrade_hotkey_secondary";
    private const string AREA_DOWNGRADE_HOTKEY_PRIMARY_KEY = "area_downgrade_hotkey_primary";
    private const string AREA_DOWNGRADE_HOTKEY_SECONDARY_KEY = "area_downgrade_hotkey_secondary";
    private const string TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY = "transport_cleanup_hotkey_primary";
    private const string TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY = "transport_cleanup_hotkey_secondary";
    private const string LEGACY_AREA_UPGRADE_HOTKEY_KEY = "area_upgrade_hotkey_key";
    private const string LEGACY_AREA_UPGRADE_HOTKEY_CTRL_KEY = "area_upgrade_hotkey_ctrl";
    private const string LEGACY_AREA_UPGRADE_HOTKEY_ALT_KEY = "area_upgrade_hotkey_alt";
    private const string LEGACY_AREA_UPGRADE_HOTKEY_SHIFT_KEY = "area_upgrade_hotkey_shift";
    private const string LEGACY_AREA_DOWNGRADE_HOTKEY_KEY = "area_downgrade_hotkey_key";
    private const string LEGACY_AREA_DOWNGRADE_HOTKEY_CTRL_KEY = "area_downgrade_hotkey_ctrl";
    private const string LEGACY_AREA_DOWNGRADE_HOTKEY_ALT_KEY = "area_downgrade_hotkey_alt";
    private const string LEGACY_AREA_DOWNGRADE_HOTKEY_SHIFT_KEY = "area_downgrade_hotkey_shift";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_KEY = "transport_cleanup_hotkey_key";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_CTRL_KEY = "transport_cleanup_hotkey_ctrl";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_ALT_KEY = "transport_cleanup_hotkey_alt";
    private const string LEGACY_TRANSPORT_CLEANUP_HOTKEY_SHIFT_KEY = "transport_cleanup_hotkey_shift";
    private const int SETTINGS_SCHEMA_VERSION = 1;
    private const string SETTINGS_TAB_ICON_ASSET =
        "Assets/Unity/UserInterface/General/Blueprint.svg";
    private static readonly Percent SETTINGS_LABEL_WIDTH = 34.Percent();
    private static readonly Percent SETTINGS_COLUMN_WIDTH = 96.Percent();
    private static readonly Px SETTINGS_SECTION_INDENT = 4.pt();
    private static readonly Px SETTINGS_OPTIONS_GAP = 2.pt();
    private static readonly BdtHotkey DEFAULT_AREA_UPGRADE_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftControl, KeyCode.PageUp);
    private static readonly BdtHotkey DEFAULT_AREA_DOWNGRADE_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftControl, KeyCode.PageDown);
    private static readonly BdtHotkey DEFAULT_TRANSPORT_CLEANUP_HOTKEY =
        BdtHotkey.FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);

    private static readonly ModLogger s_log = new ModLogger("BDT.Settings");

    private static ModJsonConfig? s_config;
    private static IModStateJsonStore? s_store;

    public static MarkdownTableLanguage MarkdownTableLanguage { get; private set; } =
        MarkdownTableLanguage.English;
    public static MarkdownNumberFormat MarkdownNumberFormat { get; private set; } =
        MarkdownNumberFormat.Auto;
    public static bool InstantBuildModeEnabled { get; private set; }
    public static BdtHotkey AreaUpgradeHotkey { get; private set; } = DEFAULT_AREA_UPGRADE_HOTKEY;
    public static BdtHotkey AreaDowngradeHotkey { get; private set; } = DEFAULT_AREA_DOWNGRADE_HOTKEY;
    public static BdtHotkey TransportCleanupHotkey { get; private set; } = DEFAULT_TRANSPORT_CLEANUP_HOTKEY;

    public static event Action<bool>? InstantBuildModeChanged;

    public static void Initialize(ModJsonConfig config, IModStateJsonStore store)
    {
        s_config = config;
        s_store = store;
        MarkdownTableLanguage initialLanguage = FromInt(config.GetInt(MARKDOWN_TABLE_LANGUAGE_KEY, 0));
        MarkdownNumberFormat initialNumberFormat = NumberFormatFromInt(config.GetInt(MARKDOWN_NUMBER_FORMAT_KEY, 0));
        bool initialInstantBuildMode = config.GetBool(INSTANT_BUILD_MODE_KEY, false);
        BdtHotkey initialAreaUpgradeHotkey = HotkeyFromConfig(
            config,
            AREA_UPGRADE_HOTKEY_PRIMARY_KEY,
            AREA_UPGRADE_HOTKEY_SECONDARY_KEY,
            LEGACY_AREA_UPGRADE_HOTKEY_KEY,
            LEGACY_AREA_UPGRADE_HOTKEY_CTRL_KEY,
            LEGACY_AREA_UPGRADE_HOTKEY_ALT_KEY,
            LEGACY_AREA_UPGRADE_HOTKEY_SHIFT_KEY,
            DEFAULT_AREA_UPGRADE_HOTKEY);
        BdtHotkey initialAreaDowngradeHotkey = HotkeyFromConfig(
            config,
            AREA_DOWNGRADE_HOTKEY_PRIMARY_KEY,
            AREA_DOWNGRADE_HOTKEY_SECONDARY_KEY,
            LEGACY_AREA_DOWNGRADE_HOTKEY_KEY,
            LEGACY_AREA_DOWNGRADE_HOTKEY_CTRL_KEY,
            LEGACY_AREA_DOWNGRADE_HOTKEY_ALT_KEY,
            LEGACY_AREA_DOWNGRADE_HOTKEY_SHIFT_KEY,
            DEFAULT_AREA_DOWNGRADE_HOTKEY);
        BdtHotkey initialTransportCleanupHotkey = HotkeyFromConfig(
            config,
            TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY,
            TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_CTRL_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_ALT_KEY,
            LEGACY_TRANSPORT_CLEANUP_HOTKEY_SHIFT_KEY,
            DEFAULT_TRANSPORT_CLEANUP_HOTKEY);
        LoadFromJsonStore(
            store,
            initialLanguage,
            initialNumberFormat,
            initialInstantBuildMode,
            initialAreaUpgradeHotkey,
            initialAreaDowngradeHotkey,
            initialTransportCleanupHotkey);
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

        root.Add(new Title(BdtLocalization.SettingsToolsHeading.AsFormatted)
            .MarginTop(4.pt())
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        BdtKeyBindingField areaUpgradePrimaryField;
        BdtKeyBindingField areaUpgradeSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsAreaUpgradeHotkey.AsFormatted,
            () => AreaUpgradeHotkey,
            hotkey => AreaUpgradeHotkey = hotkey,
            out areaUpgradePrimaryField,
            out areaUpgradeSecondaryField));

        BdtKeyBindingField areaDowngradePrimaryField;
        BdtKeyBindingField areaDowngradeSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsAreaDowngradeHotkey.AsFormatted,
            () => AreaDowngradeHotkey,
            hotkey => AreaDowngradeHotkey = hotkey,
            out areaDowngradePrimaryField,
            out areaDowngradeSecondaryField));

        BdtKeyBindingField transportCleanupPrimaryField;
        BdtKeyBindingField transportCleanupSecondaryField;
        root.Add(BuildHotkeyRow(
            BdtLocalization.SettingsTransportCleanupHotkey.AsFormatted,
            () => TransportCleanupHotkey,
            hotkey => TransportCleanupHotkey = hotkey,
            out transportCleanupPrimaryField,
            out transportCleanupSecondaryField));

        root.Add(BuildFooter(() =>
        {
            languageDropdown.SetValue(MarkdownTableLanguage);
            numberFormatDropdown.SetValue(MarkdownNumberFormat);
            instantBuildToggle.Value(InstantBuildModeEnabled);
            areaUpgradePrimaryField.Refresh();
            areaUpgradeSecondaryField.Refresh();
            areaDowngradePrimaryField.Refresh();
            areaDowngradeSecondaryField.Refresh();
            transportCleanupPrimaryField.Refresh();
            transportCleanupSecondaryField.Refresh();
        }));

        return root;
    }

    private static Row BuildHotkeyRow(
        LocStrFormatted label,
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
                .Tooltip(BdtLocalization.SettingsToolHotkeyDescription.AsFormatted)
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
            AreaUpgradeHotkey = DEFAULT_AREA_UPGRADE_HOTKEY;
            AreaDowngradeHotkey = DEFAULT_AREA_DOWNGRADE_HOTKEY;
            TransportCleanupHotkey = DEFAULT_TRANSPORT_CLEANUP_HOTKEY;
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
            if (s_config != null && !TrySetHotkeyConfig(s_config, AreaUpgradeHotkey, AREA_UPGRADE_HOTKEY_PRIMARY_KEY, AREA_UPGRADE_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, AreaDowngradeHotkey, AREA_DOWNGRADE_HOTKEY_PRIMARY_KEY, AREA_DOWNGRADE_HOTKEY_SECONDARY_KEY, out error))
                return false;
            if (s_config != null && !TrySetHotkeyConfig(s_config, TransportCleanupHotkey, TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY, TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY, out error))
                return false;

            string? directory = Path.GetDirectoryName(typeof(DesignerToolkitSettings).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
            {
                error = "Could not resolve mod directory.";
                return false;
            }

            string path = Path.Combine(directory, "config.json");
            string json = File.ReadAllText(path);
            string updated = TryReplaceConfigDefault(json, MARKDOWN_TABLE_LANGUAGE_KEY, (int)MarkdownTableLanguage, out bool languageUpdated);
            updated = TryReplaceConfigDefault(updated, MARKDOWN_NUMBER_FORMAT_KEY, (int)MarkdownNumberFormat, out bool numberFormatUpdated);
            updated = TryReplaceConfigDefault(updated, INSTANT_BUILD_MODE_KEY, InstantBuildModeEnabled, out bool instantBuildUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                AreaUpgradeHotkey,
                AREA_UPGRADE_HOTKEY_PRIMARY_KEY,
                AREA_UPGRADE_HOTKEY_SECONDARY_KEY,
                out bool areaUpgradeHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                AreaDowngradeHotkey,
                AREA_DOWNGRADE_HOTKEY_PRIMARY_KEY,
                AREA_DOWNGRADE_HOTKEY_SECONDARY_KEY,
                out bool areaDowngradeHotkeyUpdated);
            updated = TryReplaceHotkeyConfigDefaults(
                updated,
                TransportCleanupHotkey,
                TRANSPORT_CLEANUP_HOTKEY_PRIMARY_KEY,
                TRANSPORT_CLEANUP_HOTKEY_SECONDARY_KEY,
                out bool transportCleanupHotkeyUpdated);
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
            if (!areaUpgradeHotkeyUpdated)
            {
                error = "Could not find area upgrade hotkey defaults in config.json.";
                return false;
            }
            if (!areaDowngradeHotkeyUpdated)
            {
                error = "Could not find area downgrade hotkey defaults in config.json.";
                return false;
            }
            if (!transportCleanupHotkeyUpdated)
            {
                error = "Could not find transport cleanup hotkey defaults in config.json.";
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
        string result = Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RegexOptions.Singleline);
        updated = result != json;
        return result;
    }

    private static string TryReplaceConfigDefault(string json, string key, bool value, out bool updated)
    {
        string pattern = "(\"" + key + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*)(true|false|0|1)";
        string result = Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + (value ? "true" : "false"),
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        updated = result != json;
        return result;
    }

    private static string TryReplaceConfigDefault(string json, string key, string value, out bool updated)
    {
        string pattern = "(\"" + key + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*)\"(?:\\\\.|[^\"])*\"";
        string result = Regex.Replace(
            json,
            pattern,
            match => match.Groups[1].Value + "\"" + EscapeJsonString(value) + "\"",
            RegexOptions.Singleline);
        updated = result != json;
        return result;
    }

    private static string EscapeJsonString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool ConfigHasValue(ModJsonConfig config, string key)
    {
        try
        {
            string? directory = Path.GetDirectoryName(typeof(DesignerToolkitSettings).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            string json = File.ReadAllText(Path.Combine(directory, "config.json"));
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
        BdtHotkey initialAreaUpgradeHotkey,
        BdtHotkey initialAreaDowngradeHotkey,
        BdtHotkey initialTransportCleanupHotkey)
    {
        MarkdownTableLanguage = initialLanguage;
        MarkdownNumberFormat = initialNumberFormat;
        InstantBuildModeEnabled = initialInstantBuildMode;
        AreaUpgradeHotkey = initialAreaUpgradeHotkey;
        AreaDowngradeHotkey = initialAreaDowngradeHotkey;
        TransportCleanupHotkey = initialTransportCleanupHotkey;

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
            AreaUpgradeHotkey = HotkeyFromState(
                root,
                "areaUpgradeHotkeyPrimary",
                "areaUpgradeHotkeySecondary",
                "areaUpgradeHotkeyKey",
                "areaUpgradeHotkeyCtrl",
                "areaUpgradeHotkeyAlt",
                "areaUpgradeHotkeyShift",
                AreaUpgradeHotkey);
            AreaDowngradeHotkey = HotkeyFromState(
                root,
                "areaDowngradeHotkeyPrimary",
                "areaDowngradeHotkeySecondary",
                "areaDowngradeHotkeyKey",
                "areaDowngradeHotkeyCtrl",
                "areaDowngradeHotkeyAlt",
                "areaDowngradeHotkeyShift",
                AreaDowngradeHotkey);
            TransportCleanupHotkey = HotkeyFromState(
                root,
                "transportCleanupHotkeyPrimary",
                "transportCleanupHotkeySecondary",
                "transportCleanupHotkeyKey",
                "transportCleanupHotkeyCtrl",
                "transportCleanupHotkeyAlt",
                "transportCleanupHotkeyShift",
                TransportCleanupHotkey);
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
        AppendHotkeyFields(
            writer,
            AreaUpgradeHotkey,
            "areaUpgradeHotkeyPrimary",
            "areaUpgradeHotkeySecondary");
        AppendHotkeyFields(
            writer,
            AreaDowngradeHotkey,
            "areaDowngradeHotkeyPrimary",
            "areaDowngradeHotkeySecondary");
        AppendHotkeyFields(
            writer,
            TransportCleanupHotkey,
            "transportCleanupHotkeyPrimary",
            "transportCleanupHotkeySecondary");
        writer.AppendEndObject();
        return writer.GetJsonAndClear();
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
