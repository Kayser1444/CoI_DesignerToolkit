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
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Core.Utils;
using Mafi.Unity;
using Mafi.Unity.Entities;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiToolkit;
using CoI.AutoHelpers.Localization;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using CoI.AutoHelpers.Settings;

namespace CoIDesignerToolkit;

public sealed class DesignerToolkitMod : IMod, IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT");
    private Harmony? m_harmony;
    private ISimLoopEvents? m_simLoopEvents;
    private IModStateJsonStore? m_settingsStateStore;
    private InstantBuildMode? m_instantBuildMode;
    private AreaUpgradeTool? m_areaUpgradeTool;
    private TransportCleanupTool? m_transportCleanupTool;

    public string Name => "Blueprint Designer's Toolkit";

    public int Version => 1;

    public bool IsUiOnly => false;

    public ModManifest Manifest { get; }

    public ModJsonConfig JsonConfig { get; }

    public Option<IConfig> ModConfig { get; set; }

    public static string ModVersion { get; private set; } = "?";

    public DesignerToolkitMod(ModManifest manifest)
    {
        Manifest = manifest;
        ModVersion = manifest.Version.ToString();
        JsonConfig = new ModJsonConfig(this);
    }

    public void RegisterPrototypes(ProtoRegistrator registrator)
    {
        m_harmony = new Harmony("DesignerToolkit");
        BlueprintUpdater.ApplyPatches(m_harmony);
        FolderPersistence.ApplyPatches(m_harmony, JsonConfig);
        BlueprintStats.ApplyPatches(m_harmony);
        BlueprintExport.ApplyPatches(m_harmony);
        NormalizeSymmetric.ApplyPatches(m_harmony);
    }

    public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
    }

    public void EarlyInit(DependencyResolver resolver)
    {
    }

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        s_log.Info($"[BDT] Blueprint Designer's Toolkit v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(DesignerToolkitMod).Assembly)}");

        RegisterAutoHelpersLocalizationLateApply(resolver);

        m_simLoopEvents = resolver.Resolve<ISimLoopEvents>();
        m_simLoopEvents.BeforeSave.AddNonSaveable(this, beforeSave);

        m_settingsStateStore = ModStateJsonStores.CreateDefault(JsonConfig, DesignerToolkitSettings.SettingsStateConfigKey);
        DesignerToolkitSettings.Initialize(JsonConfig, m_settingsStateStore);

        object? instaBuildManager = resolver.TryResolve(typeof(InstaBuildManager)).ValueOrNull;
        m_instantBuildMode = new InstantBuildMode(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<IConstructionManager>(),
            m_simLoopEvents,
            instaBuildManager);
        m_instantBuildMode.Initialize();
        DesignerToolkitSettings.InstantBuildModeChanged += m_instantBuildMode.OnSettingsChanged;

        m_areaUpgradeTool = new AreaUpgradeTool(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<UpgradesManager>(),
            resolver.Resolve<IGameLoopEvents>(),
            m_simLoopEvents);
        m_areaUpgradeTool.Initialize();

        m_transportCleanupTool = new TransportCleanupTool(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<IGameLoopEvents>(),
            m_simLoopEvents,
            resolver.Resolve<NewInstanceOf<EntityHighlighter>>().Instance);
        m_transportCleanupTool.Initialize();

        ModSettings.EnsureInitialized(
            resolver.Resolve<HudController>(),
            resolver.Resolve<UiRoot>(),
            resolver.Resolve<IRootEscapeManager>());
        ModSettings.RegisterTab(DesignerToolkitSettings.BuildSettingsTab());
    }

    public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
    }

    public void Dispose()
    {
        unsubscribeWorldEvents();
        m_harmony?.UnpatchAll("DesignerToolkit");
    }

    private void beforeSave()
    {
        IModStateJsonStore store = m_settingsStateStore
            ?? ModStateJsonStores.CreateDefault(JsonConfig, DesignerToolkitSettings.SettingsStateConfigKey);
        m_settingsStateStore = store;
        DesignerToolkitSettings.SaveToJsonStore(store);
    }

    private void RegisterAutoHelpersLocalizationLateApply(DependencyResolver resolver)
    {
        IGameLoopEvents gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            s_log.Info("Localization: late apply at renderer init state.");
            ApplyAutoHelpersLocalization();
        });
    }

    private void ApplyAutoHelpersLocalization()
    {
        string translationsDirectory = Path.Combine(Manifest.RootDirectoryPath, "translations");
        s_log.Info($"Localization: probing directory '{translationsDirectory}'.");

        if (!Directory.Exists(translationsDirectory))
        {
            s_log.Warning("Localization: translations directory does not exist; skipping.");
            return;
        }

        string[] jsonFiles = Directory.GetFiles(translationsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
        if (jsonFiles.Length == 0)
            s_log.Warning("Localization: no translation JSON files found.");
        else
            s_log.Info($"Localization: discovered {jsonFiles.Length} file(s): {string.Join(", ", jsonFiles)}");

        ModTranslationsApplyResult result = new ModTranslations().Apply(new ModTranslationsApplyOptions(
            translationsDirectory,
            typeof(DesignerToolkitMod).Assembly,
            Array.Empty<string>()));

        s_log.Info(
            $"Localization: applied locale='{result.AppliedLocaleCode}', upserted={result.UpsertedEntryCount}, scannedFields={result.ScannedFieldCount}, reboundFields={result.ReboundFieldCount}, readonlySkipped={result.SkippedReadonlyFieldCount}, missingTranslationSkipped={result.SkippedMissingTranslationFieldCount}, failedWrites={result.FailedFieldCount}, diagnostics={result.Diagnostics.Count}.");

        foreach (TranslationDiagnostic diagnostic in result.Diagnostics)
        {
            string itemInfo = diagnostic.ItemIndex.HasValue ? $", itemIndex={diagnostic.ItemIndex.Value}" : string.Empty;
            string message = $"Localization diagnostic [{diagnostic.Severity}] source='{diagnostic.SourcePath}'{itemInfo}: {diagnostic.Message}";
            if (diagnostic.Severity == TranslationDiagnosticSeverity.Info)
                s_log.Info(message);
            else
                s_log.Warning(message);
        }
    }

    private void unsubscribeWorldEvents()
    {
        if (m_instantBuildMode != null)
        {
            DesignerToolkitSettings.InstantBuildModeChanged -= m_instantBuildMode.OnSettingsChanged;
            m_instantBuildMode.Dispose();
            m_instantBuildMode = null;
        }

        if (m_areaUpgradeTool != null)
        {
            m_areaUpgradeTool.Dispose();
            m_areaUpgradeTool = null;
        }

        if (m_transportCleanupTool != null)
        {
            m_transportCleanupTool.Dispose();
            m_transportCleanupTool = null;
        }

        if (m_simLoopEvents != null)
        {
            try { m_simLoopEvents.BeforeSave.RemoveNonSaveable(this, beforeSave); }
            catch { }
            m_simLoopEvents = null;
        }
    }
}
