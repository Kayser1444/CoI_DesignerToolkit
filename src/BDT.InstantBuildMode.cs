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
using System.Reflection;
using Mafi;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Game;
using Mafi.Core.Products;
using Mafi.Core.Simulation;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Controls the automatic instant construction, deconstruction, and upgrade mode in the blueprint designer.
/// When enabled, scans the active entities during the simulation tick and forces their completion immediately,
/// while disabling the game's native sandbox insta-build toggle to avoid conflicts.
/// </summary>
internal sealed class InstantBuildMode : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.InstantBuild");

    private readonly EntitiesManager m_entitiesManager;
    private readonly IConstructionManager m_constructionManager;
    private readonly UpgradesManager m_upgradesManager;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly object? m_instaBuildManager;
    private readonly MethodInfo? m_setInstaBuildMethod;
    private readonly GameDifficultyConfig m_difficultyConfig;
    private readonly List<IStaticEntity> m_snapshot = new List<IStaticEntity>();

    private bool m_isSubscribed;

    public InstantBuildMode(
        EntitiesManager entitiesManager,
        IConstructionManager constructionManager,
        UpgradesManager upgradesManager,
        ISimLoopEvents simLoopEvents,
        object? instaBuildManager,
        GameDifficultyConfig difficultyConfig)
    {
        m_entitiesManager = entitiesManager;
        m_constructionManager = constructionManager;
        m_upgradesManager = upgradesManager;
        m_simLoopEvents = simLoopEvents;
        m_instaBuildManager = instaBuildManager;
        m_difficultyConfig = difficultyConfig;
        m_setInstaBuildMethod = instaBuildManager?.GetType().GetMethod(
            "SetInstaBuild",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        m_simLoopEvents.UpdateAfterCmdProc.AddNonSaveable(this, OnUpdateAfterCmdProc);
        m_isSubscribed = true;

        if (DesignerToolkitSettings.InstantBuildModeEnabled && m_difficultyConfig.IsSandbox)
            DisableInstaBuildIfNeeded();

        s_log.Info("Instant build mode initialized.");
    }

    public void Dispose()
    {
        if (!m_isSubscribed)
            return;

        try { m_simLoopEvents.UpdateAfterCmdProc.RemoveNonSaveable(this, OnUpdateAfterCmdProc); }
        catch { }
        m_isSubscribed = false;
    }

    internal void OnSettingsChanged(bool enabled)
    {
        if (enabled && m_difficultyConfig.IsSandbox)
            DisableInstaBuildIfNeeded();
    }

    /// <summary>
    /// Invoked after simulation commands are processed. Scans and drains pending construction states if
    /// the tool is enabled and the map is a sandbox game.
    /// </summary>
    private void OnUpdateAfterCmdProc()
    {
        if (!DesignerToolkitSettings.InstantBuildModeEnabled || !m_difficultyConfig.IsSandbox)
            return;

        DrainConstructionStates();
    }

    /// <summary>
    /// Scans all static entities in the game, collects those in in-progress construction, deconstruction,
    /// or upgrade states, and immediately finishes them. Performs scanning in a snapshot first to avoid
    /// modifying the collection during iteration.
    /// </summary>
    private void DrainConstructionStates()
    {
        m_snapshot.Clear();

        try
        {
            foreach (IStaticEntity entity in m_entitiesManager.GetAllEntitiesOfType<IStaticEntity>())
            {
                if (entity == null || entity.IsDestroyed)
                    continue;

                ConstructionState state = entity.ConstructionState;
                if (state == ConstructionState.InConstruction || 
                    state == ConstructionState.InDeconstruction ||
                    state == ConstructionState.PendingDeconstruction ||
                    state == ConstructionState.PreparingUpgrade ||
                    state == ConstructionState.BeingUpgraded)
                {
                    m_snapshot.Add(entity);
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to scan static entities for instant build mode.");
            m_snapshot.Clear();
            return;
        }

        foreach (IStaticEntity entity in m_snapshot)
        {
            try
            {
                if (entity.IsDestroyed)
                    continue;

                if (entity.ConstructionState == ConstructionState.InConstruction)
                    m_constructionManager.MarkConstructed(entity);
                else if (entity.ConstructionState == ConstructionState.InDeconstruction)
                {
                    if (entity is StorageBase storageBase)
                        ClearStorageContents(storageBase);
                    m_constructionManager.MarkDeconstructed(entity);
                }
                else if (entity.ConstructionState == ConstructionState.PendingDeconstruction)
                {
                    if (entity is StorageBase storageBase)
                        ClearStorageContents(storageBase);
                }
                else if (entity.ConstructionState == ConstructionState.PreparingUpgrade || entity.ConstructionState == ConstructionState.BeingUpgraded)
                {
                    if (entity is IUpgradableEntity upgradableEntity)
                        m_upgradesManager.TryFinishUpgradeImmediately(upgradableEntity, payWithUnity: false, out string _);
                }
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to finish construction state for entity '{entity}': {ex.Message}");
            }
        }

        m_snapshot.Clear();
    }

    /// <summary>
    /// Clears any stored product buffer contents inside a StorageBase entity to allow immediate deconstruction.
    /// </summary>
    private void ClearStorageContents(StorageBase storageBase)
    {
        if (storageBase.Buffer.HasValue)
        {
            try
            {
                var buffer = storageBase.Buffer.Value;
                if (!buffer.IsEmpty)
                {
                    buffer.SetCleaningMode(isEnabled: false);
                    Quantity quantity = buffer.RemoveAll();
                    if (quantity.IsPositive)
                    {
                        GameApiCompat.ProductDestroyed(
                            storageBase.Context.ProductsManager,
                            buffer.Product,
                            quantity,
                            DestroyReason.Cleared);
                        s_log.Info($"InstantBuild: Cleared {quantity} of {buffer.Product} from storage {storageBase.Id} to allow deconstruction.");
                    }
                }
            }
            catch (Exception ex)
            {
                s_log.Exception(ex, $"Failed to clear storage contents for {storageBase.Id}");
            }
        }
    }

    /// <summary>
    /// Disables the game's built-in sandbox insta-build mode via reflection. Since BDT's instant build handles
    /// all designer placement actions customly, disabling vanilla insta-build prevents duplicate event handlers
    /// and conflicts.
    /// </summary>
    private void DisableInstaBuildIfNeeded()
    {
        if (!m_constructionManager.IsInstaBuildEnabled)
            return;

        if (m_instaBuildManager == null || m_setInstaBuildMethod == null)
        {
            s_log.Warning("Instant build mode is enabled, but the game insta-build manager could not be resolved.");
            return;
        }

        try
        {
            m_setInstaBuildMethod.Invoke(m_instaBuildManager, new object[] { false });
            s_log.Info("Disabled game insta-build while BDT instant build mode is enabled.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to disable game insta-build.");
        }
    }
}
