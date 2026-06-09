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
using Mafi.Core.Terrain;

namespace UtilitiesPP
{
    public static class HeightFilterPatch
    {
        private static object s_transportsRenderer;
        private static object s_pillarsRenderer;
        private static MethodInfo s_addEntityOnSync;
        private static MethodInfo s_removeEntityOnSync;

        private static TransportsManager s_transportsManager;

        private static readonly HashSet<int> s_hiddenTransportIds = new HashSet<int>();
        private static readonly HashSet<int> s_hiddenPillarIds = new HashSet<int>();
        private static readonly HashSet<int> s_hiddenLayoutEntityIds = new HashSet<int>();

        private static bool s_initialized;

        private static object s_layoutEntitiesRenderer;
        private static MethodInfo s_layoutAddEntityOnSync;
        private static MethodInfo s_layoutRemoveEntityOnSync;
        private static EntitiesManager s_entitiesManager;
        private static TerrainManager s_terrainManager;
        private static GameTime s_gameTime;

        private static Type s_rendererType;
        private static Type s_pillarsType;

        private static FieldInfo s_pillarsChunksField;
        private static object s_constructionHelper;
        private static MethodInfo s_computePillarVisuals;
        private static object s_ioPortsRenderer;
        private static Harmony s_harmony;

        public static void ApplyPatches(Harmony harmony)
        {
            s_harmony = harmony;
            HeightFilterState.OnFilterChanged += OnFilterChanged;

            var transportIsSelected = typeof(Transport).GetMethod("IsSelected",
                BindingFlags.Instance | BindingFlags.Public);
            if (transportIsSelected != null)
            {
                harmony.Patch(transportIsSelected,
                    prefix: new HarmonyMethod(typeof(HeightFilterPatch), nameof(Transport_IsSelected_Prefix)));
            }

            var staticEntityIsSelected = typeof(StaticEntity).GetMethod("IsSelected",
                BindingFlags.Instance | BindingFlags.Public);
            if (staticEntityIsSelected != null)
            {
                harmony.Patch(staticEntityIsSelected,
                    prefix: new HarmonyMethod(typeof(HeightFilterPatch), nameof(LayoutEntity_IsSelected_Prefix)));
            }
        }

