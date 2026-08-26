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
using Mafi.Core;
using Mafi.Core.Console;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Input;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.SaveGame;
using Mafi.Core.Simulation;
using Mafi.Core.Utils;
using Mafi.Core.Vehicles;
using Mafi.Unity;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
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
    private readonly ModSaveLifecycle m_removalSaveLifecycle = new ModSaveLifecycle();
    private Harmony? m_harmony;
    private ISimLoopEvents? m_simLoopEvents;
    private IGameLoopEvents? m_gameLoopEvents;
    private ISaveManager? m_saveManager;
    private IModStateJsonStore? m_settingsStateStore;
    private InstantBuildMode? m_instantBuildMode;
    private TransportCleanupTool? m_transportCleanupTool;
    private HeightFilter? m_heightFilter;
    private IModStateJsonStore? m_rateLimitsStateStore;
    private IModStateJsonStore? m_throughputStateStore;
    private IModStateJsonStore? m_groundwaterStateStore;
    private ThroughputManager? m_throughputManager;
    private ThroughputWorldRenderer? m_throughputWorldRenderer;
    private UnityEngine.GameObject? m_throughputWorldRendererGo;
    private ThroughputAoETool? m_throughputAoETool;
    private TransportProductRemovalAoETool? m_transportProductRemovalAoETool;
    private UndoManager? m_undoManager;
    private PollutionManager? m_pollutionManager;
    private PollutionWorldRenderer? m_pollutionWorldRenderer;
    private UnityEngine.GameObject? m_pollutionWorldRendererGo;
    private RadiationManager? m_radiationManager;
    private RadiationWorldRenderer? m_radiationWorldRenderer;
    private UnityEngine.GameObject? m_radiationWorldRendererGo;
    private Action<GameTime>? m_firstUnpauseAutosaveHandler;
    private bool m_isInitialSaveInProgress;

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
        BlueprintRecycleBin.ApplyPatches(m_harmony);
        FolderPersistence.ApplyPatches(m_harmony, JsonConfig);
        BlueprintStats.ApplyPatches(m_harmony);
        BlueprintExport.ApplyPatches(m_harmony);
        NormalizeSymmetric.ApplyPatches(m_harmony);
        LegacyBeltConfigurations.ApplyPatches(m_harmony);
        RateLimitPatches.Apply(m_harmony);
        ThroughputPatches.Apply(m_harmony);
        ThroughputInspectorPatches.Apply(m_harmony);
        ContentDisplayPatches.Apply(m_harmony);
        UndoPatches.Apply(m_harmony);
        PollutionPatches.Apply(m_harmony);
        HeightRoutingPatches.Apply(m_harmony);
        LegacyStackerPatches.Apply(m_harmony);
        PipeColoring.ApplyPatches(m_harmony);
        GroundwaterStatsManager.ApplyPatches(m_harmony);
        GroundwaterInspectorPatches.Apply(m_harmony);
        CoI.AutoHelpers.InputControl.CustomKeybindsInjector.ApplyPatches(m_harmony, Manifest.DisplayName, typeof(HotkeysRegistry));
    }

    public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
        depBuilder.RegisterDependency<TransportProductRemovalCommandsProcessor>().AsAllInterfaces();
        depBuilder.RegisterDependency<TransportProductRemovalBatchCommandsProcessor>().AsAllInterfaces();
        depBuilder.RegisterDependency<LegacyStackerSupportValidator>().AsAllInterfaces();
    }

    public void EarlyInit(DependencyResolver resolver)
    {
    }

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        s_log.EnableConsoleLogging();
        s_log.RegisterAutoConsoleMirroring(this, resolver.Resolve<IGameLoopEvents>(), resolver.Resolve<GameConsoleCommandsExecutor>());

        ApplyAutoHelpersLocalization();
        RegisterAutoHelpersLocalizationLateApply(resolver);

        m_gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        m_simLoopEvents = resolver.Resolve<ISimLoopEvents>();
        m_saveManager = resolver.Resolve<ISaveManager>();
        m_simLoopEvents.BeforeSave.AddNonSaveable(this, beforeSave);
        m_saveManager.OnSaveDone += onSaveDone;

        m_settingsStateStore = ModStateJsonStores.CreateDefault(JsonConfig, DesignerToolkitSettings.SettingsStateConfigKey);
        DesignerToolkitSettings.Initialize(JsonConfig, m_settingsStateStore, Manifest.RootDirectoryPath, gameWasLoaded);
        DesignerToolkitSettings.SetBlueprintsLibraryProvider(() => resolver.Resolve<Mafi.Core.Entities.Blueprints.BlueprintsLibrary>());
        DesignerToolkitSettings.SetDifficultyConfig(resolver.Resolve<Mafi.Core.Game.GameDifficultyConfig>());
        PipeColoring.Initialize(resolver);
        HotkeysRegistry.Initialize(resolver.Resolve<Mafi.Unity.Audio.AudioDb>());

        m_rateLimitsStateStore = ModStateJsonStores.CreateDefault(JsonConfig, RateLimitManager.CONFIG_KEY);
        RateLimitManager.Initialize(m_rateLimitsStateStore);
        
        m_throughputStateStore = ModStateJsonStores.CreateDefault(JsonConfig, ThroughputManager.CONFIG_KEY);
        m_throughputManager = new ThroughputManager();
        m_throughputManager.Initialize(resolver, m_throughputStateStore);

        m_pollutionManager = new PollutionManager();
        m_pollutionManager.Initialize(resolver);

        m_radiationManager = new RadiationManager();
        m_radiationManager.Initialize(resolver);

        m_groundwaterStateStore = ModStateJsonStores.CreateDefault(JsonConfig, GroundwaterStatsManager.CONFIG_KEY);
        GroundwaterStatsManager.Initialize(resolver, m_groundwaterStateStore);

        var gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            BdtDiagnostics.Debug(s_log, $"Diagnostics: {BdtDiagnostics.Describe()}.");
            m_throughputWorldRendererGo = new UnityEngine.GameObject("BDT.ThroughputWorldRenderer");
            m_throughputWorldRenderer = m_throughputWorldRendererGo.AddComponent<ThroughputWorldRenderer>();
            m_throughputWorldRenderer.Setup(resolver.Resolve<EntitiesManager>(), resolver.Resolve<NewInstanceOf<EntityHighlighter>>().Instance, gameLoopEvents);
            UnityEngine.Object.DontDestroyOnLoad(m_throughputWorldRendererGo);

            var layoutBoxRendererGo = new UnityEngine.GameObject("BDT.LayoutBoxRenderer");
            var layoutBoxRenderer = layoutBoxRendererGo.AddComponent<LayoutBoxRendererMb>();
            layoutBoxRenderer.Init(resolver.Resolve<IEntitiesManager>(), resolver.Resolve<ShortcutsManager>());
            UnityEngine.Object.DontDestroyOnLoad(layoutBoxRendererGo);

            m_pollutionWorldRendererGo = new UnityEngine.GameObject("BDT.PollutionWorldRenderer");
            m_pollutionWorldRenderer = m_pollutionWorldRendererGo.AddComponent<PollutionWorldRenderer>();
            m_pollutionWorldRenderer.Setup(resolver.Resolve<EntitiesManager>(), resolver.Resolve<NewInstanceOf<EntityHighlighter>>().Instance, gameLoopEvents, resolver.Resolve<Mafi.Core.Terrain.TerrainManager>(), resolver.Resolve<Mafi.Core.Prototypes.ProtosDb>(), resolver.Resolve<Mafi.Core.PropertiesDb.IPropertiesDb>());
            UnityEngine.Object.DontDestroyOnLoad(m_pollutionWorldRendererGo);

            m_radiationWorldRendererGo = new UnityEngine.GameObject("BDT.RadiationWorldRenderer");
            m_radiationWorldRenderer = m_radiationWorldRendererGo.AddComponent<RadiationWorldRenderer>();
            m_radiationWorldRenderer.Setup(resolver.Resolve<EntitiesManager>(), resolver.Resolve<NewInstanceOf<EntityHighlighter>>().Instance, gameLoopEvents);
            UnityEngine.Object.DontDestroyOnLoad(m_radiationWorldRendererGo);
        });
        
        var entitiesManager = resolver.Resolve<EntitiesManager>();
        entitiesManager.EntityRemoved.AddNonSaveable(this, RateLimitManager.OnEntityRemoved);
        TransportProductRemovalManager.Initialize(
            entitiesManager,
            resolver.Resolve<IVehicleBuffersRegistry>(),
            m_removalSaveLifecycle.VanillaAttachments,
            ModStateJsonStores.CreateDefault(JsonConfig, TransportProductRemovalManager.CONFIG_KEY));
        entitiesManager.EntityRemoved.AddNonSaveable(this, TransportProductRemovalManager.OnEntityRemoved);
        LegacyStackerFullAlertManager.Initialize(
            entitiesManager,
            ModStateJsonStores.CreateDefault(JsonConfig, LegacyStackerFullAlertManager.CONFIG_KEY));
        entitiesManager.EntityRemoved.AddNonSaveable(this, LegacyStackerFullAlertManager.OnEntityRemoved);

        object? instaBuildManager = resolver.TryResolve(typeof(InstaBuildManager)).ValueOrNull;
        m_instantBuildMode = new InstantBuildMode(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<IConstructionManager>(),
            resolver.Resolve<UpgradesManager>(),
            m_simLoopEvents,
            instaBuildManager,
            resolver.Resolve<GameDifficultyConfig>());
        m_instantBuildMode.Initialize();
        DesignerToolkitSettings.InstantBuildModeChanged += m_instantBuildMode.OnSettingsChanged;


        m_transportCleanupTool = new TransportCleanupTool(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<IGameLoopEvents>(),
            m_simLoopEvents,
            resolver.Resolve<NewInstanceOf<EntityHighlighter>>().Instance);
        m_transportCleanupTool.Initialize();

        m_throughputAoETool = new ThroughputAoETool(
            resolver.Resolve<Mafi.Unity.Ui.Hud.ToolbarHud>(),
            resolver.Resolve<Mafi.Unity.Ui.UiContext>(),
            resolver.Resolve<Mafi.Unity.InputControl.CursorPickingManager>(),
            resolver.Resolve<Mafi.Unity.UiStatic.Cursors.CursorManager>(),
            resolver.Resolve<Mafi.Unity.InputControl.AreaTool.AreaSelectionToolFactory>(),
            resolver.Resolve<IEntitiesManager>(),
            resolver.Resolve<NewInstanceOf<EntityHighlighter>>(),
            resolver.Resolve<NewInstanceOf<Mafi.Unity.Terrain.TerrainAreaOutlineRenderer>>(),
            resolver.Resolve<IGameLoopEvents>());
        m_throughputAoETool.Initialize();

        m_transportProductRemovalAoETool = new TransportProductRemovalAoETool(
            resolver.Resolve<Mafi.Unity.Ui.Hud.ToolbarHud>(),
            resolver.Resolve<Mafi.Unity.Ui.UiContext>(),
            resolver.Resolve<Mafi.Unity.InputControl.CursorPickingManager>(),
            resolver.Resolve<Mafi.Unity.UiStatic.Cursors.CursorManager>(),
            resolver.Resolve<Mafi.Unity.InputControl.AreaTool.AreaSelectionToolFactory>(),
            resolver.Resolve<IEntitiesManager>(),
            resolver.Resolve<NewInstanceOf<EntityHighlighter>>(),
            resolver.Resolve<NewInstanceOf<Mafi.Unity.Terrain.TerrainAreaOutlineRenderer>>(),
            resolver.Resolve<IGameLoopEvents>());
        m_transportProductRemovalAoETool.Initialize();

        m_heightFilter = new HeightFilter(m_harmony!, resolver.Resolve<IGameLoopEvents>());
        m_heightFilter.Initialize(resolver);

        m_undoManager = new UndoManager(
            resolver.Resolve<EntitiesManager>(),
            resolver.Resolve<IConstructionManager>(),
            resolver.Resolve<IInputScheduler>(),
            resolver.Resolve<EntitiesCloneConfigHelper>(),
            resolver.Resolve<IGameLoopEvents>(),
            resolver.Resolve<ISimLoopEvents>(),
            resolver.Resolve<UiRoot>()
        );
        m_undoManager.Initialize();

        ModSettings.EnsureInitialized(
            resolver.Resolve<HudController>(),
            resolver.Resolve<UiRoot>(),
            resolver.Resolve<IRootEscapeManager>());
        ModSettings.RegisterTab(DesignerToolkitSettings.BuildSettingsTab(resolver));

        InitializeNewGameOptions(resolver, gameWasLoaded);
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
        LegacyStackerFullAlertManager.BeforeSave();
        TransportProductRemovalManager.SaveState();
        m_removalSaveLifecycle.BeforeVanillaSave();

        IModStateJsonStore store = m_settingsStateStore
            ?? ModStateJsonStores.CreateDefault(JsonConfig, DesignerToolkitSettings.SettingsStateConfigKey);
        m_settingsStateStore = store;
        DesignerToolkitSettings.SaveToJsonStore(store);

        if (m_isInitialSaveInProgress)
        {
            m_isInitialSaveInProgress = false;
            DesignerToolkitSettings.IsFirstUnpausePending = false;
            s_log.Info("Initial save blob written with isFirstUnpausePending=true; runtime flag now cleared to false for active session.");
        }

        // Rate limits save automatically when modified, but just in case:
        IModStateJsonStore rateStore = m_rateLimitsStateStore
            ?? ModStateJsonStores.CreateDefault(JsonConfig, RateLimitManager.CONFIG_KEY);
        m_rateLimitsStateStore = rateStore;

        if (m_throughputManager != null)
        {
            m_throughputManager.SaveConfigState();
        }

        GroundwaterStatsManager.Instance?.SaveToStore();
    }

    private void onSaveDone(SaveResult result)
    {
        m_removalSaveLifecycle.AfterVanillaSave();
        LegacyStackerFullAlertManager.AfterSave();
    }

    private void RegisterAutoHelpersLocalizationLateApply(DependencyResolver resolver)
    {
        IGameLoopEvents gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            s_log.Info($"Blueprint Designer's Toolkit v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(DesignerToolkitMod).Assembly)}");
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

        string[] jsonFiles = Array.FindAll(
            Directory.GetFiles(translationsDirectory, "*.json", SearchOption.TopDirectoryOnly),
            filePath => !Path.GetFileName(filePath).StartsWith(".", StringComparison.Ordinal));
        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
        if (jsonFiles.Length == 0)
            s_log.Warning("Localization: no translation JSON files found.");
        else
            s_log.Info($"Localization: discovered {jsonFiles.Length} file(s): {string.Join(", ", jsonFiles)}");

        ModTranslationsApplyResult result = new ModTranslations().Apply(new ModTranslationsApplyOptions(
            translationsDirectory,
            typeof(DesignerToolkitMod).Assembly,
            Array.Empty<string>()));
        int refreshedKeybindCount = CoI.AutoHelpers.InputControl.CustomKeybindsInjector.RefreshRegisteredLocalizations();

        s_log.Info(
            $"Localization: applied locale='{result.AppliedLocaleCode}', upserted={result.UpsertedEntryCount}, scannedFields={result.ScannedFieldCount}, reboundFields={result.ReboundFieldCount}, refreshedKeybinds={refreshedKeybindCount}, readonlySkipped={result.SkippedReadonlyFieldCount}, missingTranslationSkipped={result.SkippedMissingTranslationFieldCount}, failedWrites={result.FailedFieldCount}, diagnostics={result.Diagnostics.Count}.");

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
        PipeColoring.Dispose();
        LegacyStackerFullAlertManager.Clear();
        TransportProductRemovalManager.Clear();

        if (m_instantBuildMode != null)
        {
            DesignerToolkitSettings.InstantBuildModeChanged -= m_instantBuildMode.OnSettingsChanged;
            m_instantBuildMode.Dispose();
            m_instantBuildMode = null;
        }

        if (m_transportCleanupTool != null)
        {
            m_transportCleanupTool.Dispose();
            m_transportCleanupTool = null;
        }

        if (m_throughputAoETool != null)
        {
            m_throughputAoETool.Dispose();
            m_throughputAoETool = null;
        }

        if (m_transportProductRemovalAoETool != null)
        {
            m_transportProductRemovalAoETool.Dispose();
            m_transportProductRemovalAoETool = null;
        }

        if (m_heightFilter != null)
        {
            m_heightFilter.Dispose();
            m_heightFilter = null;
        }

        if (m_undoManager != null)
        {
            m_undoManager.Dispose();
            m_undoManager = null;
        }

        RateLimitManager.Clear();

        if (m_throughputWorldRendererGo != null)
        {
            UnityEngine.Object.Destroy(m_throughputWorldRendererGo);
            m_throughputWorldRendererGo = null;
            m_throughputWorldRenderer = null;
        }

        if (m_throughputManager != null)
        {
            m_throughputManager.Dispose();
            m_throughputManager = null;
        }

        if (m_pollutionWorldRendererGo != null)
        {
            UnityEngine.Object.Destroy(m_pollutionWorldRendererGo);
            m_pollutionWorldRendererGo = null;
            m_pollutionWorldRenderer = null;
        }

        if (m_pollutionManager != null)
        {
            m_pollutionManager.Dispose();
            m_pollutionManager = null;
        }

        if (m_radiationWorldRendererGo != null)
        {
            UnityEngine.Object.Destroy(m_radiationWorldRendererGo);
            m_radiationWorldRendererGo = null;
            m_radiationWorldRenderer = null;
        }

        if (m_radiationManager != null)
        {
            m_radiationManager.Dispose();
            m_radiationManager = null;
        }

        removeFirstUnpauseHandler(m_gameLoopEvents);
        m_gameLoopEvents = null;

        if (m_simLoopEvents != null)
        {
            try { m_simLoopEvents.BeforeSave.RemoveNonSaveable(this, beforeSave); }
            catch { }
            m_simLoopEvents = null;
        }

        if (m_saveManager != null)
        {
            try { m_saveManager.OnSaveDone -= onSaveDone; }
            catch { }
            m_saveManager = null;
        }

        try { m_removalSaveLifecycle.DisposeRuntime(); }
        catch { }
    }

    private void InitializeNewGameOptions(DependencyResolver resolver, bool gameWasLoaded)
    {
        bool startNewGamePaused = JsonConfig.GetBool("startNewGamePaused", JsonConfig.GetBool("start_new_game_paused", true));
        string saveGameOnFirstDayAs = JsonConfig.GetString("saveGameOnFirstDayAs", JsonConfig.GetString("save_game_on_first_day_as", "Initial"));

        IGameLoopEvents gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            DesignerToolkitSettings.LoadFromStore();
            s_log.Info($"New game initialization (renderer init): startNewGamePaused={startNewGamePaused}, saveGameOnFirstDayAs='{saveGameOnFirstDayAs}', gameWasLoaded={gameWasLoaded}, isFirstUnpausePending={DesignerToolkitSettings.IsFirstUnpausePending}");

            if (!gameWasLoaded && startNewGamePaused)
            {
                try
                {
                    IInputScheduler inputScheduler = resolver.Resolve<IInputScheduler>();
                    inputScheduler.ScheduleInputCmd(new SetSimPauseStateCmd(isPaused: true));
                    s_log.Info("New game start (renderer init): scheduled initial pause command.");
                }
                catch (Exception ex)
                {
                    s_log.Exception(ex, "Failed to schedule initial pause command on new game start.");
                }
            }

            if (DesignerToolkitSettings.IsFirstUnpausePending && !string.IsNullOrWhiteSpace(saveGameOnFirstDayAs))
            {
                SetupFirstUnpauseAutosave(resolver, saveGameOnFirstDayAs.Trim(), !gameWasLoaded && startNewGamePaused);
            }
        });
    }

    private void SetupFirstUnpauseAutosave(DependencyResolver resolver, string saveName, bool startNewGamePaused)
    {
        IGameLoopEvents gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        ISimLoopEvents simLoopEvents = resolver.Resolve<ISimLoopEvents>();
        ISaveManager saveManager = resolver.Resolve<ISaveManager>();
        IFileSystemHelper fsHelper = resolver.Resolve<IFileSystemHelper>();
        GameNameConfig gameNameConfig = resolver.Resolve<GameNameConfig>();

        bool initialPauseConfirmed = !startNewGamePaused;
        bool saveRequested = false;

        m_firstUnpauseAutosaveHandler = _ =>
        {
            if (saveRequested)
                return;

            if (!initialPauseConfirmed)
            {
                if (simLoopEvents.IsSimPaused)
                {
                    initialPauseConfirmed = true;
                    s_log.Info("Initial game pause state confirmed active. Waiting for player unpause.");
                }
                return;
            }

            if (!simLoopEvents.IsSimPaused)
            {
                saveRequested = true;
                removeFirstUnpauseHandler(gameLoopEvents);

                string uniqueSaveName = GetUniqueSaveName(fsHelper, saveName, gameNameConfig.GameName);
                s_log.Info($"First unpause by player detected: requesting initial save '{uniqueSaveName}'.");
                try
                {
                    m_isInitialSaveInProgress = true;
                    DesignerToolkitSettings.IsFirstUnpausePending = true;
                    saveManager.RequestGameSave(uniqueSaveName);
                }
                catch (Exception ex)
                {
                    s_log.Exception(ex, $"Failed to request initial game save '{uniqueSaveName}'.");
                    m_isInitialSaveInProgress = false;
                    DesignerToolkitSettings.IsFirstUnpausePending = false;
                }
            }
        };

        gameLoopEvents.SyncUpdateStart.AddNonSaveable(this, m_firstUnpauseAutosaveHandler);
    }

    private static string GetUniqueSaveName(IFileSystemHelper fsHelper, string baseSaveName, string gameName)
    {
        string candidateName = baseSaveName;
        string saveFilePath = fsHelper.GetSaveFilePath(candidateName, gameName);
        int counter = 1;

        while (File.Exists(saveFilePath))
        {
            candidateName = $"{baseSaveName} ({counter})";
            saveFilePath = fsHelper.GetSaveFilePath(candidateName, gameName);
            counter++;
        }

        return candidateName;
    }

    private void removeFirstUnpauseHandler(IGameLoopEvents? gameLoopEvents)
    {
        if (m_firstUnpauseAutosaveHandler != null && gameLoopEvents != null)
        {
            try
            {
                gameLoopEvents.SyncUpdateStart.RemoveNonSaveable(this, m_firstUnpauseAutosaveHandler);
            }
            catch { }
            m_firstUnpauseAutosaveHandler = null;
        }
    }
}
