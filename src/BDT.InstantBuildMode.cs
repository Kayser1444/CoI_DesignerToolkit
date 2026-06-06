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
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Simulation;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal sealed class InstantBuildMode : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.InstantBuild");

    private readonly EntitiesManager m_entitiesManager;
    private readonly IConstructionManager m_constructionManager;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly object? m_instaBuildManager;
    private readonly MethodInfo? m_setInstaBuildMethod;
    private readonly List<IStaticEntity> m_snapshot = new List<IStaticEntity>();

    private bool m_isSubscribed;

    public InstantBuildMode(
        EntitiesManager entitiesManager,
        IConstructionManager constructionManager,
        ISimLoopEvents simLoopEvents,
        object? instaBuildManager)
    {
        m_entitiesManager = entitiesManager;
        m_constructionManager = constructionManager;
        m_simLoopEvents = simLoopEvents;
        m_instaBuildManager = instaBuildManager;
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

        if (DesignerToolkitSettings.InstantBuildModeEnabled)
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
        if (enabled)
            DisableInstaBuildIfNeeded();
    }

    private void OnUpdateAfterCmdProc()
    {
        if (!DesignerToolkitSettings.InstantBuildModeEnabled)
            return;

        DrainConstructionStates();
    }

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
                if (state == ConstructionState.InConstruction || state == ConstructionState.InDeconstruction)
                    m_snapshot.Add(entity);
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
                    m_constructionManager.MarkDeconstructed(entity);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to finish construction state for entity '{entity}': {ex.Message}");
            }
        }

        m_snapshot.Clear();
    }

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
