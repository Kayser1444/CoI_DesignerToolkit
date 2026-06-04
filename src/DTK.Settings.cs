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

internal static class DesignerToolkitSettings
{
    internal const string SettingsStateConfigKey = "dtkSettingsStateJson";

    private const string MARKDOWN_TABLE_LANGUAGE_KEY = "markdown_table_language";
    private const int SETTINGS_SCHEMA_VERSION = 1;
    private const string SETTINGS_TAB_ICON_ASSET =
        "Assets/Unity/UserInterface/Toolbar/Stats.svg";
    private static readonly Percent SETTINGS_LABEL_WIDTH = 50.Percent();
    private static readonly Percent SETTINGS_COLUMN_WIDTH = 60.Percent();
    private static readonly Px SETTINGS_SECTION_INDENT = 4.pt();
    private static readonly Px SETTINGS_OPTIONS_GAP = 2.pt();

    private static readonly ModLogger s_log = new ModLogger("DTK.Settings");

    public static MarkdownTableLanguage MarkdownTableLanguage { get; private set; } =
        MarkdownTableLanguage.English;

    public static void Initialize(ModJsonConfig config, IModStateJsonStore store)
    {
        MarkdownTableLanguage initialLanguage = FromInt(config.GetInt(MARKDOWN_TABLE_LANGUAGE_KEY, 0));
        MarkdownTableLanguage = LoadFromJsonStore(store, initialLanguage);
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

        return root;
    }

    private static void SetMarkdownTableLanguage(MarkdownTableLanguage language)
    {
        MarkdownTableLanguage = language;
    }

    private static MarkdownTableLanguage LoadFromJsonStore(
        IModStateJsonStore store,
        MarkdownTableLanguage initialLanguage)
    {
        string json = store.LoadJson();
        if (string.IsNullOrWhiteSpace(json))
            return initialLanguage;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root))
                return initialLanguage;

            if (!TryGetInt(root, "schemaVersion", out int schemaVersion)
                || schemaVersion != SETTINGS_SCHEMA_VERSION)
                return initialLanguage;

            if (TryGetInt(root, "markdownTableLanguage", out int language))
                return FromInt(language);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load DTK settings state from {store.StorageKind}: {ex.Message}");
        }

        return initialLanguage;
    }

    private static string BuildStateJson()
    {
        var writer = new JsonWriter(128);
        writer.AppendStartObject();
        writer.AppendNumberField("schemaVersion", SETTINGS_SCHEMA_VERSION);
        writer.AppendNumberField("markdownTableLanguage", (int)MarkdownTableLanguage);
        writer.AppendEndObject();
        return writer.GetJsonAndClear();
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
}
