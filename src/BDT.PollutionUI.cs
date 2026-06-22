// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Entities.Ships;
using Mafi.Core.Trains;
using Mafi.Core.Vehicles;
using Mafi.Core.GameLoop;
using Mafi.Localization;
using Mafi.Unity.Entities;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoIDesignerToolkit;

public sealed class PollutionWorldRenderer : MonoBehaviour
{
    private static readonly CoI.AutoHelpers.Logging.ModLogger s_log = new CoI.AutoHelpers.Logging.ModLogger("BDT.PollutionWorldRenderer");

    private IEntitiesManager? m_entitiesManager;
    private EntityHighlighter? m_highlighter;
    private IGameLoopEvents? m_gameLoopEvents;

    private bool m_isGameLoaded;
    private Texture2D? m_bgTexture;
    private Texture2D? m_whiteTexture;
    private readonly HashSet<int> m_highlightedEntities = new HashSet<int>();
    private readonly List<IPanel> m_cachedPanels = new List<IPanel>();
    private int m_lastFrameCount = -1;

    public void Setup(IEntitiesManager entitiesManager, EntityHighlighter highlighter, IGameLoopEvents gameLoopEvents)
    {
        m_entitiesManager = entitiesManager;
        m_highlighter = highlighter;
        m_gameLoopEvents = gameLoopEvents;

        m_gameLoopEvents.SyncUpdate.AddNonSaveable(this, OnSyncUpdate);
    }

