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
using Mafi.Core.Entities.Static;
using Mafi.Core.GameLoop;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using CoI.AutoHelpers.Logging;
using UnityEngine;

namespace CoIDesignerToolkit;

internal enum AreaUpgradeMode
{
    Upgrade,
    Downgrade,
}

internal sealed class AreaUpgradeTool : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.AreaUpgrade");

    private readonly EntitiesManager m_entitiesManager;
    private readonly UpgradesManager m_upgradesManager;
    private readonly IGameLoopEvents m_gameLoopEvents;
    private readonly ISimLoopEvents m_simLoopEvents;
    private readonly object m_pendingLock = new object();
    private readonly List<PendingAreaUpgrade> m_pending = new List<PendingAreaUpgrade>();
    private readonly List<EntityId> m_selectedIds = new List<EntityId>();

    private AreaUpgradeOverlay? m_overlay;
    private AreaUpgradeMode? m_activeMode;
    private Vector2 m_dragStart;
    private Vector2 m_dragCurrent;
    private bool m_isDragging;
    private bool m_isSubscribed;

    public AreaUpgradeTool(
        EntitiesManager entitiesManager,
        UpgradesManager upgradesManager,
        IGameLoopEvents gameLoopEvents,
        ISimLoopEvents simLoopEvents)
    {
        m_entitiesManager = entitiesManager;
        m_upgradesManager = upgradesManager;
        m_gameLoopEvents = gameLoopEvents;
        m_simLoopEvents = simLoopEvents;
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        var overlayObject = new GameObject("BDT Area Upgrade Overlay");
        UnityEngine.Object.DontDestroyOnLoad(overlayObject);
        m_overlay = overlayObject.AddComponent<AreaUpgradeOverlay>();

        m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
        m_simLoopEvents.UpdateAfterCmdProc.AddNonSaveable(this, OnUpdateAfterCmdProc);
        m_isSubscribed = true;
        s_log.Info("Area upgrade tool initialized. Hotkeys: Ctrl+PgUp upgrade, Ctrl+PgDn downgrade.");
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

    private void OnInputUpdate(GameTime _)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Deactivate();
            return;
        }

        if (DesignerToolkitSettings.AreaUpgradeHotkey.IsPressed())
        {
            Activate(AreaUpgradeMode.Upgrade);
            return;
        }

        if (DesignerToolkitSettings.AreaDowngradeHotkey.IsPressed())
        {
            Activate(AreaUpgradeMode.Downgrade);
            return;
        }

        if (!m_activeMode.HasValue)
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
            return;
        }

        if (m_isDragging && Input.GetMouseButton(0))
        {
            m_dragCurrent = MouseGuiPosition();
            m_overlay?.SetRect(m_dragStart, m_dragCurrent);
            return;
        }

        if (m_isDragging && Input.GetMouseButtonUp(0))
        {
            AreaUpgradeMode mode = m_activeMode.Value;
            m_dragCurrent = MouseGuiPosition();
            m_overlay?.Hide();
            m_isDragging = false;
            EnqueueSelection(mode, m_dragStart, m_dragCurrent);
            Deactivate();
        }
    }

    private void OnUpdateAfterCmdProc()
    {
        List<PendingAreaUpgrade> work;
        lock (m_pendingLock)
        {
            if (m_pending.Count == 0)
                return;

            work = new List<PendingAreaUpgrade>(m_pending);
            m_pending.Clear();
        }

        foreach (PendingAreaUpgrade pending in work)
            ApplyPending(pending);
    }

    private void Activate(AreaUpgradeMode mode)
    {
        m_activeMode = mode;
        m_isDragging = false;
        m_overlay?.Hide();
        s_log.Info($"{ModeLabel(mode)} area mode armed.");
    }

    private void Deactivate()
    {
        m_activeMode = null;
        m_isDragging = false;
        m_overlay?.Hide();
    }

    private void EnqueueSelection(AreaUpgradeMode mode, Vector2 start, Vector2 end)
    {
        m_selectedIds.Clear();
        CollectMatches(mode, start, end, m_selectedIds);

        if (m_selectedIds.Count == 0)
        {
            s_log.Info($"{ModeLabel(mode)} area selected no matching buildings.");
            return;
        }

        lock (m_pendingLock)
            m_pending.Add(new PendingAreaUpgrade(mode, new List<EntityId>(m_selectedIds)));

        s_log.Info($"{ModeLabel(mode)} area queued {m_selectedIds.Count} building(s).");
        m_selectedIds.Clear();
    }

    private void CollectMatches(AreaUpgradeMode mode, Vector2 start, Vector2 end, List<EntityId> ids)
    {
        float minX = Mathf.Min(start.x, end.x);
        float maxX = Mathf.Max(start.x, end.x);
        float minY = Mathf.Min(start.y, end.y);
        float maxY = Mathf.Max(start.y, end.y);

        Camera? camera = Camera.main;
        if (camera == null)
        {
            s_log.Warning("Area upgrade selection skipped because Camera.main is not available.");
            return;
        }

        try
        {
            foreach (IStaticEntity entity in m_entitiesManager.GetAllEntitiesOfType<IStaticEntity>())
            {
                if (!(entity is IUpgradableEntity upgradable) || entity.IsDestroyed)
                    continue;

                if (!CanApplyMode(upgradable, mode))
                    continue;

                Tile3i tile = entity.CenterTile;
                Vector3 worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
                Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0)
                    continue;

                float guiY = Screen.height - screenPos.y;
                if (screenPos.x >= minX && screenPos.x <= maxX && guiY >= minY && guiY <= maxY)
                    ids.Add(upgradable.Id);
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to collect area upgrade matches.");
            ids.Clear();
        }
    }

    private void ApplyPending(PendingAreaUpgrade pending)
    {
        int succeeded = 0;
        foreach (EntityId id in pending.EntityIds)
        {
            try
            {
                if (!m_entitiesManager.TryGetEntity(id, out IUpgradableEntity entity))
                    continue;

                if (entity.IsDestroyed || !CanApplyMode(entity, pending.Mode))
                    continue;

                Option<IProtoWithUpgrade> target = TargetFor(entity, pending.Mode);
                if (target.IsNone || !target.Value.IsUnlockedAndAvailable)
                    continue;

                if (!m_upgradesManager.TryStartUpgrade(entity, target.Value, Option<EntityConfigData>.None, applyConfiguration: false))
                    continue;

                if (m_upgradesManager.TryFinishUpgradeImmediately(entity, payWithUnity: false, out string _))
                    succeeded++;
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to {ModeVerb(pending.Mode)} entity {id}: {ex.Message}");
            }
        }

        s_log.Info($"{ModeLabel(pending.Mode)} area applied to {succeeded}/{pending.EntityIds.Count} building(s).");
    }

    private bool CanApplyMode(IUpgradableEntity entity, AreaUpgradeMode mode)
    {
        if (entity.ConstructionState != ConstructionState.Constructed
            && entity.ConstructionState != ConstructionState.InConstruction)
            return false;

        Option<IProtoWithUpgrade> target = TargetFor(entity, mode);
        return target.HasValue && target.Value.IsUnlockedAndAvailable;
    }

    private Option<IProtoWithUpgrade> TargetFor(IUpgradableEntity entity, AreaUpgradeMode mode)
    {
        return mode == AreaUpgradeMode.Upgrade
            ? m_upgradesManager.GetNextTier(entity)
            : m_upgradesManager.GetPreviousTier(entity);
    }

    private static Vector2 MouseGuiPosition()
    {
        return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }

    private static string ModeLabel(AreaUpgradeMode mode)
    {
        return mode == AreaUpgradeMode.Upgrade ? "Upgrade" : "Downgrade";
    }

    private static string ModeVerb(AreaUpgradeMode mode)
    {
        return mode == AreaUpgradeMode.Upgrade ? "upgrade" : "downgrade";
    }

    private sealed class PendingAreaUpgrade
    {
        public readonly AreaUpgradeMode Mode;
        public readonly List<EntityId> EntityIds;

        public PendingAreaUpgrade(AreaUpgradeMode mode, List<EntityId> entityIds)
        {
            Mode = mode;
            EntityIds = entityIds;
        }
    }
}

internal sealed class AreaUpgradeOverlay : MonoBehaviour
{
    private Texture2D? m_fillTexture;
    private Texture2D? m_borderTexture;
    private bool m_isActive;
    private Vector2 m_start;
    private Vector2 m_end;

    private void Awake()
    {
        m_fillTexture = new Texture2D(1, 1);
        m_fillTexture.SetPixel(0, 0, new Color(0.15f, 0.65f, 1f, 0.14f));
        m_fillTexture.Apply();

        m_borderTexture = new Texture2D(1, 1);
        m_borderTexture.SetPixel(0, 0, new Color(0.15f, 0.65f, 1f, 0.85f));
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
