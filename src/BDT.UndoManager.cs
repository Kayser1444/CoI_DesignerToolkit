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
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.GameLoop;
using Mafi.Core.Input;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain.Designation;
using Mafi.Unity;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.InputControl;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal sealed class UndoRecord
{
    public readonly List<EntityId> PlacedEntityIds = new List<EntityId>();
    public readonly List<EntityConfigData> OverwrittenConfigs = new List<EntityConfigData>();
    public readonly List<IStaticEntity> OverwrittenDeconstructedEntities = new List<IStaticEntity>();
    public readonly List<TileSurfaceCopyPasteData> PastedSurfaces = new List<TileSurfaceCopyPasteData>();
    public readonly List<TileSurfaceCopyPasteData> PastedDecals = new List<TileSurfaceCopyPasteData>();

    public bool HasChanges()
    {
        return PlacedEntityIds.Count > 0
            || OverwrittenConfigs.Count > 0
            || OverwrittenDeconstructedEntities.Count > 0
            || PastedSurfaces.Count > 0
            || PastedDecals.Count > 0;
    }
}

internal sealed class UndoManager : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.UndoManager");

    private readonly EntitiesManager m_entitiesManager;
    private readonly IConstructionManager m_constructionManager;
    private readonly IInputScheduler m_inputScheduler;
    private readonly EntitiesCloneConfigHelper m_cloneConfigHelper;
    private readonly IGameLoopEvents m_gameLoopEvents;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly UiRoot m_uiRoot;

    private readonly List<UndoRecord> m_undoStack = new List<UndoRecord>();
    private const int MAX_UNDO_DEPTH = 20;

    private static UndoRecord? s_activeUndoRecord;
    private static int s_recordNestLevel;
    private static bool s_isUndoing;

    private readonly object m_lock = new object();
    private bool m_pendingUndo;
    private bool m_isSubscribed;

    public static bool IsRecording => s_activeUndoRecord != null && !s_isUndoing;

    public UndoManager(
        EntitiesManager entitiesManager,
        IConstructionManager constructionManager,
        IInputScheduler inputScheduler,
        EntitiesCloneConfigHelper cloneConfigHelper,
        IGameLoopEvents gameLoopEvents,
        ISimLoopEvents simLoopEvents,
        UiRoot uiRoot)
    {
        m_entitiesManager = entitiesManager;
        m_constructionManager = constructionManager;
        m_inputScheduler = inputScheduler;
        m_cloneConfigHelper = cloneConfigHelper;
        m_gameLoopEvents = gameLoopEvents;
        m_simLoopEvents = simLoopEvents;
        m_uiRoot = uiRoot;
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
        m_simLoopEvents.UpdateAfterCmdProc.AddNonSaveable(this, OnUpdateAfterCmdProc);
        m_isSubscribed = true;

        s_instance = this;
        s_log.Info("UndoManager initialized.");
    }

    public void Dispose()
    {
        if (!m_isSubscribed)
            return;

        try { m_gameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnInputUpdate); }
        catch { }
        try { m_simLoopEvents.UpdateAfterCmdProc.RemoveNonSaveable(this, OnUpdateAfterCmdProc); }
        catch { }
        m_isSubscribed = false;

        if (s_instance == this)
        {
            s_instance = null;
        }

        Clear();
    }

    public void Clear()
    {
        m_undoStack.Clear();
        s_activeUndoRecord = null;
        s_recordNestLevel = 0;
    }

    public static void BeginRecord()
    {
        if (s_isUndoing) return;

        if (s_recordNestLevel == 0)
        {
            s_activeUndoRecord = new UndoRecord();
        }
        s_recordNestLevel++;
    }

    public static void EndRecord()
    {
        if (s_isUndoing) return;

        s_recordNestLevel--;
        if (s_recordNestLevel <= 0)
        {
            s_recordNestLevel = 0;
            if (s_activeUndoRecord != null)
            {
                if (s_activeUndoRecord.HasChanges())
                {
                    PushRecord(s_activeUndoRecord);
                }
                s_activeUndoRecord = null;
            }
        }
    }

    private static void PushRecord(UndoRecord record)
    {
        if (s_instance != null)
        {
            s_instance.Push(record);
        }
    }

    private void Push(UndoRecord record)
    {
        m_undoStack.Add(record);
        if (m_undoStack.Count > MAX_UNDO_DEPTH)
        {
            m_undoStack.RemoveAt(0);
        }
        s_log.Info($"Pushed Undo record. Stack size: {m_undoStack.Count}");
    }

    public static void RecordPlacedEntity(EntityId id)
    {
        if (s_activeUndoRecord != null && !s_activeUndoRecord.PlacedEntityIds.Contains(id))
        {
            s_activeUndoRecord.PlacedEntityIds.Add(id);
        }
    }

    public static void RecordRemovedEntity(IEntity entity)
    {
        if (s_activeUndoRecord != null && entity is IStaticEntity staticEntity)
        {
            try
            {
                if (s_instance != null)
                {
                    EntityConfigData config = s_instance.m_cloneConfigHelper.CreateConfigFrom(staticEntity);
                    s_activeUndoRecord.OverwrittenConfigs.Add(config);
                }
            }
            catch (Exception ex)
            {
                s_log.Exception(ex, $"Failed to clone config for removed entity: {staticEntity}");
            }
        }
    }

    public static void RecordDeconstructedEntity(IStaticEntity entity)
    {
        if (s_activeUndoRecord != null && !s_activeUndoRecord.OverwrittenDeconstructedEntities.Contains(entity))
        {
            s_activeUndoRecord.OverwrittenDeconstructedEntities.Add(entity);
            RecordRemovedEntity(entity);
        }
    }

    public static void RecordPastedSurfaces(ImmutableArray<TileSurfaceCopyPasteData> data)
    {
        if (s_activeUndoRecord != null)
        {
            foreach (var item in data)
            {
                s_activeUndoRecord.PastedSurfaces.Add(item);
            }
        }
    }

    public static void RecordPastedDecals(ImmutableArray<TileSurfaceCopyPasteData> data)
    {
        if (s_activeUndoRecord != null)
        {
            foreach (var item in data)
            {
                s_activeUndoRecord.PastedDecals.Add(item);
            }
        }
    }

    private void OnInputUpdate(GameTime _)
    {
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.UndoPlacement))
        {
            lock (m_lock)
            {
                if (m_undoStack.Count > 0)
                {
                    HotkeysRegistry.PlayClickSound();
                }
                m_pendingUndo = true;
            }
        }
    }

    private void OnUpdateAfterCmdProc()
    {
        bool performUndo = false;
        lock (m_lock)
        {
            if (m_pendingUndo)
            {
                m_pendingUndo = false;
                performUndo = true;
            }
        }

        if (performUndo)
        {
            PerformUndoSim();
        }
    }

    private void PerformUndoSim()
    {
        if (m_undoStack.Count == 0)
        {
            m_uiRoot.ToastNotifProvider.ShowFailure(BdtLocalization.UndoNoActionMessage.AsFormatted);
            return;
        }

        s_isUndoing = true;
        try
        {
            int index = m_undoStack.Count - 1;
            UndoRecord record = m_undoStack[index];
            m_undoStack.RemoveAt(index);

            // Revert placed entities
            foreach (EntityId id in record.PlacedEntityIds)
            {
                if (m_entitiesManager.TryGetEntity<IStaticEntity>(id, out var entity) && !entity.IsDestroyed)
                {
                    if (entity.ConstructionState == ConstructionState.InConstruction)
                    {
                        m_constructionManager.StartDeconstruction(entity, doNotCreateProducts: true);
                    }
                    else
                    {
                        if (m_constructionManager.IsInstaBuildEnabled || DesignerToolkitSettings.IsSandbox)
                        {
                            m_entitiesManager.RemoveAndDestroyEntityNoChecks(entity, EntityRemoveReason.Remove);
                        }
                        else
                        {
                            m_constructionManager.StartDeconstruction(entity, doNotCreateProducts: false);
                        }
                    }
                }
            }

            // Restore overwritten pre-existing deconstructed entities
            foreach (IStaticEntity entity in record.OverwrittenDeconstructedEntities)
            {
                if (!entity.IsDestroyed && (entity.ConstructionState == ConstructionState.InDeconstruction || entity.ConstructionState == ConstructionState.PendingDeconstruction))
                {
                    entity.AbortDeconstruction();
                }
            }

            // Restore overwritten pre-existing completely destroyed entities
            var configsToRestore = new List<EntityConfigData>();
            foreach (EntityConfigData config in record.OverwrittenConfigs)
            {
                if (config.OriginalEntityId.HasValue && !m_entitiesManager.GetEntity(config.OriginalEntityId.Value).HasValue)
                {
                    configsToRestore.Add(config);
                }
            }

            if (configsToRestore.Count > 0)
            {
                var cmd = new BatchCreateStaticEntitiesCmd(
                    configsToRestore.ToImmutableArray(),
                    BuildMiniZippersMode.DeferToProto,
                    isFree: m_constructionManager.IsInstaBuildEnabled,
                    allowValidationSuppression: true,
                    applyConfiguration: true
                );
                m_inputScheduler.ScheduleInputCmd(cmd);
            }

            // Revert pasted surface designations
            if (record.PastedSurfaces.Count > 0)
            {
                var cmd = new BatchRemoveSurfacePlacingDesignationsCmd(record.PastedSurfaces.ToImmutableArray());
                m_inputScheduler.ScheduleInputCmd(cmd);
            }

            // Revert pasted decals
            if (record.PastedDecals.Count > 0)
            {
                var cmd = new BatchRemoveSurfaceDecalCmd(record.PastedDecals.ToImmutableArray());
                m_inputScheduler.ScheduleInputCmd(cmd);
            }

            s_log.Info("Undo executed successfully.");
            m_uiRoot.ToastNotifProvider.ShowSuccess(BdtLocalization.UndoSuccessMessage.AsFormatted);
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to perform undo.");
        }
        finally
        {
            s_isUndoing = false;
        }
    }

    private static UndoManager? s_instance;
}