    private void OnSyncUpdate(Mafi.Core.GameTime time)
    {
        m_isGameLoaded = true;
        if (m_gameLoopEvents != null)
        {
            try { m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, OnSyncUpdate); } catch { }
        }
    }

    private void ClearHighlights()
    {
        if (m_highlighter == null || m_entitiesManager == null) return;

        foreach (int id in m_highlightedEntities)
        {
            if (m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity e) && !e.IsDestroyed)
            {
                if (e is IRenderedEntity re)
                {
                    try { m_highlighter.RemoveHighlight(re); } catch { }
                }
            }
        }
        m_highlightedEntities.Clear();
    }

    private void UpdateCachedPanels()
    {
        int currentFrame = Time.frameCount;
        if (m_lastFrameCount == currentFrame) return;
        m_lastFrameCount = currentFrame;

        m_cachedPanels.Clear();
        var uiDocs = UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(UnityEngine.FindObjectsSortMode.None);
        if (uiDocs != null)
        {
            foreach (var doc in uiDocs)
            {
                if (doc != null && doc.rootVisualElement != null && doc.rootVisualElement.panel != null)
                {
                    m_cachedPanels.Add(doc.rootVisualElement.panel);
                }
            }
        }
    }

    private bool IsPositionOverUI(Vector2 screenPos)
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;

        var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
        pointerData.position = screenPos;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        
        if (results.Count == 0) return false;

        UpdateCachedPanels();

        foreach (var panel in m_cachedPanels)
        {
            if (panel == null) continue;
            float scale = panel.scaledPixelsPerPoint;
            var localPos = new Vector2(screenPos.x / scale, (UnityEngine.Screen.height - screenPos.y) / scale);
            var picked = panel.Pick(localPos);
            if (picked != null)
            {
                var element = picked;
                while (element != null)
                {
                    if (element.ClassListContains("window") || 
                        element.ClassListContains("panel") || 
                        element.ClassListContains("panelHud") || 
                        element.ClassListContains("floater") || 
                        element.ClassListContains("frostedPanel"))
                    {
                        return true;
                    }
                    element = element.parent;
                }
                return false; // Found an element in this panel but it's not a blocking window/panel
            }
        }

        return false;
    }

    private static Color InterpolateColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color c1 = new Color(0.18f, 0.80f, 0.44f); // Green
        Color c2 = new Color(0.95f, 0.61f, 0.07f); // Orange
        Color c3 = new Color(0.91f, 0.30f, 0.24f); // Red

        if (t <= 0.5f)
        {
            return Color.Lerp(c1, c2, t * 2f);
        }
        else
        {
            return Color.Lerp(c2, c3, (t - 0.5f) * 2f);
        }
    }

    private struct RenderTarget
    {
        public IEntity Entity;
        public float AveragePollution;
        public PollutionManager.PollutionType Type;
    }

    private void OnGUI()
    {
        if (!m_isGameLoaded || (!DesignerToolkitSettings.PollutionOverlayEnabled && !DesignerToolkitSettings.PollutionGlowEnabled) || PollutionManager.Instance == null || m_entitiesManager == null)
        {
            ClearHighlights();
            return;
        }

        if (DesignerToolkitSettings.PollutionDaysToAverage == 0)
        {
            ClearHighlights();
            return;
        }

        Camera? camera = Camera.main;
        if (camera == null) return;

        // Overlay Style setup
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 11;
        style.fontStyle = UnityEngine.FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        // Shadow Background style
        GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
        if (m_bgTexture == null)
        {
            m_bgTexture = new Texture2D(1, 1);
            m_bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            m_bgTexture.Apply();
        }
        bgStyle.normal.background = m_bgTexture;
        bgStyle.border = new RectOffset(0, 0, 0, 0);

        var targets = new List<RenderTarget>();
        var states = PollutionManager.Instance.GetAllStates();

        // 1. Static targets (machines/outfalls) with recorded average > 0
        foreach (var kvp in states)
        {
            var state = kvp.Value;
            if (state.Type == PollutionManager.PollutionType.Air && !DesignerToolkitSettings.PollutionShowAir) continue;
            if (state.Type == PollutionManager.PollutionType.Ground && !DesignerToolkitSettings.PollutionShowGround) continue;

            if (state.Type == PollutionManager.PollutionType.Air || state.Type == PollutionManager.PollutionType.Ground)
            {
                if (state.CachedAveragePollution > 0f && m_entitiesManager.TryGetEntity(new EntityId(kvp.Key), out IEntity entity) && !entity.IsDestroyed)
                {
                    targets.Add(new RenderTarget { Entity = entity, AveragePollution = state.CachedAveragePollution, Type = state.Type });
                }
            }
        }

        // 2. Vehicles / Locomotives
        if (DesignerToolkitSettings.PollutionShowVehicle)
        {
            foreach (var v in m_entitiesManager.GetAllEntitiesOfType<Vehicle>())
            {
                if (v.IsDestroyed || !v.IsEnabled) continue;
                float avg = 0f;
                if (states.TryGetValue(v.Id.Value, out var state))
                {
                    avg = state.CachedAveragePollution;
                }
                targets.Add(new RenderTarget { Entity = v, AveragePollution = avg, Type = PollutionManager.PollutionType.Vehicle });
            }
            foreach (var l in m_entitiesManager.GetAllEntitiesOfType<Locomotive>())
            {
                if (l.IsDestroyed || !l.IsEnabled) continue;
                float avg = 0f;
                if (states.TryGetValue(l.Id.Value, out var state))
                {
                    avg = state.CachedAveragePollution;
                }
                targets.Add(new RenderTarget { Entity = l, AveragePollution = avg, Type = PollutionManager.PollutionType.Vehicle });
            }
        }

        // 3. Ships
        if (DesignerToolkitSettings.PollutionShowShip)
        {
            foreach (var s in m_entitiesManager.GetAllEntitiesOfType<Ship>())
            {
                if (s.IsDestroyed || !s.IsEnabled) continue;
                float avg = 0f;
                if (s is Mafi.Core.Buildings.Cargo.Ships.CargoShipV2 cargoShip)
                {
                    avg = PollutionManager.Instance?.GetShipPredictedPollution(cargoShip) ?? 0f;
                }
                targets.Add(new RenderTarget { Entity = s, AveragePollution = avg, Type = PollutionManager.PollutionType.Ship });
            }
        }

        if (targets.Count == 0)
        {
            ClearHighlights();
            return;
        }

        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        // Calculate min/max for relative scaling based on targets
        foreach (var target in targets)
        {
            float avg = target.AveragePollution;
            if (avg < minVal) minVal = avg;
            if (avg > maxVal) maxVal = avg;
        }

        var currentHighlights = new HashSet<int>();

        foreach (var target in targets)
        {
            IEntity entity = target.Entity;
            Vector3 worldPos;
            if (entity is IStaticEntity staticEntity)
            {
                Tile3i tile = staticEntity.CenterTile;
                worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
            }
            else if (entity is IEntityWithPosition ePos)
            {
                Tile3f pos = ePos.Position3f;
                worldPos = new Vector3(pos.X.ToFloat() * 2f, pos.Z.ToFloat() * 2f, pos.Y.ToFloat() * 2f);
            }
            else
            {
                continue;
            }

            // Adjust position vertically based on height to float nicely above
            worldPos.y += 1.3f;

            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue; // Skip behind camera
            if (IsPositionOverUI(screenPos)) continue;

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            float avg = target.AveragePollution;
            string text = avg.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo) + " / min";

            Vector2 size = style.CalcSize(new GUIContent(text));
            float width = size.x + 8f;
            float height = size.y + 2f;

            float t = (maxVal > minVal) ? (avg - minVal) / (maxVal - minVal) : 0f;
            Color textColor = InterpolateColor(t);
            style.normal.textColor = textColor;

            // Only highlight/glow entities that have avg > 0
            if (avg > 0f && m_highlighter != null && DesignerToolkitSettings.PollutionGlowEnabled && entity is IRenderedEntity renderedEntity)
            {
                int r = (int)(textColor.r * 255f);
                int g = (int)(textColor.g * 255f);
                int b = (int)(textColor.b * 255f);
                
                ColorRgba highlightColor = new ColorRgba(r, g, b, 150);
                try { m_highlighter.Highlight(renderedEntity, highlightColor); } catch { }
                currentHighlights.Add(entity.Id.Value);
            }

            if (DesignerToolkitSettings.PollutionOverlayEnabled)
            {
                Rect rect = new Rect(guiX - width / 2f, guiY - height / 2f, width, height);
                GUI.Box(rect, GUIContent.none, bgStyle);
                GUI.Label(rect, text, style);
            }
        }

        if (m_highlighter != null)
        {
            foreach (int id in m_highlightedEntities)
            {
                if (!currentHighlights.Contains(id))
                {
                    if (m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity e) && !e.IsDestroyed)
                    {
                        if (e is IRenderedEntity re)
                        {
                            try { m_highlighter.RemoveHighlight(re); } catch { }
                        }
                    }
                }
            }
            m_highlightedEntities.Clear();
            foreach (int id in currentHighlights)
            {
                m_highlightedEntities.Add(id);
            }
        }
    }

    private void Update()
    {
        if (DesignerToolkitSettings.PollutionOverlayToggleHotkey.IsPressed())
        {
            DesignerToolkitSettings.SetPollutionOverlayEnabled(!DesignerToolkitSettings.PollutionOverlayEnabled);
        }
    }

    private void OnDestroy()
    {
        if (m_gameLoopEvents != null)
        {
            try { m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, OnSyncUpdate); } catch { }
        }
        ClearHighlights();
        if (m_bgTexture != null)
        {
            Destroy(m_bgTexture);
            m_bgTexture = null;
        }
        if (m_whiteTexture != null)
        {
            Destroy(m_whiteTexture);
            m_whiteTexture = null;
        }
    }
}
