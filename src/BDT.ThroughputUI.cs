// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Core.Ports;
using Mafi.Core.Ports.Io;
using Mafi.Unity.Entities;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class ThroughputUI
{
    private static readonly CoI.AutoHelpers.Logging.ModLogger s_log = new CoI.AutoHelpers.Logging.ModLogger("BDT.ThroughputUI");

    internal static float GetCapacityPerMinute(IEntity entity)
    {
        try
        {
            return GetCapacityPerMinuteInternal(entity);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in GetCapacityPerMinute: {ex}");
            return 0f;
        }
    }

    private static float GetCapacityPerMinuteInternal(IEntity entity)
    {
        if (entity is Mafi.Core.Factory.Transports.Transport transport)
        {
            return transport.Prototype.ThroughputPerTick.Value.ToFloat() * 600f;
        }
        if (entity is Mafi.Core.Factory.Lifts.Lift lift)
        {
            // Lifts (both conveyor/flat and loose) have a maximum throughput capacity of 1,200/min 
            // when connected to high-capacity targets (sorting plants, source/sinks, balancers, etc.). 
            // We use a flat 1,200 here to give designers a true view of the lift's capability.
            return 1200f;
        }
        if (entity is Mafi.Core.Factory.Zippers.Zipper || 
            entity is Mafi.Core.Factory.Zippers.MiniZipper)
        {
            return 5400f;
        }
        if (entity is Mafi.Core.Factory.Sorters.Sorter)
        {
            return 1800f;
        }
        if (entity is IEntityWithPorts entityWithPorts)
        {
            float inputSum = 0f;
            float outputSum = 0f;

            foreach (var port in entityWithPorts.Ports)
            {
                if (port.IsConnected)
                {
                    float cap = port.GetMaxThroughputPerTick().Value.ToFloat() * 600f;
                    if (port.IsConnectedAsInput)
                    {
                        inputSum += cap;
                    }
                    else if (port.IsConnectedAsOutput)
                    {
                        outputSum += cap;
                    }
                }
            }

            if (inputSum > 0f && outputSum > 0f)
            {
                return Math.Min(inputSum, outputSum);
            }
            else
            {
                return Math.Max(inputSum, outputSum);
            }
        }
        return 0f;
    }
    public static PanelWithHeader BuildPanel(UiComponent inspector, Func<IEntity> getEntity)
    {
        var panel = new PanelWithHeader().Title(BdtLocalization.ThroughputTitle);
        var col = new Column(2.pt()).AlignItemsStretch();

        // --- MONITOR ROW ---
        var monitorRow = new Row(2.pt()).AlignItemsCenter();
        var monitorToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.ThroughputDisplay);

        var monitorSpacer = new UiComponent().FlexGrow(1f);
        var monitorInputRow = new Row(2.pt()).AlignItemsCenter();

        var monitorMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var monitorPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        var monitorInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(35.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(monitorInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        
        var monitorUnitsLabel = new Label(BdtLocalization.ThroughputDaysToAverage)
            .Color(Theme.InactiveColor)
            .MarginRight(6.px());

        monitorInputRow.Add(monitorUnitsLabel);
        monitorInputRow.Add(monitorMinusBtn);
        monitorInputRow.Add(monitorInput);
        monitorInputRow.Add(monitorPlusBtn);

        monitorRow.Add(monitorToggle);
        monitorRow.Add(monitorSpacer);
        monitorRow.Add(monitorInputRow);

        // --- LIMIT ROW ---
        var limitRow = new Row(2.pt()).AlignItemsCenter();
        var limitToggle = new Toggle(standalone: true)
            .Label(BdtLocalization.RateLimitEnable);

        var limitSpacer = new UiComponent().FlexGrow(1f);
        var limitInputRow = new Row(2.pt()).AlignItemsCenter();

        var limitMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var limitPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        var limitInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(50.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(limitInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        
        var limitUnitsLabel = new Label(BdtLocalization.RateLimitItemsPerMin)
            .Color(Theme.InactiveColor)
            .MarginRight(6.px());

        limitInputRow.Add(limitUnitsLabel);
        limitInputRow.Add(limitMinusBtn);
        limitInputRow.Add(limitInput);
        limitInputRow.Add(limitPlusBtn);

        limitRow.Add(limitToggle);
        limitRow.Add(limitSpacer);
        limitRow.Add(limitInputRow);

        col.Add(monitorRow);
        col.Add(limitRow);
        panel.BodyAdd(col);

        int lastKnownLimit = 0;

        inspector.Observe(getEntity).Do(entity =>
        {
            if (entity == null || entity.IsDestroyed)
            {
                panel.Hide();
                return;
            }

            bool canMonitor = entity is Mafi.Core.Factory.Transports.Transport || 
                             entity is Mafi.Core.Factory.Lifts.Lift || 
                             entity is Mafi.Core.Factory.Zippers.Zipper || 
                             entity is Mafi.Core.Factory.Sorters.Sorter || 
                             entity is Mafi.Core.Factory.Zippers.MiniZipper || 
                             entity is Mafi.Base.Prototypes.Sandbox.ProductsSourceEntity || 
                             entity is Mafi.Base.Prototypes.Sandbox.ProductsSinkEntity || 
                             entity is Mafi.Base.Prototypes.Buildings.UniversalProductsSource || 
                             entity is Mafi.Base.Prototypes.Buildings.UniversalProductsSink;

            bool canLimit = entity is Mafi.Core.Factory.Transports.Transport || 
                           entity is Mafi.Base.Prototypes.Sandbox.ProductsSourceEntity || 
                           entity is Mafi.Base.Prototypes.Sandbox.ProductsSinkEntity;

            if (!canMonitor && !canLimit)
            {
                panel.Hide();
                return;
            }

            panel.Show();
            monitorRow.Visible(canMonitor);
            limitRow.Visible(canLimit);

            if (canMonitor)
            {
                var manager = ThroughputManager.Instance;
                if (manager != null)
                {
                    var state = manager.GetOrCreateState(entity.Id.Value);
                    monitorToggle.Value(state.DisplayThroughput);
                    monitorInput.Text(state.DaysToAverage.ToString());
                }
            }

            if (canLimit)
            {
                int defaultMax = 5000;
                if (entity is Mafi.Core.Factory.Transports.Transport transport)
                    defaultMax = transport.Prototype.ThroughputPer60.Value;

                var currentLimitOpt = RateLimitManager.GetLimit(entity.Id);
                bool isLimitEnabled = currentLimitOpt.HasValue && currentLimitOpt.Value > 0;
                int currentLimit = currentLimitOpt.HasValue ? currentLimitOpt.Value : 0;

                if (currentLimit > 0)
                {
                    lastKnownLimit = currentLimit;
                }

                int toShow = isLimitEnabled ? currentLimit : (lastKnownLimit > 0 ? lastKnownLimit : defaultMax);

                if (!DesignerToolkitSettings.IsSandbox)
                {
                    limitToggle.Enabled(false);
                    limitToggle.Tooltip(BdtLocalization.RateLimitSandboxOnly.AsFormatted);
                    limitMinusBtn.Enabled(false);
                    limitPlusBtn.Enabled(false);
                    limitInput.Enabled(false);
                }
                else
                {
                    limitToggle.Enabled(true);
                    limitToggle.Tooltip(null);
                    limitMinusBtn.Enabled(true);
                    limitPlusBtn.Enabled(true);
                    limitInput.Enabled(true);
                }

                limitToggle.Value(isLimitEnabled);
                limitInput.Text(toShow.ToString());
            }
        });

        Action<bool> updateDisplay = (val) =>
        {
            var entity = getEntity();
            if (entity == null || entity.IsDestroyed) return;

            var manager = ThroughputManager.Instance;
            if (manager == null) return;

            var state = manager.GetOrCreateState(entity.Id.Value);
            state.DisplayThroughput = val;
            manager.SaveConfigState();
        };

        Action<int> updateDays = (val) =>
        {
            var entity = getEntity();
            if (entity == null || entity.IsDestroyed) return;

            var manager = ThroughputManager.Instance;
            if (manager == null) return;

            var state = manager.GetOrCreateState(entity.Id.Value);
            state.DaysToAverage = Math.Max(1, Math.Min(360, val));
            state.RecalculateAverage();
            monitorInput.Text(state.DaysToAverage.ToString());
            manager.SaveConfigState();
        };

        monitorToggle.OnValueChanged(isOn => 
        {
            updateDisplay(isOn);
            if (isOn && !DesignerToolkitSettings.ThroughputOverlayEnabled)
            {
                DesignerToolkitSettings.SetThroughputOverlayEnabled(true);
            }
        });

        monitorInput.OnValueChanged((text) => 
        {
            if (int.TryParse(text, out int val))
            {
                if (!monitorToggle.GetValue())
                {
                    monitorToggle.Value(true);
                    updateDisplay(true);
                }
                updateDays(val);
            }
        });

        Action<int> adjustDays = (sign) =>
        {
            if (!monitorToggle.GetValue())
            {
                monitorToggle.Value(true);
                updateDisplay(true);
            }
            if (int.TryParse(monitorInput.GetText(), out int current))
            {
                int step = 1;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 10;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 5;

                int next = Math.Max(1, Math.Min(360, current + sign * step));
                updateDays(next);
            }
        };

        monitorMinusBtn.OnClick(() => adjustDays(-1), allowKeyPresses: true);
        monitorPlusBtn.OnClick(() => adjustDays(1), allowKeyPresses: true);

        Action<int> updateLimit = (val) =>
        {
            var entity = getEntity();
            if (entity == null || entity.IsDestroyed) return;

            int defaultMax = 5000;
            if (entity is Mafi.Core.Factory.Transports.Transport transport)
                defaultMax = transport.Prototype.ThroughputPer60.Value;

            if (val <= 0 || !limitToggle.GetValue())
            {
                RateLimitManager.RemoveLimit(entity.Id);
            }
            else
            {
                lastKnownLimit = val;
                RateLimitManager.SetLimit(entity.Id, val);
                limitInput.Text(val.ToString());
            }
        };

        limitToggle.OnValueChanged(isOn => 
        {
            if (isOn)
            {
                int defaultMax = 5000;
                var entity = getEntity();
                if (entity != null && entity is Mafi.Core.Factory.Transports.Transport transport)
                    defaultMax = transport.Prototype.ThroughputPer60.Value;

                int toSet = lastKnownLimit > 0 ? lastKnownLimit : defaultMax;
                updateLimit(toSet);
            }
            else
            {
                var entity = getEntity();
                if (entity != null && !entity.IsDestroyed) 
                    RateLimitManager.RemoveLimit(entity.Id);
            }
        });

        limitInput.OnValueChanged((text) => 
        {
            if (int.TryParse(text, out int val))
            {
                if (!limitToggle.GetValue())
                {
                    limitToggle.Value(true);
                }
                updateLimit(val);
            }
        });

        Action<int> adjustLimit = (sign) =>
        {
            if (!limitToggle.GetValue())
            {
                limitToggle.Value(true);
            }
            if (int.TryParse(limitInput.GetText(), out int current))
            {
                int step = 1;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 10;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 5;

                int next = Math.Max(1, current + sign * step);
                updateLimit(next);
            }
        };

        limitMinusBtn.OnClick(() => adjustLimit(-1), allowKeyPresses: true);
        limitPlusBtn.OnClick(() => adjustLimit(1), allowKeyPresses: true);

        return panel;
    }
}

public sealed class ThroughputWorldRenderer : MonoBehaviour
{
    private EntitiesManager? m_entitiesManager;
    private EntityHighlighter? m_highlighter;
    private Mafi.Core.GameLoop.IGameLoopEvents? m_gameLoopEvents;
    private bool m_isGameLoaded = false;
    private System.Collections.Generic.HashSet<int> m_highlightedEntities = new System.Collections.Generic.HashSet<int>();
    private Texture2D? m_bgTexture;
    private Texture2D? m_whiteTexture;
    private readonly System.Collections.Generic.List<UnityEngine.UIElements.IPanel> m_cachedPanels = new System.Collections.Generic.List<UnityEngine.UIElements.IPanel>();
    private int m_lastFrameCount = -1;

    public void Setup(EntitiesManager entitiesManager, EntityHighlighter highlighter, Mafi.Core.GameLoop.IGameLoopEvents gameLoopEvents)
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
        if (m_highlighter == null) return;
        foreach (int id in m_highlightedEntities)
        {
            if (m_entitiesManager != null && m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity e) && !e.IsDestroyed)
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


    private static Color InterpolateColor(float t, bool colorblind)
    {
        t = Mathf.Clamp01(t);
        Color c1 = colorblind ? new Color(0.20f, 0.60f, 0.86f) : new Color(0.18f, 0.80f, 0.44f); // Blue or Green
        Color c2 = colorblind ? new Color(0.95f, 0.77f, 0.06f) : new Color(0.95f, 0.61f, 0.07f); // Yellow or Orange
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

    private void OnGUI()
    {
        if (!m_isGameLoaded || (!DesignerToolkitSettings.ThroughputOverlayEnabled && !DesignerToolkitSettings.ThroughputGlowEnabled) || ThroughputManager.Instance == null || m_entitiesManager == null)
        {
            ClearHighlights();
            return;
        }

        var states = ThroughputManager.Instance.GetAllStates();
        if (states.Count == 0) 
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

        var heatmapMode = DesignerToolkitSettings.ThroughputHeatmapMode;
        bool colorblind = DesignerToolkitSettings.ThroughputColorblindMode;

        float minVal = float.MaxValue;
        float maxVal = float.MinValue;
        if (heatmapMode == ThroughputHeatmapMode.Relative)
        {
            foreach (var kvp in states)
            {
                var state = kvp.Value;
                if (!state.DisplayThroughput) continue;

                if (!m_entitiesManager.TryGetEntity(new EntityId(kvp.Key), out IEntity entity) || entity.IsDestroyed)
                    continue;

                if (!(entity is IStaticEntity staticEntity)) continue;

                Tile3i tile = staticEntity.CenterTile;
                Vector3 worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
                worldPos.y += 1.3f;

                Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) continue;
                if (IsPositionOverUI(screenPos)) continue;

                float avg = state.CachedAverageThroughput;
                if (avg < minVal) minVal = avg;
                if (avg > maxVal) maxVal = avg;
            }
        }

        var currentHighlights = new System.Collections.Generic.HashSet<int>();

        foreach (var kvp in states)
        {
            var state = kvp.Value;
            if (!state.DisplayThroughput) continue;

            if (!m_entitiesManager.TryGetEntity(new EntityId(kvp.Key), out IEntity entity) || entity.IsDestroyed)
                continue;

            if (!(entity is IStaticEntity staticEntity)) continue;
            Tile3i tile = staticEntity.CenterTile;
            Vector3 worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
            
            // Adjust position vertically based on height to float nicely above
            worldPos.y += 1.3f;

            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue; // Skip behind camera
            if (IsPositionOverUI(screenPos)) continue;

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            // Format number based on current game locale (decimal only if < 100)
            float avg = state.CachedAverageThroughput;
            float cap = ThroughputUI.GetCapacityPerMinute(entity);
            bool isOver98 = (cap > 0f) && (avg / cap > 0.98f);

            string text;
            if (DesignerToolkitSettings.ThroughputShowAsPercent)
            {
                if (cap > 0f)
                {
                    float pct = (avg / cap) * 100f;
                    if (pct < 100f)
                    {
                        text = pct.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo) + "%";
                    }
                    else
                    {
                        text = pct.ToString("0", Mafi.Localization.LocalizationManager.CurrentCultureInfo) + "%";
                    }
                }
                else
                {
                    if (avg < 100f)
                    {
                        text = avg.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo);
                    }
                    else
                    {
                        text = avg.ToString("0", Mafi.Localization.LocalizationManager.CurrentCultureInfo);
                    }
                }
            }
            else
            {
                if (avg < 100f)
                {
                    text = avg.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo);
                }
                else
                {
                    text = avg.ToString("0", Mafi.Localization.LocalizationManager.CurrentCultureInfo);
                }
            }

            Vector2 size = style.CalcSize(new GUIContent(text));
            float width = size.x + 8f;
            float height = size.y + 2f;

            Color textColor = Color.white;
            if (heatmapMode == ThroughputHeatmapMode.Relative)
            {
                float t = (maxVal > minVal) ? (avg - minVal) / (maxVal - minVal) : 0f;
                textColor = InterpolateColor(t, colorblind);
            }
            else if (heatmapMode == ThroughputHeatmapMode.Capacity)
            {
                float t = (cap > 0f) ? avg / cap : 0f;
                textColor = InterpolateColor(t, colorblind);
            }
            style.normal.textColor = textColor;

            if (m_highlighter != null && DesignerToolkitSettings.ThroughputGlowEnabled && entity is IRenderedEntity renderedEntity)
            {
                int r = (int)(textColor.r * 255f);
                int g = (int)(textColor.g * 255f);
                int b = (int)(textColor.b * 255f);
                
                // Maximum glow opacity for 98%+ to make them stand out, softer glow for normal entities
                int alpha = isOver98 ? 255 : 150;
                ColorRgba highlightColor = new ColorRgba(r, g, b, alpha);
                try { m_highlighter.Highlight(renderedEntity, highlightColor); } catch { }
                currentHighlights.Add(entity.Id.Value);
            }

            if (DesignerToolkitSettings.ThroughputOverlayEnabled)
            {
                Rect rect = new Rect(guiX - width / 2f, guiY - height / 2f, width, height);

                if (isOver98)
                {
                    if (m_whiteTexture == null)
                    {
                        m_whiteTexture = new Texture2D(1, 1);
                        m_whiteTexture.SetPixel(0, 0, Color.white);
                        m_whiteTexture.Apply();
                    }

                    float pulse = Mathf.PingPong(Time.time * 3f, 1f);
                    Color glowColor = new Color(1.0f, 0.15f, 0.0f, 0.25f + pulse * 0.45f);

                    Color origColor = GUI.color;
                    GUI.color = glowColor;
                    Rect glowRect = new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f);
                    GUI.DrawTexture(glowRect, m_whiteTexture);
                    GUI.color = origColor;
                }

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
                    if (m_entitiesManager != null && m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity e) && !e.IsDestroyed)
                    {
                        if (e is IRenderedEntity re)
                        {
                            try { m_highlighter.RemoveHighlight(re); } catch { }
                        }
                    }
                }
            }
            m_highlightedEntities = currentHighlights;
        }
    }

    private void Update()
    {
        if (DesignerToolkitSettings.ThroughputOverlayToggleHotkey.IsPressed())
        {
            DesignerToolkitSettings.SetThroughputOverlayEnabled(!DesignerToolkitSettings.ThroughputOverlayEnabled);
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
