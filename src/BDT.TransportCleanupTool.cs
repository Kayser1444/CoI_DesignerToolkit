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
using CoI.AutoHelpers.InputControl;
using CoI.AutoHelpers.Logging;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.Terrain;
using Mafi.Unity.Ui;
using Mafi.Unity.UiStatic;
using Mafi.Unity.UiStatic.Cursors;
using UnityEngine;
using UnityEngine.EventSystems;
using EntityId = Mafi.Core.EntityId;

namespace CoIDesignerToolkit;

/// <summary>
/// Selects disconnected belt and pipe segments by their overlap with a terrain area.
/// Confirmed selections are removed on the simulation thread.
/// </summary>
internal sealed class TransportCleanupTool : IUnityInputController, IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.TransportCleanup");
    private static readonly ColorRgba s_cleanupHighlight = new ColorRgba(230, 80, 80);

    private readonly EntitiesManager m_entitiesManager;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly UiContext m_context;
    private readonly EntityHighlighter m_highlighter;
    private readonly AreaSelectionTool m_areaSelectionTool;
    private readonly Cursoor m_cursor;
    private readonly object m_pendingLock = new object();
    private readonly List<EntityId> m_pending = new List<EntityId>();
    private readonly HashSet<Transport> m_highlighted = new();
    private readonly HashSet<Transport> m_matches = new();
    private Vector3 m_dragStart;

    public ControllerConfig Config => ControllerConfig.Tool;

    public TransportCleanupTool(
        EntitiesManager entitiesManager,
        ISimLoopEvents simLoopEvents,
        UiContext context,
        AreaSelectionToolFactory areaSelectionToolFactory,
        CursorManager cursorManager,
        NewInstanceOf<EntityHighlighter> highlighter,
        NewInstanceOf<TerrainAreaOutlineRenderer> terrainOutlineRenderer)
    {
        m_entitiesManager = entitiesManager;
        m_simLoopEvents = simLoopEvents;
        m_context = context;
        m_highlighter = highlighter.Instance;
        m_cursor = cursorManager.RegisterCursor(CursorsStyles.SelectArea);
        m_areaSelectionTool = areaSelectionToolFactory.CreateInstance(
            terrainOutlineRenderer.Instance, UpdateSelection, FinishSelection, DeactivateSelf, DeactivateSelf);
        m_areaSelectionTool.SetLeftClickColor(s_cleanupHighlight);

        m_context.InputMgr.RegisterGlobalShortcut(
            CustomKeybindsInjector.GetHandle(nameof(HotkeysRegistry.TransportCleanup)), this);
        m_simLoopEvents.UpdateAfterCmdProc.AddNonSaveable(this, OnUpdateAfterCmdProc);
    }

    public void Dispose()
    {
        m_context.InputMgr.RemoveGlobalShortcut(this);
        DeactivateSelf();
        m_simLoopEvents.UpdateAfterCmdProc.RemoveNonSaveable(this, OnUpdateAfterCmdProc);
    }

    public void Activate()
    {
        m_cursor.Show();
        m_areaSelectionTool.TerrainCursor.Activate();
        HotkeysRegistry.PlayClickSound();
        s_log.Info("Transport cleanup mode armed.");
    }

    public void Deactivate()
    {
        m_areaSelectionTool.Deactivate();
        m_areaSelectionTool.TerrainCursor.Deactivate();
        m_cursor.Hide();
        m_highlighter.ClearAllHighlights();
        m_highlighted.Clear();
        m_matches.Clear();
    }

    public bool InputUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            return false;

        if (m_context.ShortcutsManager.IsSecondaryActionUp)
        {
            DeactivateSelf();
            return true;
        }

        if (m_areaSelectionTool.IsActive)
            return true;

        if (!m_context.ShortcutsManager.IsPrimaryActionDown ||
            EventSystem.current.IsPointerOverGameObject() ||
            !m_areaSelectionTool.TerrainCursor.HasValue)
        {
            return false;
        }

        m_dragStart = Input.mousePosition;
        m_areaSelectionTool.Activate(additionMode: true, m_dragStart);
        return true;
    }

    private void DeactivateSelf()
    {
        m_context.InputMgr.DeactivateController(this);
    }

    private IEnumerable<Transport> FindMatches(RectangleTerrainArea2i area)
    {
        foreach (Transport transport in m_entitiesManager.GetAllEntitiesOfType<Transport>())
        {
            if (CanCleanup(transport) && transport.IsSelected(area))
                yield return transport;
        }
    }

    private void UpdateSelection(RectangleTerrainArea2i area, bool isLeftClick)
    {
        m_matches.Clear();
        m_matches.UnionWith(FindMatches(area));
        foreach (Transport transport in m_highlighted)
        {
            if (!m_matches.Contains(transport))
                m_highlighter.RemoveHighlight(transport);
        }

        foreach (Transport transport in m_matches)
        {
            if (!m_highlighted.Contains(transport))
                m_highlighter.Highlight(transport, s_cleanupHighlight);
        }

        m_highlighted.Clear();
        m_highlighted.UnionWith(m_matches);
    }

    private void FinishSelection(RectangleTerrainArea2i area, bool isLeftClick)
    {
        // Require a drag so an ordinary click never confirms demolition.
        if (isLeftClick && (Input.mousePosition - m_dragStart).sqrMagnitude >= 4f &&
            !EventSystem.current.IsPointerOverGameObject())
        {
            // The mouse can move and release before the final preview sync.
            m_matches.Clear();
            m_matches.UnionWith(FindMatches(area));
            lock (m_pendingLock)
            {
                foreach (Transport transport in m_matches)
                    m_pending.Add(transport.Id);
            }
            s_log.Info($"Transport cleanup queued {m_matches.Count} disconnected belt/pipe segment(s).");
        }

        DeactivateSelf();
    }

    /// <summary>
    /// Processes enqueued deletion commands on the simulation thread to safely remove transport
    /// entities from the game world without causing threading or synchronization exceptions.
    /// </summary>
    private void OnUpdateAfterCmdProc()
    {
        List<EntityId> work;
        lock (m_pendingLock)
        {
            if (m_pending.Count == 0)
                return;

            work = new List<EntityId>(m_pending);
            m_pending.Clear();
        }

        ApplyPending(work);
    }

    /// <summary>
    /// Performs the actual entity deletion using <see cref="EntitiesManager.RemoveAndDestroyEntityNoChecks"/>.
    /// Executes on the simulation thread.
    /// </summary>
    private void ApplyPending(List<EntityId> ids)
    {
        int succeeded = 0;
        foreach (EntityId id in ids)
        {
            try
            {
                if (!m_entitiesManager.TryGetEntity(id, out Transport transport))
                    continue;

                if (!CanCleanup(transport))
                    continue;

                m_entitiesManager.RemoveAndDestroyEntityNoChecks(transport, EntityRemoveReason.Remove);
                succeeded++;
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to remove disconnected transport {id}: {ex.Message}");
            }
        }

        s_log.Info($"Transport cleanup removed {succeeded}/{ids.Count} disconnected belt/pipe segment(s).");
    }

    private static bool CanCleanup(Transport transport)
    {
        return !transport.IsDestroyed && !transport.IsFullyConnected;
    }

}