        private static bool Transport_IsSelected_Prefix(Transport __instance, ref bool __result)
        {
            if (s_hiddenTransportIds.Contains(__instance.Id.Value))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool LayoutEntity_IsSelected_Prefix(StaticEntity __instance, ref bool __result)
        {
            if (s_hiddenLayoutEntityIds.Contains(__instance.Id.Value))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool ShowPort_Prefix(IoPort port)
        {
            if (port.OwnerEntity is Transport t && s_hiddenTransportIds.Contains(t.Id.Value))
                return false;
            if (port.OwnerEntity is LayoutEntityBase && s_hiddenLayoutEntityIds.Contains(port.OwnerEntity.Id.Value))
                return false;
            return true;
        }

        public static void LateInit(DependencyResolver resolver)
        {
            try
            {
                s_transportsManager = resolver.Resolve<TransportsManager>();
                s_entitiesManager = resolver.Resolve<EntitiesManager>();
                s_terrainManager = resolver.Resolve<TerrainManager>();

                var unityAsm = typeof(Mafi.Unity.UiToolkit.Component.UiComponent).Assembly;

                s_rendererType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Unity.Factory.Transports.InstancedChunkBasedTransportsRenderer, Mafi.Unity",
                    "Utilities++ height filter (transport renderer)");
                if (s_rendererType != null)
                {
                    var opt = resolver.TryResolve(s_rendererType);
                    if (opt.HasValue)
                    {
                        s_transportsRenderer = opt.Value;
                        s_addEntityOnSync = s_rendererType.GetMethod("AddEntityOnSync",
                            BindingFlags.Instance | BindingFlags.Public);
                        s_removeEntityOnSync = s_rendererType.GetMethod("RemoveEntityOnSync",
                            BindingFlags.Instance | BindingFlags.Public);
                    }
                }

                s_pillarsType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Unity.Factory.Transports.TransportPillarsRenderer, Mafi.Unity",
                    "Utilities++ height filter (pillars renderer)");
                if (s_pillarsType != null)
                {
                    var opt = resolver.TryResolve(s_pillarsType);
                    if (opt.HasValue)
                    {
                        s_pillarsRenderer = opt.Value;
                        s_pillarsChunksField = s_pillarsType.GetField("m_pillarsChunks",
                            BindingFlags.Instance | BindingFlags.NonPublic);

                        var helperField = s_pillarsType.GetField("m_transportsConstructionHelper",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (helperField != null)
                            s_constructionHelper = helperField.GetValue(s_pillarsRenderer);
                        if (s_constructionHelper != null)
                            s_computePillarVisuals = s_constructionHelper.GetType().GetMethod("ComputePillarVisuals",
                                BindingFlags.Instance | BindingFlags.Public);
                    }
                }

                var ioPortsType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Unity.Ports.Io.IoPortsRenderer, Mafi.Unity",
                    "Utilities++ height filter (io ports renderer)");
                if (ioPortsType != null)
                {
                    var opt = resolver.TryResolve(ioPortsType);
                    if (opt.HasValue)
                        s_ioPortsRenderer = opt.Value;

                    if (s_harmony != null)
                    {
                        var chunkType = ioPortsType.GetNestedType("PortsChunkStandard", BindingFlags.NonPublic);
                        if (chunkType != null)
                        {
                            var showPort = chunkType.GetMethod("ShowPort", BindingFlags.Instance | BindingFlags.Public);
                            if (showPort != null)
                                s_harmony.Patch(showPort, prefix: new HarmonyMethod(typeof(HeightFilterPatch), nameof(ShowPort_Prefix)));

                            var highlightPort = chunkType.GetMethod("showAndRegisterHighlightForPort", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (highlightPort != null)
                                s_harmony.Patch(highlightPort, prefix: new HarmonyMethod(typeof(HeightFilterPatch), nameof(ShowPort_Prefix)));
                        }
                    }
                }

                var layoutRendererType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Unity.Entities.Static.InstancedChunkBasedLayoutEntitiesRenderer, Mafi.Unity",
                    "Utilities++ height filter (layout renderer)");
                if (layoutRendererType != null)
                {
                    var opt = resolver.TryResolve(layoutRendererType);
                    if (opt.HasValue)
                    {
                        s_layoutEntitiesRenderer = opt.Value;
                        s_layoutAddEntityOnSync = layoutRendererType.GetMethod("AddEntityOnSync",
                            BindingFlags.Instance | BindingFlags.Public);
                        s_layoutRemoveEntityOnSync = layoutRendererType.GetMethod("RemoveEntityOnSync",
                            BindingFlags.Instance | BindingFlags.Public);
                    }
                }

                try { s_gameTime = resolver.Resolve<GameTime>(); }
                catch { s_gameTime = new GameTime(); }

                s_initialized = s_transportsRenderer != null && s_addEntityOnSync != null && s_removeEntityOnSync != null;

                if (s_initialized && HeightFilterState.AnyHidden)
                    OnFilterChanged();
            }
            catch
            {
            }
        }

        private static int RelativeToLevel(int relative)
        {
            return relative + 1;
        }

        private static int GetTerrainHeight(Tile2i pos)
        {
            if (s_terrainManager == null) return 0;
            return s_terrainManager.GetHeight(pos).Value.ToIntFloored();
        }

