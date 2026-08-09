using System;
using System.Collections.Generic;
using System.Globalization;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.GameLoop;
using Mafi.Core.Terrain;
using Mafi.Localization;
using Mafi.Unity.Entities;
using Mafi.Unity.UiToolkit;
using UnityEngine;
using UnityEngine.UIElements;
using EntityId = Mafi.Core.EntityId;

namespace CoIDesignerToolkit;

/// <summary>
/// World-space label/glow renderer for the daily unsafe-radioactive-inventory samples.
/// </summary>
public sealed class RadiationWorldRenderer : MonoBehaviour
{
    private static readonly Color RADIATION_GLOW_COLOR = new Color(0.20f, 1.00f, 0.25f, 1.00f);

    private IEntitiesManager? m_entitiesManager;
    private EntityHighlighter? m_highlighter;
    private IGameLoopEvents? m_gameLoopEvents;
    private bool m_isGameLoaded;
    private bool m_isSyncUpdateRegistered;
    private Texture2D? m_bgTexture;
    private Texture2D? m_glowTexture;
    private readonly HashSet<int> m_highlightedEntities = new HashSet<int>();
    private readonly List<IPanel> m_cachedPanels = new List<IPanel>();
    private int m_lastFrameCount = -1;

    public void Setup(IEntitiesManager entitiesManager, EntityHighlighter highlighter, IGameLoopEvents gameLoopEvents)
    {
        m_entitiesManager = entitiesManager;
        m_highlighter = highlighter;
        m_gameLoopEvents = gameLoopEvents;
        m_entitiesManager.EntityRemoved.AddNonSaveable(this, OnEntityRemoved);
        m_gameLoopEvents.SyncUpdate.AddNonSaveable(this, OnSyncUpdate);
        m_isSyncUpdateRegistered = true;
    }

    private void OnSyncUpdate(GameTime time)
    {
        m_isGameLoaded = true;
    }

