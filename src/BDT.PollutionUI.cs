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
using Mafi.Core.Terrain;
using Mafi.Core.Prototypes;
using Mafi.Core.Products;
using Mafi.Localization;
using Mafi.Unity.Entities;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;
using EntityId = Mafi.Core.EntityId;
using UnityEngine.UIElements;

namespace CoIDesignerToolkit;

public sealed class LandfillCluster
{
    public Vector3 CenterWorldPos;
    public int TileCount;
    public float TotalThickness;
    public readonly List<Vector3> TilePositions = new List<Vector3>();
}

public sealed class PollutionWorldRenderer : MonoBehaviour
{
    private static readonly CoI.AutoHelpers.Logging.ModLogger s_log = new CoI.AutoHelpers.Logging.ModLogger("BDT.PollutionWorldRenderer");

    private IEntitiesManager? m_entitiesManager;
    private EntityHighlighter? m_highlighter;
    private IGameLoopEvents? m_gameLoopEvents;
    private TerrainManager? m_terrainManager;
    private TerrainMaterialProto? m_landfillProto;
    private TerrainMaterialSlimId? m_landfillSlimId;
    private float m_landfillPollutionMultiplier = 1f;

    private bool m_isGameLoaded;
    private readonly List<IEntity> m_cachedMovingEntities = new List<IEntity>();
    private readonly List<LandfillCluster> m_cachedLandfillClusters = new List<LandfillCluster>();
    private readonly Dictionary<Tile2i, (Vector3 Pos, float Thickness)> m_tileMap = new Dictionary<Tile2i, (Vector3 Pos, float Thickness)>();
    private readonly HashSet<Tile2i> m_visitedSet = new HashSet<Tile2i>();
    private readonly Queue<Tile2i> m_bfsQueue = new Queue<Tile2i>();
    private int m_landfillScanCounter;
    private bool m_isSyncUpdateRegistered;
    private Texture2D? m_bgTexture;
    private Texture2D? m_whiteTexture;
    private Texture2D? m_glowTexture;
    private readonly HashSet<int> m_highlightedEntities = new HashSet<int>();
    private readonly List<IPanel> m_cachedPanels = new List<IPanel>();
    private int m_lastFrameCount = -1;

    public void Setup(IEntitiesManager entitiesManager, EntityHighlighter highlighter, IGameLoopEvents gameLoopEvents, TerrainManager? terrainManager = null, ProtosDb? protosDb = null, Mafi.Core.PropertiesDb.IPropertiesDb? propertiesDb = null)
    {
        m_entitiesManager = entitiesManager;
        m_highlighter = highlighter;
        m_gameLoopEvents = gameLoopEvents;
        m_terrainManager = terrainManager;

        if (protosDb != null)
        {
            if (protosDb.TryGetProto<TerrainMaterialProto>(IdsCore.TerrainMaterials.Landfill, out var landfillProto))
            {
                m_landfillProto = landfillProto;
                m_landfillSlimId = landfillProto.SlimId;
            }
        }

        if (propertiesDb != null)
        {
            try
            {
                m_landfillPollutionMultiplier = propertiesDb.GetProperty(IdsCore.PropertyIds.LandfillPollutionMultiplier).Value.ToFloat();
            }
            catch { }
        }

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

        // Cache active landfill terrain clusters (throttled to once every 10 ticks / ~0.5s to minimize CPU overhead)
        m_landfillScanCounter++;
        if (m_landfillScanCounter % 10 == 0)
        {
            m_cachedLandfillClusters.Clear();
            if (m_terrainManager != null && m_landfillSlimId.HasValue && DesignerToolkitSettings.PollutionShowSolidWaste && (DesignerToolkitSettings.PollutionOverlayEnabled || DesignerToolkitSettings.PollutionGlowEnabled))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camPos = cam.transform.position;
                    Vector3 camFwd = cam.transform.forward;
                    float distance = (camFwd.y != 0f) ? -camPos.y / camFwd.y : 0f;
                    if (distance < 0f) distance = 40f;
                    Vector3 focusPos = camPos + camFwd * distance;

                    int centerTileX = Mathf.Clamp((int)(focusPos.x / 2f), 0, m_terrainManager.TerrainSize.X - 1);
                    int centerTileY = Mathf.Clamp((int)(focusPos.z / 2f), 0, m_terrainManager.TerrainSize.Y - 1);

                    int radius = Mathf.Clamp((int)(camPos.y * 0.8f + 40f), 40, 120);

                    int minX = Math.Max(0, centerTileX - radius);
                    int maxX = Math.Min(m_terrainManager.TerrainSize.X - 1, centerTileX + radius);
                    int minY = Math.Max(0, centerTileY - radius);
                    int maxY = Math.Min(m_terrainManager.TerrainSize.Y - 1, centerTileY + radius);

                    TerrainMaterialSlimId targetSlimId = m_landfillSlimId.Value;
                    m_tileMap.Clear();

                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            var tileCoord = new Tile2i(x, y);
                            var tileIndex = m_terrainManager.GetTileIndex(tileCoord);
                            TileMaterialLayers layers = m_terrainManager.GetLayersRawData(tileIndex);
                            if (TryGetActiveLandfillThickness(layers, targetSlimId, out float activeThickness))
                            {
                                HeightTilesF height = m_terrainManager.GetHeight(tileIndex);
                                Vector3 worldPos = new Vector3(x * 2f + 1f, height.Value.ToFloat() * 2f + 0.3f, y * 2f + 1f);
                                m_tileMap[tileCoord] = (worldPos, activeThickness);
                            }
                        }
                    }