        private static bool ShouldBeHidden(Transport transport)
        {
            ImmutableArray<Tile3i> pivots = transport.Trajectory.Pivots;
            if (pivots.Length == 0) return false;
            int maxRelative = 0;
            for (int i = 0; i < pivots.Length; i++)
            {
                int terrainZ = GetTerrainHeight(pivots[i].Xy);
                int relative = pivots[i].Z - terrainZ;
                if (relative > maxRelative) maxRelative = relative;
            }
            int uiLevel = RelativeToLevel(maxRelative);
            return uiLevel <= HeightFilterState.MAX_LEVELS && !HeightFilterState.IsLevelVisible(uiLevel);
        }

        private static void OnFilterChanged()
        {
            if (!s_initialized || s_transportsManager == null) return;

            try
            {
                foreach (Transport transport in s_transportsManager.Transports)
                {
                    int id = transport.Id.Value;
                    bool shouldHide = ShouldBeHidden(transport);
                    bool isHidden = s_hiddenTransportIds.Contains(id);

                    if (shouldHide && !isHidden)
                    {
                        HideTransport(transport);
                        s_hiddenTransportIds.Add(id);
                    }
                    else if (!shouldHide && isHidden)
                    {
                        s_hiddenTransportIds.Remove(id);
                        ShowTransport(transport);
                    }
                }

                if (s_pillarsRenderer != null && s_pillarsChunksField != null)
                {
                    foreach (var kvp in s_transportsManager.Pillars)
                    {
                        TransportPillar pillar = kvp.Value;
                        int pillarId = pillar.Id.Value;

                        int topZ = pillar.CenterTile.Z + pillar.Height.Value - 1;
                        int terrainZ = GetTerrainHeight(pillar.CenterTile.Xy);
                        int pillarLevel = RelativeToLevel(topZ - terrainZ);
                        bool shouldHide = pillarLevel <= HeightFilterState.MAX_LEVELS && !HeightFilterState.IsLevelVisible(pillarLevel);

                        bool isHidden = s_hiddenPillarIds.Contains(pillarId);

                        if (shouldHide && !isHidden)
                        {
                            HidePillarDirect(pillar);
                            s_hiddenPillarIds.Add(pillarId);
                        }
                        else if (!shouldHide && isHidden)
                        {
                            ShowPillarDirect(pillar);
                            s_hiddenPillarIds.Remove(pillarId);
                        }
                    }
                }

                if (s_layoutEntitiesRenderer != null && s_layoutRemoveEntityOnSync != null
                    && s_layoutAddEntityOnSync != null && s_entitiesManager != null)
                {
                    ProcessLayoutEntities(s_entitiesManager.GetAllEntitiesOfType<Zipper>());
                    ProcessLayoutEntities(s_entitiesManager.GetAllEntitiesOfType<MiniZipper>());
                    ProcessLayoutEntities(s_entitiesManager.GetAllEntitiesOfType<Sorter>());
                    ProcessLayoutEntities(s_entitiesManager.GetAllEntitiesOfType<Lift>());
                }
            }
            catch
            {
            }
        }

        private static void ProcessLayoutEntities<T>(IEnumerable<T> entities) where T : LayoutEntityBase
        {
            foreach (T entity in entities)
            {
                int id = entity.Id.Value;
                bool shouldHide = ShouldLayoutEntityBeHidden(entity);
                bool isHidden = s_hiddenLayoutEntityIds.Contains(id);

                if (shouldHide && !isHidden)
                {
                    HideLayoutEntity(entity);
                    s_hiddenLayoutEntityIds.Add(id);
                }
                else if (!shouldHide && isHidden)
                {
                    s_hiddenLayoutEntityIds.Remove(id);
                    ShowLayoutEntity(entity);
                }
            }
        }

        private static bool ShouldLayoutEntityBeHidden(LayoutEntityBase entity)
        {
            int terrainZ = GetTerrainHeight(entity.CenterTile.Xy);
            int baseLevel = RelativeToLevel(entity.CenterTile.Z - terrainZ);
            if (baseLevel > HeightFilterState.MAX_LEVELS) return false;
            return !HeightFilterState.IsLevelVisible(baseLevel);
        }