    private void OnGUI()
    {
        if (!m_isGameLoaded ||
            (!DesignerToolkitSettings.RadiationOverlayEnabled && !DesignerToolkitSettings.RadiationGlowEnabled) ||
            RadiationManager.Instance == null)
        {
            ClearHighlights();
            return;
        }

        if (DesignerToolkitSettings.RadiationDaysToAverage == 0)
        {
            ClearHighlights();
            return;
        }

        Camera? camera = Camera.main;
        if (camera == null)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = UnityEngine.FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUIStyle backgroundStyle = new GUIStyle(GUI.skin.box);
        if (m_bgTexture == null)
        {
            m_bgTexture = new Texture2D(1, 1);
            m_bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            m_bgTexture.Apply();
        }
        backgroundStyle.normal.background = m_bgTexture;
        backgroundStyle.border = new RectOffset(0, 0, 0, 0);

        var drawTargets = new List<DrawTarget>();
        var states = RadiationManager.Instance.GetAllStates();
        // Radiation has a meaningful fixed zero baseline. Unlike throughput, the lowest
        // currently visible source must not be treated as the bottom of the heatmap.
        float globalMax = 0f;

        foreach (var pair in states)
        {
            EntityRadiationState state = pair.Value;
            if (state.CachedAverageRadiation <= 0f || m_entitiesManager == null)
                continue;
            if (!m_entitiesManager.TryGetEntity(new EntityId(pair.Key), out IEntity entity) || entity.IsDestroyed)
                continue;

            float value = state.CachedAverageRadiation;
            if (value > globalMax) globalMax = value;
            drawTargets.Add(new DrawTarget { Entity = entity, Radiation = value });
        }

        if (drawTargets.Count == 0)
        {
            ClearHighlights();
            return;
        }

        var currentHighlights = new HashSet<int>();
        var visibleTargets = new List<VisibleTarget>();
        foreach (DrawTarget target in drawTargets)
        {
            Vector3 worldPosition;
            if (target.Entity is IStaticEntity staticEntity)
            {
                Tile3i tile = staticEntity.CenterTile;
                worldPosition = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
            }
            else if (target.Entity is IEntityWithPosition positionedEntity)
            {
                Tile3f position = positionedEntity.Position3f;
                worldPosition = new Vector3(position.X.ToFloat() * 2f, position.Z.ToFloat() * 2f, position.Y.ToFloat() * 2f);
            }
            else
            {
                continue;
            }

            Vector3 entityScreenPosition = camera.WorldToScreenPoint(worldPosition);
            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition + Vector3.up * 1.3f);
            if (screenPosition.z < 0f)
                continue;

            float t = globalMax > 0f ? target.Radiation / globalMax : 0f;
            t = Mathf.Clamp01(t);
            Color textColor = InterpolateColor(t);
            float cameraHeight = camera.transform.position.y;
            float maxRadius = Mathf.Clamp(Mathf.Lerp(30f, 90f, (cameraHeight - 30f) / 200f), 30f, 90f);
            float radius = Mathf.Lerp(15f, maxRadius, t);
            float maxOpacity = Mathf.Clamp(Mathf.Lerp(0.15f, 0.85f, (cameraHeight - 30f) / 200f), 0.15f, 0.85f);
            float opacity = Mathf.Lerp(0.02f, maxOpacity, t);
            string text = "#" + target.Radiation.ToString("0.0", CultureInfo.InvariantCulture) + "#";
            Vector2 size = style.CalcSize(new GUIContent(text));

            if (DesignerToolkitSettings.RadiationGlowEnabled && m_highlighter != null && target.Entity is IRenderedEntity renderedEntity && t >= 0.1f)
            {
                try { m_highlighter.Highlight(renderedEntity, new ColorRgba(50, 255, 64, (int)(t * 215f))); } catch { }
                currentHighlights.Add(target.Entity.Id.Value);
            }

            visibleTargets.Add(new VisibleTarget
            {
                GuiX = screenPosition.x,
                GuiY = Screen.height - screenPosition.y,
                GlowGuiX = entityScreenPosition.x,
                GlowGuiY = Screen.height - entityScreenPosition.y,
                Width = size.x + 8f,
                Height = size.y + 2f,
                Text = text,
                TextColor = textColor,
                Radius = radius,
                Opacity = opacity,
                Radiation = target.Radiation
            });
        }

        if (DesignerToolkitSettings.RadiationGlowEnabled)
        {
            if (m_glowTexture == null)
                m_glowTexture = CreateGlowTexture(64);

            Color oldColor = GUI.color;
            foreach (VisibleTarget target in visibleTargets)
            {
                Color glowColor = RADIATION_GLOW_COLOR;
                glowColor.a = target.Opacity;
                GUI.color = glowColor;
                GUI.DrawTexture(
                    new Rect(target.GlowGuiX - target.Radius, target.GlowGuiY - target.Radius, target.Radius * 2f, target.Radius * 2f),
                    m_glowTexture);
            }
            GUI.color = oldColor;
        }

        if (DesignerToolkitSettings.RadiationOverlayEnabled)
        {
            foreach (VisibleTarget target in visibleTargets)
            {
                style.normal.textColor = target.TextColor;
                Rect rect = new Rect(target.GuiX - target.Width / 2f, target.GuiY - target.Height / 2f, target.Width, target.Height);
                GUI.Box(rect, GUIContent.none, backgroundStyle);
                GUI.Label(rect, target.Text, style);
            }
        }

        if (m_highlighter != null)
        {
            foreach (int id in m_highlightedEntities)
            {
                if (currentHighlights.Contains(id))
                    continue;
                if (m_entitiesManager != null && m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity entity) && !entity.IsDestroyed && entity is IRenderedEntity renderedEntity)
                {
                    try { m_highlighter.RemoveHighlight(renderedEntity); } catch { }
                }
            }