                    m_visitedSet.Clear();

                    foreach (var kvp in m_tileMap)
                    {
                        Tile2i startTile = kvp.Key;
                        if (m_visitedSet.Contains(startTile)) continue;

                        var cluster = new LandfillCluster();
                        m_bfsQueue.Clear();

                        m_bfsQueue.Enqueue(startTile);
                        m_visitedSet.Add(startTile);

                        Vector3 sumPos = Vector3.zero;

                        while (m_bfsQueue.Count > 0)
                        {
                            Tile2i current = m_bfsQueue.Dequeue();
                            var data = m_tileMap[current];

                            cluster.TilePositions.Add(data.Pos);
                            cluster.TotalThickness += data.Thickness;
                            cluster.TileCount++;
                            sumPos += data.Pos;

                            // Check 8-neighbor tiles plus up to 2-tile distance to bridge small gaps
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                for (int dy = -2; dy <= 2; dy++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    Tile2i neighbor = new Tile2i(current.X + dx, current.Y + dy);
                                    if (m_tileMap.ContainsKey(neighbor) && !m_visitedSet.Contains(neighbor))
                                    {
                                        m_visitedSet.Add(neighbor);
                                        m_bfsQueue.Enqueue(neighbor);
                                    }
                                }
                            }
                        }

                        if (cluster.TileCount > 0)
                        {
                            cluster.CenterWorldPos = sumPos / cluster.TileCount;
                            m_cachedLandfillClusters.Add(cluster);
                        }
                    }
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

    private static bool TryGetActiveLandfillThickness(TileMaterialLayers layers, TerrainMaterialSlimId targetSlimId, out float activeThickness)
    {
        activeThickness = 0f;
        if (layers.Count <= 0) return false;

        ThicknessTilesF coverDepth = ThicknessTilesF.Zero;
        ThicknessTilesF maxDepth = ThicknessTilesF.One; // MAX_RECOVERY_DEPTH = 1.0 tiles

        // Layer 1
        if (coverDepth < maxDepth)
        {
            if (layers.First.SlimId == targetSlimId && layers.First.Thickness.IsPositive)
            {
                activeThickness += layers.First.Thickness.Value.ToFloat();
            }
            coverDepth += layers.First.Thickness;
        }

        // Layer 2
        if (layers.Count >= 2 && coverDepth < maxDepth)
        {
            if (layers.Second.SlimId == targetSlimId && layers.Second.Thickness.IsPositive)
            {
                activeThickness += layers.Second.Thickness.Value.ToFloat();
            }
            coverDepth += layers.Second.Thickness;
        }

        // Layer 3
        if (layers.Count >= 3 && coverDepth < maxDepth)
        {
            if (layers.Third.SlimId == targetSlimId && layers.Third.Thickness.IsPositive)
            {
                activeThickness += layers.Third.Thickness.Value.ToFloat();
            }
            coverDepth += layers.Third.Thickness;
        }

        // Layer 4
        if (layers.Count >= 4 && coverDepth < maxDepth)
        {
            if (layers.Fourth.SlimId == targetSlimId && layers.Fourth.Thickness.IsPositive)
            {
                activeThickness += layers.Fourth.Thickness.Value.ToFloat();
            }
        }

        return activeThickness > 0f;
    }

    private static Color InterpolateColor(float t)
    {
        t = Mathf.Clamp01(t);

        // Keep pollution visually separate from the green/orange/red throughput
        // heatmap. The light end is pure white so the largest polluter remains
        // the strongest label, while the lower end uses a balanced light-grey for legibility.
        Color lowPollution = new Color(0.70f, 0.70f, 0.70f);
        return Color.Lerp(lowPollution, Color.white, t);
    }

    private struct RenderTarget
    {
        public IEntity? Entity;
        public Vector3? CustomWorldPos;
        public float AveragePollution;
        public PollutionManager.PollutionType Type;
        public string? CustomText;
        public LandfillCluster? Cluster;
    }

    private void OnGUI()
    {
        if (!m_isGameLoaded || (!DesignerToolkitSettings.PollutionOverlayEnabled && !DesignerToolkitSettings.PollutionGlowEnabled) || PollutionManager.Instance == null)
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
        if (m_entitiesManager != null)
        {
            foreach (var kvp in states)
            {
                var state = kvp.Value;
                if (state.Type == PollutionManager.PollutionType.Air && !DesignerToolkitSettings.PollutionShowAir) continue;
                if (state.Type == PollutionManager.PollutionType.Ground && !DesignerToolkitSettings.PollutionShowGround) continue;
                if (state.Type == PollutionManager.PollutionType.SolidWaste && !DesignerToolkitSettings.PollutionShowSolidWaste) continue;

                if (state.Type == PollutionManager.PollutionType.Air || state.Type == PollutionManager.PollutionType.Ground || state.Type == PollutionManager.PollutionType.SolidWaste)
                {
                    if (state.CachedAveragePollution > 0f && m_entitiesManager.TryGetEntity(new EntityId(kvp.Key), out IEntity entity) && !entity.IsDestroyed)
                    {
                        targets.Add(new RenderTarget { Entity = entity, AveragePollution = state.CachedAveragePollution, Type = state.Type });
                    }
                }
            }
        }

        // 2. Cached moving entities (Vehicles, Locomotives, Ships)
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

        // 3. Landfill terrain clusters
        if (DesignerToolkitSettings.PollutionShowSolidWaste)
        {
            float recoveryMonths = (m_landfillProto != null && m_landfillProto.DisruptionRecoveryTime.Months > Fix32.Zero) ? m_landfillProto.DisruptionRecoveryTime.Months.ToFloat() : 48f;
            foreach (var cluster in m_cachedLandfillClusters)
            {
                // Calculate quantity based on active landfill tile count (standard 1.0 tile thickness)
                float totalQty = (m_landfillProto != null) ? m_landfillProto.ThicknessToQuantity(new ThicknessTilesF(cluster.TileCount.ToFix32())).Value.ToFloat() : (cluster.TileCount * 10f);
                
                // Landfill weathers over its prototype DisruptionRecoveryTime (default 48 months / 4 years). The monthly pollution emission rate remains constant:
                float monthlyPollutionRate = (totalQty / recoveryMonths) * m_landfillPollutionMultiplier;
                string pollutionStr = monthlyPollutionRate.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo);

                targets.Add(new RenderTarget
                {
                    CustomWorldPos = cluster.CenterWorldPos,
                    AveragePollution = monthlyPollutionRate,
                    Type = PollutionManager.PollutionType.SolidWaste,
                    CustomText = $"[{pollutionStr}({cluster.TileCount})]",
                    Cluster = cluster
                });
            }
        }

        if (targets.Count == 0)
        {
            ClearHighlights();
            return;
        }

        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;

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
            Vector3 worldPos;
            if (target.CustomWorldPos.HasValue)
            {
                worldPos = target.CustomWorldPos.Value;
            }
            else if (target.Entity is IStaticEntity staticEntity)
            {
                Tile3i tile = staticEntity.CenterTile;
                worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
            }
            else if (target.Entity is IEntityWithPosition ePos)
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

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            float avg = target.AveragePollution;
            string text = target.CustomText ?? ("[" + avg.ToString("0.0", Mafi.Localization.LocalizationManager.CurrentCultureInfo) + "]");

            Vector2 size = style.CalcSize(new GUIContent(text));
            float width = size.x + 8f;
            float height = size.y + 2f;

            float t = (globalMax > globalMin) ? (avg - globalMin) / (globalMax - globalMin) : (globalMax > 0f ? 1f : 0f);
            Color textColor = InterpolateColor(t);

            // Only highlight/glow entities that have avg > 0
            if (target.Entity != null && avg > 0f && m_highlighter != null && DesignerToolkitSettings.PollutionGlowEnabled && target.Entity is IRenderedEntity renderedEntity)
            {
                if (t >= 0.1f)
                {
                    int alpha = (int)(t * 215f);
                    ColorRgba highlightColor = new ColorRgba(255, 255, 255, alpha);
                    try { m_highlighter.Highlight(renderedEntity, highlightColor); } catch { }
                    currentHighlights.Add(target.Entity.Id.Value);
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
                Opacity = opacity,
                Cluster = target.Cluster
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
                    if (dt.Cluster != null)
                    {
                        foreach (var tilePos in dt.Cluster.TilePositions)
                        {
                            Vector3 tileScreen = camera.WorldToScreenPoint(tilePos);
                            if (tileScreen.z > 0)
                            {
                                float gx = tileScreen.x;
                                float gy = Screen.height - tileScreen.y;
                                float tileRadius = Mathf.Clamp(dt.Radius * 0.7f, 15f, 40f);
                                Rect glowRect = new Rect(gx - tileRadius, gy - tileRadius, tileRadius * 2f, tileRadius * 2f);
                                GUI.color = new Color(1f, 1f, 1f, dt.Opacity * 0.7f);
                                GUI.DrawTexture(glowRect, m_glowTexture);
                            }
                        }
                    }
                    else
                    {
                        Rect glowRect = new Rect(dt.GuiX - dt.Radius, dt.GuiY - dt.Radius, dt.Radius * 2f, dt.Radius * 2f);
                        GUI.color = new Color(1f, 1f, 1f, dt.Opacity);
                        GUI.DrawTexture(glowRect, m_glowTexture);
                    }
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
                    if (m_entitiesManager != null && m_entitiesManager.TryGetEntity(new EntityId(id), out IEntity e) && !e.IsDestroyed)
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
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.PollutionOverlayToggle))
        {
            HotkeysRegistry.PlayClickSound();
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
        public LandfillCluster? Cluster;
    }
}
