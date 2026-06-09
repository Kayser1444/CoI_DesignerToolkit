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
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Transports;
using Mafi.Core.GameLoop;
using Mafi.Core.Simulation;
using Mafi.Unity.Entities;
using CoI.AutoHelpers.Logging;
using UnityEngine;

namespace CoIDesignerToolkit;

/// <summary>
/// A designer tool that allows dragging a deletion box to clean up disconnected/stray transport segments
/// (belts and pipes) immediately. Highlights matches in red and removes them during the simulation update.
/// </summary>
internal sealed class TransportCleanupTool : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.TransportCleanup");
    private static readonly ColorRgba s_cleanupHighlight = new ColorRgba(230, 80, 80);

    private readonly EntitiesManager m_entitiesManager;
    private readonly IGameLoopEvents m_gameLoopEvents;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly EntityHighlighter m_highlighter;
    private readonly object m_pendingLock = new object();
    private readonly List<EntityId> m_pending = new List<EntityId>();
    private readonly List<Transport> m_matches = new List<Transport>();
    private readonly Dictionary<int, Transport> m_highlighted = new Dictionary<int, Transport>();

    private TransportCleanupOverlay? m_overlay;
    private Vector2 m_dragStart;
    private Vector2 m_dragCurrent;
    private bool m_isActive;
    private bool m_isDragging;
    private bool m_isSubscribed;

    public TransportCleanupTool(
        EntitiesManager entitiesManager,
        IGameLoopEvents gameLoopEvents,
        ISimLoopEvents simLoopEvents,
        EntityHighlighter highlighter)
    {
        m_entitiesManager = entitiesManager;
        m_gameLoopEvents = gameLoopEvents;
        m_simLoopEvents = simLoopEvents;
        m_highlighter = highlighter;
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        var overlayObject = new GameObject("BDT Transport Cleanup Overlay");
        UnityEngine.Object.DontDestroyOnLoad(overlayObject);
        m_overlay = overlayObject.AddComponent<TransportCleanupOverlay>();

        m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
        m_simLoopEvents.UpdateAfterCmdProc.AddNonSaveable(this, OnUpdateAfterCmdProc);
        m_isSubscribed = true;
        s_log.Info("Transport cleanup tool initialized. Hotkey: Alt+Del.");
    }

    public void Dispose()
    {
        if (m_isSubscribed)
        {
            try { m_gameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnInputUpdate); }
            catch { }
            try { m_simLoopEvents.UpdateAfterCmdProc.RemoveNonSaveable(this, OnUpdateAfterCmdProc); }
            catch { }
            m_isSubscribed = false;
        }

        Deactivate();

        if (m_overlay != null)
        {
            UnityEngine.Object.Destroy(m_overlay.gameObject);
            m_overlay = null;
        }
    }

    /// <summary>
    /// Handles user input for the cleanup tool, starting/updating/ending the selection box drag,
    /// and activating/deactivating the tool. Runs on the main UI/Input thread.
    /// </summary>
    private void OnInputUpdate(GameTime _)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Deactivate();
            return;
        }

        if (DesignerToolkitSettings.TransportCleanupHotkey.IsPressed())
        {
            Activate();
            return;
        }

        if (!m_isActive)
            return;

        if (Input.GetMouseButtonDown(1))
        {
            Deactivate();
            return;
        }

        if (!m_isDragging && Input.GetMouseButtonDown(0))
        {
            m_dragStart = MouseGuiPosition();
            m_dragCurrent = m_dragStart;
            m_isDragging = true;
            m_overlay?.Show(m_dragStart, m_dragCurrent);
            ComputeMatchesAndUpdateHighlights();
            return;
        }

        if (m_isDragging && Input.GetMouseButton(0))
        {
            m_dragCurrent = MouseGuiPosition();
            m_overlay?.SetRect(m_dragStart, m_dragCurrent);
            ComputeMatchesAndUpdateHighlights();
            return;
        }

        if (m_isDragging && Input.GetMouseButtonUp(0))
        {
            m_dragCurrent = MouseGuiPosition();
            m_overlay?.Hide();
            m_isDragging = false;
            EnqueueMatches();
            Deactivate();
        }
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

    private void Activate()
    {
        m_isActive = true;
        m_isDragging = false;
        m_overlay?.Hide();
        ClearHighlights();
        s_log.Info("Transport cleanup mode armed.");
    }

    private void Deactivate()
    {
        m_isActive = false;
        m_isDragging = false;
        m_overlay?.Hide();
        m_matches.Clear();
        ClearHighlights();
    }

    /// <summary>
    /// Computes which transports fall inside the screen-space selection rectangle. Converts 3D game coordinates
    /// to Unity world space and then to screen space.
    /// Note: Game coordinates (X, Y, height Z) map to Unity world space as (X*2, Z*2, Y*2), where vertical height
    /// is represented by the second component (Y).
    /// </summary>
    private void ComputeMatchesAndUpdateHighlights()
    {
        m_matches.Clear();

        float minX = Mathf.Min(m_dragStart.x, m_dragCurrent.x);
        float maxX = Mathf.Max(m_dragStart.x, m_dragCurrent.x);
        float minY = Mathf.Min(m_dragStart.y, m_dragCurrent.y);
        float maxY = Mathf.Max(m_dragStart.y, m_dragCurrent.y);

        Camera? camera = Camera.main;
        if (camera == null)
        {
            s_log.Warning("Transport cleanup selection skipped because Camera.main is not available.");
            ClearHighlights();
            return;
        }

        var wanted = new HashSet<int>();
        try
        {
            foreach (Transport transport in m_entitiesManager.GetAllEntitiesOfType<Transport>())
            {
                if (!CanCleanup(transport))
                    continue;

                Tile3i tile = transport.CenterTile;
                Vector3 worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
                Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0)
                    continue;

                float guiY = Screen.height - screenPos.y;
                if (screenPos.x < minX || screenPos.x > maxX || guiY < minY || guiY > maxY)
                    continue;

                m_matches.Add(transport);
                wanted.Add(transport.Id.Value);
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to collect transport cleanup matches.");
            m_matches.Clear();
            wanted.Clear();
        }

        SyncHighlights(wanted);
    }

    private void SyncHighlights(HashSet<int> wanted)
    {
        var toRemove = new List<int>();
        foreach (KeyValuePair<int, Transport> item in m_highlighted)
        {
            if (!wanted.Contains(item.Key) || item.Value.IsDestroyed)
                toRemove.Add(item.Key);
        }

        foreach (int id in toRemove)
        {
            m_highlighter.RemoveHighlight(m_highlighted[id]);
            m_highlighted.Remove(id);
        }

        foreach (Transport transport in m_matches)
        {
            int id = transport.Id.Value;
            if (m_highlighted.ContainsKey(id))
                continue;

            m_highlighter.Highlight(transport, s_cleanupHighlight);
            m_highlighted[id] = transport;
        }
    }

    private void ClearHighlights()
    {
        foreach (Transport transport in m_highlighted.Values)
        {
            if (!transport.IsDestroyed)
                m_highlighter.RemoveHighlight(transport);
        }

        m_highlighted.Clear();
    }

    private void EnqueueMatches()
    {
        if (m_matches.Count == 0)
        {
            s_log.Info("Transport cleanup selected no disconnected belts or pipes.");
            return;
        }

        lock (m_pendingLock)
        {
            foreach (Transport transport in m_matches)
                m_pending.Add(transport.Id);
        }

        s_log.Info($"Transport cleanup queued {m_matches.Count} disconnected belt/pipe segment(s).");
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

    private static Vector2 MouseGuiPosition()
    {
        return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }
}