        private static void HideLayoutEntity(LayoutEntityBase entity)
        {
            try
            {
                if (s_layoutRemoveEntityOnSync == null || s_layoutEntitiesRenderer == null) return;

                var parms = s_layoutRemoveEntityOnSync.GetParameters();
                object[] args = new object[parms.Length];
                args[0] = entity;
                for (int i = 1; i < parms.Length; i++)
                {
                    var pt = parms[i].ParameterType;
                    if (pt.IsEnum)
                        args[i] = Enum.ToObject(pt, 0);
                    else if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                s_layoutRemoveEntityOnSync.Invoke(s_layoutEntitiesRenderer, args);
                HideLayoutEntityPorts(entity);
            }
            catch { }
        }

        private static void ShowLayoutEntity(LayoutEntityBase entity)
        {
            try
            {
                if (s_layoutAddEntityOnSync == null || s_layoutEntitiesRenderer == null) return;

                var parms = s_layoutAddEntityOnSync.GetParameters();
                object[] args = new object[parms.Length];
                args[0] = entity;
                for (int i = 1; i < parms.Length; i++)
                {
                    var pt = parms[i].ParameterType;
                    if (pt == typeof(GameTime))
                        args[i] = s_gameTime;
                    else if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                s_layoutAddEntityOnSync.Invoke(s_layoutEntitiesRenderer, args);
                ShowLayoutEntityPorts(entity);
            }
            catch { }
        }

        private static void HideLayoutEntityPorts(LayoutEntityBase entity)
        {
            try
            {
                if (s_ioPortsRenderer == null) return;
                if (!(entity is IEntityWithPorts entityWithPorts)) return;
                var ports = entityWithPorts.Ports;
                for (int i = 0; i < ports.Length; i++)
                    HideOnePort(ports[i]);
            }
            catch { }
        }

        private static void ShowLayoutEntityPorts(LayoutEntityBase entity)
        {
            try
            {
                if (s_ioPortsRenderer == null) return;
                if (!(entity is IEntityWithPorts entityWithPorts)) return;
                var ports = entityWithPorts.Ports;
                for (int i = 0; i < ports.Length; i++)
                    ShowOnePort(ports[i]);
            }
            catch { }
        }

        private static void HideTransport(Transport transport)
        {
            try
            {
                if (s_removeEntityOnSync == null || s_transportsRenderer == null) return;

                var parms = s_removeEntityOnSync.GetParameters();
                object[] args = new object[parms.Length];
                args[0] = transport;
                for (int i = 1; i < parms.Length; i++)
                {
                    var pt = parms[i].ParameterType;
                    if (pt.IsEnum)
                        args[i] = Enum.ToObject(pt, 0);
                    else if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                s_removeEntityOnSync.Invoke(s_transportsRenderer, args);
                HideTransportPorts(transport);
            }
            catch { }
        }

        private static void ShowTransport(Transport transport)
        {
            try
            {
                if (s_addEntityOnSync == null || s_transportsRenderer == null) return;

                var parms = s_addEntityOnSync.GetParameters();
                object[] args = new object[parms.Length];
                args[0] = transport;
                for (int i = 1; i < parms.Length; i++)
                {
                    var pt = parms[i].ParameterType;
                    if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                s_addEntityOnSync.Invoke(s_transportsRenderer, args);
                ShowTransportPorts(transport);
            }
            catch { }
        }

        private static void HidePillarDirect(TransportPillar pillar)
        {
            try
            {
                if (!pillar.RendererData.IsValid) return;

                var chunksArray = s_pillarsChunksField.GetValue(s_pillarsRenderer) as Array;
                if (chunksArray == null) return;

                int chunkIdx = pillar.RendererData.ChunkIndex.Value;
                if (chunkIdx < 0 || chunkIdx >= chunksArray.Length) return;

                var chunkOption = chunksArray.GetValue(chunkIdx);
                var valueOrNullProp = chunkOption.GetType().GetProperty("ValueOrNull",
                    BindingFlags.Instance | BindingFlags.Public);
                if (valueOrNullProp == null) return;
                var chunk = valueOrNullProp.GetValue(chunkOption);
                if (chunk == null) return;

                var removeParts = chunk.GetType().GetMethod("RemovePillarParts",
                    BindingFlags.Instance | BindingFlags.Public);
                if (removeParts == null) return;

                removeParts.Invoke(chunk, new object[] { pillar.RendererData.PartsIds });
                pillar.RendererData.PartsIds.ReturnToPool();
                pillar.RendererData = default(TransportPillarRendererData);
            }
            catch { }
        }

        private static void ShowPillarDirect(TransportPillar pillar)
        {
            try
            {
                if (s_computePillarVisuals == null || s_constructionHelper == null) return;

                var computeParams = s_computePillarVisuals.GetParameters();
                object[] computeArgs = new object[computeParams.Length];
                computeArgs[0] = pillar.CenterTile;
                computeArgs[1] = pillar.Height;
                for (int i = 2; i < computeParams.Length; i++)
                {
                    if (computeParams[i].HasDefaultValue)
                        computeArgs[i] = computeParams[i].DefaultValue;
                    else if (computeParams[i].ParameterType.IsValueType)
                        computeArgs[i] = Activator.CreateInstance(computeParams[i].ParameterType);
                    else
                        computeArgs[i] = null;
                }

                var visuals = s_computePillarVisuals.Invoke(s_constructionHelper, computeArgs);
                if (visuals == null) return;

                var chunksRendererField = s_pillarsType.GetField("m_chunksRenderer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (chunksRendererField == null) return;
                var chunksRenderer = chunksRendererField.GetValue(s_pillarsRenderer);

                var getChunkIndex = chunksRenderer.GetType().GetMethod("GetChunkIndex",
                    BindingFlags.Instance | BindingFlags.Public,
                    null, new Type[] { typeof(Tile2i) }, null);
                if (getChunkIndex == null) return;

                var chunkIndex = getChunkIndex.Invoke(chunksRenderer, new object[] { pillar.CenterTile.Xy });
                var indexField = chunkIndex.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public);
                if (indexField == null) return;
                int indexValue = Convert.ToInt32(indexField.GetValue(chunkIndex));

                var chunksArray = s_pillarsChunksField.GetValue(s_pillarsRenderer) as Array;
                if (chunksArray == null || indexValue < 0 || indexValue >= chunksArray.Length) return;

                var chunkOption = chunksArray.GetValue(indexValue);
                var valueOrNullProp = chunkOption.GetType().GetProperty("ValueOrNull",
                    BindingFlags.Instance | BindingFlags.Public);
                if (valueOrNullProp == null) return;
                var chunk = valueOrNullProp.GetValue(chunkOption);
                if (chunk == null) return;

                MethodInfo addParts = null;
                foreach (var m in chunk.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name == "AddPillarParts" && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == visuals.GetType())
                    {
                        addParts = m;
                        break;
                    }
                }
                if (addParts == null) return;

                var partsResult = addParts.Invoke(chunk, new object[] { visuals });
                if (partsResult == null) return;

                var ci = new Chunk256Index((ushort)indexValue);
                pillar.RendererData = new TransportPillarRendererData(ci, (PooledArray<uint>)partsResult);
            }
            catch { }
        }

        private static void HideTransportPorts(Transport transport)
        {
            try
            {
                if (s_ioPortsRenderer == null) return;
                HideOnePort(transport.StartInputPort);
                HideOnePort(transport.EndOutputPort);
            }
            catch { }
        }

        private static void ShowTransportPorts(Transport transport)
        {
            try
            {
                if (s_ioPortsRenderer == null) return;
                ShowOnePort(transport.StartInputPort);
                ShowOnePort(transport.EndOutputPort);
            }
            catch { }
        }

        private static void HideOnePort(Mafi.Core.Ports.Io.IoPort port)
        {
            try
            {
                var chunk = GetPortChunk(port);
                if (chunk == null) return;

                var hideMethod = chunk.GetType().GetMethod("HidePort",
                    BindingFlags.Instance | BindingFlags.Public);
                if (hideMethod != null)
                    hideMethod.Invoke(chunk, new object[] { port });
            }
            catch { }
        }

        private static void ShowOnePort(Mafi.Core.Ports.Io.IoPort port)
        {
            try
            {
                var chunk = GetPortChunk(port);
                if (chunk == null) return;

                var showMethod = chunk.GetType().GetMethod("ShowPort",
                    BindingFlags.Instance | BindingFlags.Public);
                if (showMethod != null)
                    showMethod.Invoke(chunk, new object[] { port });
            }
            catch { }
        }

        private static object GetPortChunk(Mafi.Core.Ports.Io.IoPort port)
        {
            try
            {
                var ioType = s_ioPortsRenderer.GetType();

                var chunksRendererField = ioType.GetField("m_chunksRenderingManager",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (chunksRendererField == null) return null;
                var chunksRenderer = chunksRendererField.GetValue(s_ioPortsRenderer);

                var getChunkIndex = chunksRenderer.GetType().GetMethod("GetChunkIndex",
                    BindingFlags.Instance | BindingFlags.Public,
                    null, new Type[] { typeof(Tile2i) }, null);
                if (getChunkIndex == null) return null;

                var chunkIndex = getChunkIndex.Invoke(chunksRenderer, new object[] { port.Position.Xy });

                var portsChunksField = ioType.GetField("m_portsChunks",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (portsChunksField == null) return null;
                var portsChunks = portsChunksField.GetValue(s_ioPortsRenderer) as Array;
                if (portsChunks == null) return null;

                var indexField = chunkIndex.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public);
                if (indexField == null) return null;
                int idx = Convert.ToInt32(indexField.GetValue(chunkIndex));

                if (idx < 0 || idx >= portsChunks.Length) return null;

                var chunkOption = portsChunks.GetValue(idx);
                var valueOrNull = chunkOption.GetType().GetProperty("ValueOrNull",
                    BindingFlags.Instance | BindingFlags.Public);
                if (valueOrNull == null) return null;
                return valueOrNull.GetValue(chunkOption);
            }
            catch { return null; }
        }

        public static void EnforceFilter()
        {
            if (!s_initialized || !HeightFilterState.AnyHidden) return;
            if (s_transportsManager == null) return;

            try
            {
                foreach (Transport transport in s_transportsManager.Transports)
                {
                    int id = transport.Id.Value;
                    if (s_hiddenTransportIds.Contains(id))
                    {
                        HideTransportPorts(transport);
                        continue;
                    }
                    if (ShouldBeHidden(transport))
                    {
                        HideTransport(transport);
                        s_hiddenTransportIds.Add(id);
                    }
                }

                if (s_pillarsRenderer != null && s_pillarsChunksField != null)
                {
                    foreach (var kvp in s_transportsManager.Pillars)
                    {
                        TransportPillar pillar = kvp.Value;
                        if (!pillar.RendererData.IsValid) continue;

                        int topZ = pillar.CenterTile.Z + pillar.Height.Value - 1;
                        int terrainZ = GetTerrainHeight(pillar.CenterTile.Xy);
                        int pillarLevel = RelativeToLevel(topZ - terrainZ);

                        if (pillarLevel <= HeightFilterState.MAX_LEVELS
                            && !HeightFilterState.IsLevelVisible(pillarLevel))
                        {
                            HidePillarDirect(pillar);
                            s_hiddenPillarIds.Add(pillar.Id.Value);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
