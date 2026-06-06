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
using Mafi.Core.Mods;
using Mafi.Localization;
using Mafi.Serialization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using CoI.AutoHelpers.Settings;

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
    private const int SETTINGS_SCHEMA_VERSION = 1;
    private const string SETTINGS_TAB_ICON_ASSET =
        "Assets/Unity/UserInterface/General/Blueprint.svg";
    private static readonly Percent SETTINGS_LABEL_WIDTH = 50.Percent();
    private static readonly Percent SETTINGS_COLUMN_WIDTH = 60.Percent();
    private static readonly Px SETTINGS_SECTION_INDENT = 4.pt();
    private static readonly Px SETTINGS_OPTIONS_GAP = 2.pt();

    private static readonly ModLogger s_log = new ModLogger("DTK.Settings");

    private static ModJsonConfig? s_config;
    private static IModStateJsonStore? s_store;

    public static MarkdownTableLanguage MarkdownTableLanguage { get; private set; } =
        MarkdownTableLanguage.English;
    public static MarkdownNumberFormat MarkdownNumberFormat { get; private set; } =
        MarkdownNumberFormat.Auto;

    public static void Initialize(ModJsonConfig config, IModStateJsonStore store)
    {
        s_config = config;
        s_store = store;
        MarkdownTableLanguage initialLanguage = FromInt(config.GetInt(MARKDOWN_TABLE_LANGUAGE_KEY, 0));
        MarkdownNumberFormat initialNumberFormat = NumberFormatFromInt(config.GetInt(MARKDOWN_NUMBER_FORMAT_KEY, 0));
        LoadFromJsonStore(store, initialLanguage, initialNumberFormat);
    }

    public static void SaveToJsonStore(IModStateJsonStore store)
    {
        ModStateJsonSaveResult result = store.SaveJson(BuildStateJson());
        if (!result.Succeeded)
            s_log.Warning($"Failed to save DTK settings state to {result.StorageKind} value '{result.StateKey}': {result.ErrorMessage}");
    }

    public static ModSettingsTab BuildSettingsTab()
    {
        return new ModSettingsTab(
            "designer-toolkit",
            DtkLocalization.ModName.AsFormatted,
            DtkLocalization.SettingsTabMarkdown.AsFormatted,
            100,
            BuildMarkdownSettingsContent,
            SETTINGS_TAB_ICON_ASSET);
    }

    private static UiComponent BuildMarkdownSettingsContent()
    {
        var root = new Column(SETTINGS_OPTIONS_GAP)
            .AlignItemsStretch()
            .PaddingLeft(SETTINGS_SECTION_INDENT)
            .Width(SETTINGS_COLUMN_WIDTH);

        root.Add(new Title(DtkLocalization.SettingsMarkdownCopyHeading.AsFormatted)
            .MarginLeft(-SETTINGS_SECTION_INDENT));

        Dropdown<MarkdownTableLanguage> languageDropdown =
            new Dropdown<MarkdownTableLanguage>(LanguageDropdownOption)
                .Label(DtkLocalization.SettingsMarkdownTableLanguage.AsFormatted)
                .Tooltip(new LocStrFormatted(
                    DtkLocalization.SettingsMarkdownTableLanguageDescription.TranslatedString
                    + "\n\n"
                    + DtkLocalization.SettingsMarkdownTableLanguagePending.TranslatedString))
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
                .Label(DtkLocalization.SettingsMarkdownNumberFormat.AsFormatted)
                .Tooltip(DtkLocalization.SettingsMarkdownNumberFormatDescription.AsFormatted)
                .LabelWidth(SETTINGS_LABEL_WIDTH)
                .SetOptions(
                    MarkdownNumberFormat.Auto,
                    MarkdownNumberFormat.English,
                    MarkdownNumberFormat.Local)
                .SetValue(MarkdownNumberFormat)
                .OnValueChanged((numberFormat, _) => SetMarkdownNumberFormat(numberFormat));

        root.Add(numberFormatDropdown);
        root.Add(BuildFooter(() =>
        {
            languageDropdown.SetValue(MarkdownTableLanguage);
            numberFormatDropdown.SetValue(MarkdownNumberFormat);
        }));

        return root;
    }

    private static PanelFooterRow BuildFooter(Action refresh)
    {
        var status = new Label(LocStrFormatted.Empty).MarginTopBottom(1.pt());

        var reset = new ButtonText(Button.General, DtkLocalization.SettingsRestoreDefaults.AsFormatted, () =>
        {
            MarkdownTableLanguage = MarkdownTableLanguage.English;
            MarkdownNumberFormat = MarkdownNumberFormat.Auto;
            refresh();
            status.Value(DtkLocalization.SettingsRestoredDefaults.AsFormatted);
        }).Tooltip(DtkLocalization.SettingsRestoreDefaultsTooltip.AsFormatted);

        var save = new ButtonText(Button.Primary, DtkLocalization.SettingsSaveAsGlobal.AsFormatted, () =>
        {
            if (s_store == null)
            {
                status.Value(DtkLocalization.SettingsStoreNotInitialized.AsFormatted);
                return;
            }

            SaveToJsonStore(s_store);
            status.Value(TrySaveGlobalConfig(out string error)
                ? DtkLocalization.SettingsSavedToConfig.AsFormatted
                : new LocStrFormatted(string.Format(DtkLocalization.SettingsSaveFailed.TranslatedString, error)));
        }).Tooltip(DtkLocalization.SettingsSaveAsGlobalTooltip.AsFormatted);

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

    private static void LoadFromJsonStore(
        IModStateJsonStore store,
        MarkdownTableLanguage initialLanguage,
        MarkdownNumberFormat initialNumberFormat)
    {
        MarkdownTableLanguage = initialLanguage;
        MarkdownNumberFormat = initialNumberFormat;

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
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load DTK settings state from {store.StorageKind}: {ex.Message}");
        }
    }

    private static string BuildStateJson()
    {
        var writer = new JsonWriter(128);
        writer.AppendStartObject();
        writer.AppendNumberField("schemaVersion", SETTINGS_SCHEMA_VERSION);
        writer.AppendNumberField("markdownTableLanguage", (int)MarkdownTableLanguage);
        writer.AppendNumberField("markdownNumberFormat", (int)MarkdownNumberFormat);
        writer.AppendEndObject();
        return writer.GetJsonAndClear();
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
                return DtkLocalization.SettingsLanguageLocal.AsFormatted;
            case MarkdownTableLanguage.Both:
                return DtkLocalization.SettingsLanguageBoth.AsFormatted;
            case MarkdownTableLanguage.Hybrid:
                return DtkLocalization.SettingsLanguageHybrid.AsFormatted;
            default:
                return DtkLocalization.SettingsLanguageEnglish.AsFormatted;
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
                return DtkLocalization.SettingsNumberFormatEnglish.AsFormatted;
            case MarkdownNumberFormat.Local:
                return DtkLocalization.SettingsNumberFormatLocal.AsFormatted;
            default:
                return DtkLocalization.SettingsNumberFormatAuto.AsFormatted;
        }
    }
}
