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
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Sorters;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Ports;
using Mafi.Core.Ports.Io;
using Mafi.Core.GameLoop;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Unity;
using Mafi.Unity.Entities.Static;
using Mafi.Unity.Factory.Transports;
using Mafi.Unity.Ports.Io;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit
{
    internal sealed class HeightFilter : IDisposable
    {
        private static readonly ModLogger s_log = new ModLogger("BDT.HeightFilter");
        private static HeightFilter? s_instance;

        private readonly Harmony m_harmony;
        private readonly IGameLoopEvents m_gameLoopEvents;
        private readonly HashSet<int> m_hiddenTransportIds = new HashSet<int>();
        private readonly HashSet<int> m_hiddenPillarIds = new HashSet<int>();
        private readonly HashSet<int> m_hiddenLayoutEntityIds = new HashSet<int>();

        private TransportsManager? m_transportsManager;
        private EntitiesManager? m_entitiesManager;
        private TerrainManager? m_terrainManager;
        private GameTime m_gameTime = new GameTime();

        private InstancedChunkBasedTransportsRenderer? s_transportsRenderer;
        private TransportPillarsRenderer? s_pillarsRenderer;
        private IoPortsRenderer? s_ioPortsRenderer;
        private InstancedChunkBasedLayoutEntitiesRenderer? s_layoutEntitiesRenderer;

        private bool m_isSubscribed;
        private bool m_isInitialized;

        public HeightFilter(Harmony harmony, IGameLoopEvents gameLoopEvents)
        {
            m_harmony = harmony;
            m_gameLoopEvents = gameLoopEvents;
        }

        public void Initialize(DependencyResolver resolver)
        {
            if (m_isInitialized) return;

            s_instance = this;

            try
            {
                m_transportsManager = resolver.Resolve<TransportsManager>();
                m_entitiesManager = resolver.Resolve<EntitiesManager>();
                m_terrainManager = resolver.Resolve<TerrainManager>();
                try { m_gameTime = resolver.Resolve<GameTime>(); } catch { }

                s_transportsRenderer = resolver.TryResolve<InstancedChunkBasedTransportsRenderer>().ValueOrNull;
                s_pillarsRenderer = resolver.TryResolve<TransportPillarsRenderer>().ValueOrNull;
                s_ioPortsRenderer = resolver.TryResolve<IoPortsRenderer>().ValueOrNull;
                s_layoutEntitiesRenderer = resolver.TryResolve<InstancedChunkBasedLayoutEntitiesRenderer>().ValueOrNull;

                s_log.Info($"HeightFilter renderers resolved - Transports: {s_transportsRenderer != null}, Pillars: {s_pillarsRenderer != null}, IoPorts: {s_ioPortsRenderer != null}, LayoutEntities: {s_layoutEntitiesRenderer != null}");

                ApplyHarmonyPatches();

                m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
                m_gameLoopEvents.SyncUpdate.AddNonSaveable(this, OnSyncUpdate);
                m_gameLoopEvents.RenderUpdateEnd.AddNonSaveable(this, OnRenderUpdateEnd);
                DesignerToolkitSettings.HeightFilterMaxVisibleLevelChanged += OnMaxVisibleLevelChanged;
                m_isSubscribed = true;
                m_isInitialized = true;

                s_log.Info("Height filter system initialized successfully. Initial filter application deferred to first sync update tick.");
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to initialize HeightFilter component: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (m_isSubscribed)
            {
                try { m_gameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnInputUpdate); } catch { }
                try { m_gameLoopEvents.SyncUpdate.RemoveNonSaveable(this, OnSyncUpdate); } catch { }
                try { m_gameLoopEvents.RenderUpdateEnd.RemoveNonSaveable(this, OnRenderUpdateEnd); } catch { }
                DesignerToolkitSettings.HeightFilterMaxVisibleLevelChanged -= OnMaxVisibleLevelChanged;
                m_isSubscribed = false;
            }

            s_instance = null;
            m_isInitialized = false;

            // Restore all hidden entities to renderers on dispose/unload
            ShowAllHidden();
        }

        private void ApplyHarmonyPatches()
        {
            SafePatch(typeof(Transport).GetMethod("IsSelected", BindingFlags.Instance | BindingFlags.Public),
                "Transport.IsSelected", nameof(Transport_IsSelected_Prefix));

            SafePatch(typeof(StaticEntity).GetMethod("IsSelected", BindingFlags.Instance | BindingFlags.Public),
                "StaticEntity.IsSelected", nameof(StaticEntity_IsSelected_Prefix));

            SafePatch(typeof(IoPortsRenderer.PortsChunkStandard).GetMethod("ShowPort", BindingFlags.Instance | BindingFlags.Public),
                "IoPortsRenderer.PortsChunkStandard.ShowPort", nameof(ShowPort_Prefix));

            SafePatch(typeof(IoPortsRenderer.PortsChunkStandard).GetMethod("showAndRegisterHighlightForPort", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                "IoPortsRenderer.PortsChunkStandard.showAndRegisterHighlightForPort", nameof(ShowPort_Prefix));
        }

        private void SafePatch(MethodInfo? target, string patchName, string prefixName)
        {
            if (target == null)
            {
                s_log.Warning($"Patch target for '{patchName}' was not found. It might have been modified or removed in this version of CoI.");
                return;
            }

            try
            {
                var prefix = new HarmonyMethod(typeof(HeightFilter), prefixName);
                m_harmony.Patch(target, prefix: prefix);
                s_log.Info($"Successfully patched '{patchName}'");
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to apply Harmony patch for '{patchName}': {ex.Message}");
            }
        }

        private static bool Transport_IsSelected_Prefix(Transport __instance, ref bool __result)
        {
            if (s_instance != null && s_instance.m_hiddenTransportIds.Contains(__instance.Id.Value))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool StaticEntity_IsSelected_Prefix(StaticEntity __instance, ref bool __result)
        {
            if (s_instance != null && s_instance.m_hiddenLayoutEntityIds.Contains(__instance.Id.Value))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool ShowPort_Prefix(IoPort port)
        {
            if (s_instance == null) return true;
            if (port.OwnerEntity is Transport t && s_instance.m_hiddenTransportIds.Contains(t.Id.Value))
                return false;
            if (port.OwnerEntity is LayoutEntityBase && s_instance.m_hiddenLayoutEntityIds.Contains(port.OwnerEntity.Id.Value))
                return false;
            return true;
        }

        private bool m_firstUpdate = true;
        private bool m_filterDirty = false;
        private int m_initDelayFrames = 0;

        private void OnInputUpdate(GameTime _)
        {
            if (DesignerToolkitSettings.HeightFilterShowLayerHotkey.IsPressed())
            {
                int current = DesignerToolkitSettings.HeightFilterMaxVisibleLevel;
                if (current < 6)
                {
                    DesignerToolkitSettings.SetHeightFilterMaxVisibleLevel(current + 1);
                    s_log.Info($"Show layer shortcut pressed. Max visible level set to: {DesignerToolkitSettings.HeightFilterMaxVisibleLevel}");
                }
            }
            else if (DesignerToolkitSettings.HeightFilterHideLayerHotkey.IsPressed())
            {
                int current = DesignerToolkitSettings.HeightFilterMaxVisibleLevel;
                if (current > 0)
                {
                    DesignerToolkitSettings.SetHeightFilterMaxVisibleLevel(current - 1);
                    s_log.Info($"Hide layer shortcut pressed. Max visible level set to: {DesignerToolkitSettings.HeightFilterMaxVisibleLevel}");
                }
            }
        }

        private void OnSyncUpdate(GameTime _)
        {
            if (m_firstUpdate)
            {
                m_firstUpdate = false;
                m_initDelayFrames = 5;
            }

            if (m_filterDirty)
            {
                m_filterDirty = false;
                OnFilterChanged();
            }
        }

        private void OnRenderUpdateEnd(GameTime _)
        {
            if (m_initDelayFrames > 0)
            {
                m_initDelayFrames--;
                if (m_initDelayFrames == 0)
                {
                    s_log.Info("Initial delay frames completed. Triggering initial height filter.");
                    m_filterDirty = true;
                }
            }
        }

        private void OnMaxVisibleLevelChanged(int _)
        {
            m_filterDirty = true;
        }

        private int GetTerrainHeight(Tile2i pos)
        {
            if (m_terrainManager == null) return 0;
            return m_terrainManager.GetHeight(pos).Value.ToIntFloored();
        }

        private bool ShouldBeHidden(Transport transport)
        {
            ImmutableArray<Tile3i> pivots = transport.Trajectory.Pivots;
            if (pivots.Length == 0) return false;

            int maxVisibleLevel = DesignerToolkitSettings.HeightFilterMaxVisibleLevel;
            if (maxVisibleLevel >= 6) return false;

            int hiddenCount = 0;
            for (int i = 0; i < pivots.Length; i++)
            {
                int terrainZ = GetTerrainHeight(pivots[i].Xy);
                int relative = pivots[i].Z - terrainZ;
                int uiLevel = relative + 1; // 1-based level (0 is 1, 1 is 2, etc.)

                if (uiLevel > maxVisibleLevel)
                {
                    hiddenCount++;
                }
            }

            return (double)hiddenCount / pivots.Length > 0.5;
        }

        private bool ShouldPillarBeHidden(TransportPillar pillar)
        {
            int maxVisibleLevel = DesignerToolkitSettings.HeightFilterMaxVisibleLevel;
            if (maxVisibleLevel >= 6) return false;

            int baseZ = pillar.CenterTile.Z;
            int height = pillar.Height.Value;
            int terrainZ = GetTerrainHeight(pillar.CenterTile.Xy);

            int hiddenCount = 0;
            for (int z = baseZ; z < baseZ + height; z++)
            {
                int relative = z - terrainZ;
                int level = relative + 1; // 1-based level

                if (level > maxVisibleLevel)
                {
                    hiddenCount++;
                }
            }

            return (double)hiddenCount / height >= 0.5;
        }

        private bool ShouldLayoutEntityBeHidden(LayoutEntityBase entity)
        {
            int maxVisibleLevel = DesignerToolkitSettings.HeightFilterMaxVisibleLevel;
            if (maxVisibleLevel >= 6) return false;

            int terrainZ = GetTerrainHeight(entity.CenterTile.Xy);
            int baseLevel = entity.CenterTile.Z - terrainZ + 1; // 1-based level

            return baseLevel > maxVisibleLevel;
        }

        private void OnFilterChanged()
        {
            if (!m_isInitialized || m_transportsManager == null) return;

            try
            {
                // Process Transports
                foreach (Transport transport in m_transportsManager.Transports)
                {
                    if (transport.IsDestroyed) continue;

                    int id = transport.Id.Value;
                    bool shouldHide = ShouldBeHidden(transport);
                    bool isHidden = m_hiddenTransportIds.Contains(id);

                    if (shouldHide && !isHidden)
                    {
                        HideTransport(transport);
                        m_hiddenTransportIds.Add(id);
                    }
                    else if (!shouldHide && isHidden)
                    {
                        m_hiddenTransportIds.Remove(id);
                        ShowTransport(transport);
                    }
                }

                // Process Pillars
                foreach (var kvp in m_transportsManager.Pillars)
                {
                    TransportPillar pillar = kvp.Value;
                    if (pillar.IsDestroyed) continue;

                    int pillarId = pillar.Id.Value;
                    bool shouldHide = ShouldPillarBeHidden(pillar);
                    bool isHidden = m_hiddenPillarIds.Contains(pillarId);

                    if (shouldHide)
                    {
                        if (pillar.RendererData.IsValid)
                        {
                            HidePillarDirect(pillar);
                        }
                        m_hiddenPillarIds.Add(pillarId);
                    }
                    else
                    {
                        if (!pillar.RendererData.IsValid && isHidden)
                        {
                            ShowPillarDirect(pillar);
                        }
                        m_hiddenPillarIds.Remove(pillarId);
                    }
                }

                // Process Layout Entities
                if (m_entitiesManager != null)
                {
                    ProcessLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Zipper>());
                    ProcessLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<MiniZipper>());
                    ProcessLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Sorter>());
                    ProcessLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Lift>());
                }
            }
            catch (Exception ex)
            {
                s_log.Warning($"Error while applying height filter: {ex.Message}");
            }
        }

        private void ProcessLayoutEntities<T>(IEnumerable<T> entities) where T : LayoutEntityBase
        {
            foreach (T entity in entities)
            {
                if (entity.IsDestroyed) continue;

                int id = entity.Id.Value;
                bool shouldHide = ShouldLayoutEntityBeHidden(entity);
                bool isHidden = m_hiddenLayoutEntityIds.Contains(id);

                if (shouldHide && !isHidden)
                {
                    HideLayoutEntity(entity);
                    m_hiddenLayoutEntityIds.Add(id);
                }
                else if (!shouldHide && isHidden)
                {
                    m_hiddenLayoutEntityIds.Remove(id);
                    ShowLayoutEntity(entity);
                }
            }
        }

        private void HideLayoutEntity(LayoutEntityBase entity)
        {
            if (s_layoutEntitiesRenderer == null) return;
            try
            {
                s_layoutEntitiesRenderer.RemoveEntityOnSync(entity, m_gameTime, EntityRemoveReason.Remove, Option<IEntityProto>.None);
                HideLayoutEntityPorts(entity);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to hide layout entity {entity.Id}: {ex.Message}");
            }
        }

        private void ShowLayoutEntity(LayoutEntityBase entity)
        {
            if (s_layoutEntitiesRenderer == null) return;
            try
            {
                s_layoutEntitiesRenderer.AddEntityOnSync(entity, m_gameTime);
                ShowLayoutEntityPorts(entity);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to show layout entity {entity.Id}: {ex.Message}");
            }
        }

        private void HideLayoutEntityPorts(LayoutEntityBase entity)
        {
            if (s_ioPortsRenderer == null) return;
            if (!(entity is IEntityWithPorts entityWithPorts)) return;
            foreach (var port in entityWithPorts.Ports)
            {
                HideOnePort(port);
            }
        }

        private void ShowLayoutEntityPorts(LayoutEntityBase entity)
        {
            if (s_ioPortsRenderer == null) return;
            if (!(entity is IEntityWithPorts entityWithPorts)) return;
            foreach (var port in entityWithPorts.Ports)
            {
                ShowOnePort(port);
            }
        }

        private void HideTransport(Transport transport)
        {
            if (s_transportsRenderer == null) return;
            try
            {
                s_transportsRenderer.RemoveEntityOnSync(transport, m_gameTime, EntityRemoveReason.Remove, Option<IEntityProto>.None);
                HideTransportPorts(transport);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to hide transport {transport.Id}: {ex.Message}");
            }
        }

        private void ShowTransport(Transport transport)
        {
            if (s_transportsRenderer == null) return;
            try
            {
                s_transportsRenderer.AddEntityOnSync(transport, m_gameTime);
                ShowTransportPorts(transport);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to show transport {transport.Id}: {ex.Message}");
            }
        }

        private void HideTransportPorts(Transport transport)
        {
            if (s_ioPortsRenderer == null) return;
            HideOnePort(transport.StartInputPort);
            HideOnePort(transport.EndOutputPort);
        }

        private void ShowTransportPorts(Transport transport)
        {
            if (s_ioPortsRenderer == null) return;
            ShowOnePort(transport.StartInputPort);
            ShowOnePort(transport.EndOutputPort);
        }

        private void HideOnePort(IoPort port)
        {
            var chunk = GetPortChunk(port);
            if (chunk == null) return;
            try
            {
                chunk.HidePort(port);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to hide port {port.Id}: {ex.Message}");
            }
        }

        private void ShowOnePort(IoPort port)
        {
            var chunk = GetPortChunk(port);
            if (chunk == null) return;
            try
            {
                chunk.ShowPort(port);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to show port {port.Id}: {ex.Message}");
            }
        }

        private IoPortsRenderer.PortsChunkStandard? GetPortChunk(IoPort port)
        {
            if (s_ioPortsRenderer == null) return null;
            try
            {
                var chunkIndex = s_ioPortsRenderer.m_chunksRenderingManager.GetChunkIndex(port.Position.Xy);
                int idx = chunkIndex.Value;
                if (idx < 0 || idx >= s_ioPortsRenderer.m_portsChunks.Length) return null;

                var chunkOption = s_ioPortsRenderer.m_portsChunks[idx];
                return chunkOption.HasValue ? chunkOption.Value : null;
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to resolve port chunk: {ex.Message}");
                return null;
            }
        }

        private void HidePillarDirect(TransportPillar pillar)
        {
            if (s_pillarsRenderer == null) return;
            try
            {
                if (!pillar.RendererData.IsValid) return;

                int chunkIdx = pillar.RendererData.ChunkIndex.Value;
                if (chunkIdx < 0 || chunkIdx >= s_pillarsRenderer.m_pillarsChunks.Length) return;

                var chunkOption = s_pillarsRenderer.m_pillarsChunks[chunkIdx];
                if (!chunkOption.HasValue) return;

                var chunk = chunkOption.Value;
                chunk.RemovePillarParts(pillar.RendererData.PartsIds);
                pillar.RendererData.PartsIds.ReturnToPool();
                pillar.RendererData = default;
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to hide pillar {pillar.Id}: {ex.Message}");
            }
        }

        private void ShowPillarDirect(TransportPillar pillar)
        {
            if (s_pillarsRenderer == null) return;
            try
            {
                var visuals = s_pillarsRenderer.m_transportsConstructionHelper.ComputePillarVisuals(pillar.CenterTile, pillar.Height);
                if (!visuals.Layers.IsValid) return;

                var chunkIndex = s_pillarsRenderer.m_chunksRenderer.GetChunkIndex(pillar.CenterTile.Xy);
                int indexValue = chunkIndex.Value;

                if (indexValue < 0 || indexValue >= s_pillarsRenderer.m_pillarsChunks.Length) return;

                var chunkOption = s_pillarsRenderer.m_pillarsChunks[indexValue];
                if (!chunkOption.HasValue) return;

                var chunk = chunkOption.Value;
                var partsResult = chunk.AddPillarParts(visuals);
                if (!partsResult.IsValid) return;

                pillar.RendererData = new TransportPillarRendererData(chunkIndex, partsResult);
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to show pillar {pillar.Id}: {ex.Message}");
            }
        }

        private void ShowAllHidden()
        {
            if (m_transportsManager == null) return;

            try
            {
                // Restore hidden transports
                foreach (Transport transport in m_transportsManager.Transports)
                {
                    if (transport.IsDestroyed) continue;
                    int id = transport.Id.Value;
                    if (m_hiddenTransportIds.Contains(id))
                    {
                        ShowTransport(transport);
                    }
                }
                m_hiddenTransportIds.Clear();

                // Restore hidden pillars
                foreach (var kvp in m_transportsManager.Pillars)
                {
                    TransportPillar pillar = kvp.Value;
                    if (pillar.IsDestroyed) continue;
                    int id = pillar.Id.Value;
                    if (m_hiddenPillarIds.Contains(id))
                    {
                        if (!pillar.RendererData.IsValid)
                        {
                            ShowPillarDirect(pillar);
                        }
                    }
                }
                m_hiddenPillarIds.Clear();

                // Restore hidden layout entities
                if (m_entitiesManager != null)
                {
                    RestoreAllLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Zipper>());
                    RestoreAllLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<MiniZipper>());
                    RestoreAllLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Sorter>());
                    RestoreAllLayoutEntities(m_entitiesManager.GetAllEntitiesOfType<Lift>());
                }
                m_hiddenLayoutEntityIds.Clear();
            }
            catch (Exception ex)
            {
                s_log.Warning($"Error while restoring hidden entities on dispose: {ex.Message}");
            }
        }

        private void RestoreAllLayoutEntities<T>(IEnumerable<T> entities) where T : LayoutEntityBase
        {
            foreach (T entity in entities)
            {
                if (entity.IsDestroyed) continue;
                int id = entity.Id.Value;
                if (m_hiddenLayoutEntityIds.Contains(id))
                {
                    ShowLayoutEntity(entity);
                }
            }
        }
    }
}
