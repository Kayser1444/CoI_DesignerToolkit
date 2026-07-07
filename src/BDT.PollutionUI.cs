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
    private readonly List<IEntity> m_cachedMovingEntities = new List<IEntity>();
    private bool m_isSyncUpdateRegistered;
    private Texture2D? m_bgTexture;
    private Texture2D? m_whiteTexture;
    private Texture2D? m_glowTexture;
    private readonly HashSet<int> m_highlightedEntities = new HashSet<int>();
    private readonly List<IPanel> m_cachedPanels = new List<IPanel>();
    private int m_lastFrameCount = -1;

    public void Setup(IEntitiesManager entitiesManager, EntityHighlighter highlighter, IGameLoopEvents gameLoopEvents)
    {
        m_entitiesManager = entitiesManager;
        m_highlighter = highlighter;
        m_gameLoopEvents = gameLoopEvents;

        m_gameLoopEvents.SyncUpdate.AddNonSaveable(this, OnSyncUpdate);
        m_isSyncUpdateRegistered = true;
    }

    private void OnSyncUpdate(Mafi.Core.GameTime time)
    {
        m_isGameLoaded = true;

        // Cache active moving entities on the main thread during a safe update step
        m_cachedMovingEntities.Clear();
        if (m_entitiesManager != null && (DesignerToolkitSettings.PollutionOverlayEnabled || DesignerToolkitSettings.PollutionGlowEnabled))
        {
            if (DesignerToolkitSettings.PollutionShowVehicle)
            {
                foreach (var v in m_entitiesManager.GetAllEntitiesOfType<Vehicle>())
                {
                    if (v.IsDestroyed || !v.IsEnabled) continue;
                    m_cachedMovingEntities.Add(v);
                }
                foreach (var l in m_entitiesManager.GetAllEntitiesOfType<Locomotive>())
                {
                    if (l.IsDestroyed || !l.IsEnabled) continue;
                    m_cachedMovingEntities.Add(l);
                }
            }
            if (DesignerToolkitSettings.PollutionShowShip)
            {
                foreach (var s in m_entitiesManager.GetAllEntitiesOfType<Ship>())
                {
                    if (s.IsDestroyed || !s.IsEnabled) continue;
                    m_cachedMovingEntities.Add(s);
                }
            }
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

        // 2 & 3. Cached moving entities (Vehicles, Locomotives, Ships)
        foreach (var entity in m_cachedMovingEntities)
        {
            if (entity.IsDestroyed) continue;
            float avg = 0f;
            if (states.TryGetValue(entity.Id.Value, out var state))
            {
                avg = state.CachedAveragePollution;
            }

            var type = (entity is Ship) ? PollutionManager.PollutionType.Ship : PollutionManager.PollutionType.Vehicle;
            targets.Add(new RenderTarget { Entity = entity, AveragePollution = avg, Type = type });
        }

        if (targets.Count == 0)
        {
            ClearHighlights();
            return;
        }

        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;

        // Calculate min/max for relative scaling using a common pool across all active/toggled-on layers
        foreach (var target in targets)
        {
            float avg = target.AveragePollution;
            if (avg < globalMin) globalMin = avg;
            if (avg > globalMax) globalMax = avg;
        }

        var currentHighlights = new HashSet<int>();

        var drawTargets = new List<DrawTarget>();

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

            float t = (globalMax > globalMin) ? (avg - globalMin) / (globalMax - globalMin) : (globalMax > 0f ? 1f : 0f);
            Color textColor = InterpolateColor(t);

            // Only highlight/glow entities that have avg > 0
            if (avg > 0f && m_highlighter != null && DesignerToolkitSettings.PollutionGlowEnabled && entity is IRenderedEntity renderedEntity)
            {
                // The game's 3D selection outline replacement shader does not support alpha blending (rendering a solid silhouette),
                // so we skip highlighting minor polluters (t < 0.1) entirely to prevent them from generating massive halos.
                if (t >= 0.1f)
                {
                    int alpha = (int)(t * 215f);
                    ColorRgba highlightColor = new ColorRgba(255, 255, 255, alpha);
                    try { m_highlighter.Highlight(renderedEntity, highlightColor); } catch { }
                    currentHighlights.Add(entity.Id.Value);
                }
            }

            float heightVal = camera.transform.position.y;
            
            // Scale radius based on both camera height and relative pollution level t
            float minRadius = 15f;
            float maxRadiusVal = Mathf.Lerp(30f, 90f, (heightVal - 30f) / 200f);
            maxRadiusVal = Mathf.Clamp(maxRadiusVal, 30f, 90f);
            float radius = Mathf.Lerp(minRadius, maxRadiusVal, t);

            // Scale opacity based on both camera height and relative pollution level t
            float maxOpacityVal = Mathf.Lerp(0.15f, 0.85f, (heightVal - 30f) / 200f);
            maxOpacityVal = Mathf.Clamp(maxOpacityVal, 0.15f, 0.85f);
            float opacity = Mathf.Lerp(0.02f, maxOpacityVal, t);

            drawTargets.Add(new DrawTarget
            {
                GuiX = guiX,
                GuiY = guiY,
                Width = width,
                Height = height,
                Text = text,
                TextColor = textColor,
                Avg = avg,
                Radius = radius,
                Opacity = opacity
            });
        }

        // Pass 1: Draw all 2D screen-space glow textures (drawn at the bottom layer)
        if (DesignerToolkitSettings.PollutionGlowEnabled)
        {
            if (m_glowTexture == null)
            {
                m_glowTexture = CreateGlowTexture(64);
            }

            Color oldColor = GUI.color;
            foreach (var dt in drawTargets)
            {
                if (dt.Avg > 0f)
                {
                    Rect glowRect = new Rect(dt.GuiX - dt.Radius, dt.GuiY - dt.Radius, dt.Radius * 2f, dt.Radius * 2f);
                    GUI.color = new Color(1f, 1f, 1f, dt.Opacity);
                    GUI.DrawTexture(glowRect, m_glowTexture);
                }
            }
            GUI.color = oldColor;
        }

        // Pass 2: Draw all overlay text boxes (drawn on top of all glows)
        if (DesignerToolkitSettings.PollutionOverlayEnabled)
        {
            foreach (var dt in drawTargets)
            {
                style.normal.textColor = dt.TextColor;
                Rect rect = new Rect(dt.GuiX - dt.Width / 2f, dt.GuiY - dt.Height / 2f, dt.Width, dt.Height);
                GUI.Box(rect, GUIContent.none, bgStyle);
                GUI.Label(rect, dt.Text, style);
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

            // Glow parameters are no longer globally overridden, so no restoration is needed.
        }
    }

    private void Update()
    {
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.PollutionOverlayToggle))
        {
            DesignerToolkitSettings.SetPollutionOverlayEnabled(!DesignerToolkitSettings.PollutionOverlayEnabled);
        }
    }

    private Texture2D CreateGlowTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float maxDist = size / 2f;
                if (dist > maxDist)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // Radial falloff with smoothstep
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private void OnDestroy()
    {
        if (m_gameLoopEvents != null && m_isSyncUpdateRegistered)
        {
            try { m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, OnSyncUpdate); } catch { }
            m_isSyncUpdateRegistered = false;
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
        if (m_glowTexture != null)
        {
            Destroy(m_glowTexture);
            m_glowTexture = null;
        }
    }

    private struct DrawTarget
    {
        public float GuiX;
        public float GuiY;
        public float Width;
        public float Height;
        public string Text;
        public Color TextColor;
        public float Avg;
        public float Radius;
        public float Opacity;
    }
}