internal sealed class TransportCleanupOverlay : MonoBehaviour
{
    private Texture2D? m_fillTexture;
    private Texture2D? m_borderTexture;
    private bool m_isActive;
    private Vector2 m_start;
    private Vector2 m_end;

    private void Awake()
    {
        m_fillTexture = new Texture2D(1, 1);
        m_fillTexture.SetPixel(0, 0, new Color(0.9f, 0.2f, 0.2f, 0.14f));
        m_fillTexture.Apply();

        m_borderTexture = new Texture2D(1, 1);
        m_borderTexture.SetPixel(0, 0, new Color(0.9f, 0.2f, 0.2f, 0.85f));
        m_borderTexture.Apply();
    }

    public void Show(Vector2 start, Vector2 end)
    {
        m_start = start;
        m_end = end;
        m_isActive = true;
    }

    public void SetRect(Vector2 start, Vector2 end)
    {
        m_start = start;
        m_end = end;
    }

    public void Hide()
    {
        m_isActive = false;
    }

    private void OnGUI()
    {
        if (!m_isActive || m_fillTexture == null || m_borderTexture == null)
            return;

        float x = Mathf.Min(m_start.x, m_end.x);
        float y = Mathf.Min(m_start.y, m_end.y);
        float width = Mathf.Abs(m_end.x - m_start.x);
        float height = Mathf.Abs(m_end.y - m_start.y);
        if (width < 2f && height < 2f)
            return;

        GUI.DrawTexture(new Rect(x, y, width, height), m_fillTexture);

        const float border = 2f;
        GUI.DrawTexture(new Rect(x, y, width, border), m_borderTexture);
        GUI.DrawTexture(new Rect(x, y + height - border, width, border), m_borderTexture);
        GUI.DrawTexture(new Rect(x, y, border, height), m_borderTexture);
        GUI.DrawTexture(new Rect(x + width - border, y, border, height), m_borderTexture);
    }

    private void OnDestroy()
    {
        if (m_fillTexture != null)
            Destroy(m_fillTexture);
        if (m_borderTexture != null)
            Destroy(m_borderTexture);
    }
}