            m_highlightedEntities.Clear();
            foreach (int id in currentHighlights)
                m_highlightedEntities.Add(id);
        }
    }

    private void Update()
    {
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.RadiationOverlayToggle))
        {
            HotkeysRegistry.PlayClickSound();
            DesignerToolkitSettings.SetRadiationOverlayEnabled(!DesignerToolkitSettings.RadiationOverlayEnabled);
        }
    }

    private void ClearHighlights()
    {
        if (m_highlighter != null && m_entitiesManager != null)
        {
            foreach (int id in m_highlightedEntities)
            {
                if (m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity entity) && !entity.IsDestroyed && entity is IRenderedEntity renderedEntity)
                {
                    try { m_highlighter.RemoveHighlight(renderedEntity); } catch { }
                }
            }
        }
        m_highlightedEntities.Clear();
    }

    private void OnEntityRemoved(IEntity entity)
    {
        if (!m_highlightedEntities.Remove(entity.Id.Value) || m_highlighter == null)
            return;

        if (entity is IRenderedEntity renderedEntity)
        {
            try { m_highlighter.RemoveHighlight(renderedEntity); } catch { }
        }
    }

    private void UpdateCachedPanels()
    {
        int currentFrame = Time.frameCount;
        if (m_lastFrameCount == currentFrame)
            return;
        m_lastFrameCount = currentFrame;
        m_cachedPanels.Clear();

        var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(UnityEngine.FindObjectsSortMode.None);
        if (uiDocuments == null)
            return;
        foreach (UIDocument document in uiDocuments)
        {
            if (document != null && document.rootVisualElement != null && document.rootVisualElement.panel != null)
                m_cachedPanels.Add(document.rootVisualElement.panel);
        }
    }

    private bool IsPositionOverUI(Vector2 screenPosition)
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;

        var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
        pointerData.position = screenPosition;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        
        if (results.Count == 0) return false;

        UpdateCachedPanels();
        foreach (var panel in m_cachedPanels)
        {
            if (panel == null) continue;
            float scale = panel.scaledPixelsPerPoint;
            var localPos = new Vector2(screenPosition.x / scale, (UnityEngine.Screen.height - screenPosition.y) / scale);
            var picked = panel.Pick(localPos);
            if (picked != null)
            {
                var element = picked;
                while (element != null)
                {
                    if (element.ClassListContains("window") ||
                        element.ClassListContains("modal") ||
                        element.ClassListContains("panel") ||
                        element.ClassListContains("toolbar") ||
                        element.name.Contains("Window") ||
                        element.name.Contains("Panel"))
                    {
                        return true;
                    }
                    element = element.parent;
                }
                return false;
            }
        }

        return false;
    }

    private static Color InterpolateColor(float t)
    {
        return Color.Lerp(new Color(0.55f, 0.85f, 1f), new Color(1f, 0.12f, 0.08f), Mathf.Clamp01(t));
    }

    private Texture2D CreateGlowTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = distance > center ? 0f : Mathf.Clamp01(1f - distance / center);
                alpha = alpha * alpha * (3f - 2f * alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (m_entitiesManager != null)
        {
            try { m_entitiesManager.EntityRemoved.RemoveNonSaveable(this, OnEntityRemoved); } catch { }
        }

        if (m_gameLoopEvents != null && m_isSyncUpdateRegistered)
        {
            try { m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, OnSyncUpdate); } catch { }
            m_isSyncUpdateRegistered = false;
        }
        ClearHighlights();
        if (m_bgTexture != null) Destroy(m_bgTexture);
        if (m_glowTexture != null) Destroy(m_glowTexture);
        m_bgTexture = null;
        m_glowTexture = null;
    }

    private struct DrawTarget
    {
        public IEntity Entity;
        public float Radiation;
    }

    private struct VisibleTarget
    {
        public float GuiX;
        public float GuiY;
        public float GlowGuiX;
        public float GlowGuiY;
        public float Width;
        public float Height;
        public string Text;
        public Color TextColor;
        public float Radius;
        public float Opacity;
        public float Radiation;
    }
}
