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
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Mafi;
using Mafi.Base.Prototypes.Buildings;
using Mafi.Base.Prototypes.Sandbox;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Ports;
using Mafi.Core.Ports.Io;
using Mafi.Core.Products;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Seeds vanilla's current color for empty fluid and molten transports. Vanilla
/// flow then owns the color and fades it toward incoming products as usual.
/// </summary>
internal static class PipeColoring
{
    private const int MAX_UPSTREAM_TRANSITIONS = 10;

    private static readonly ModLogger s_log = new ModLogger("BDT.PipeColoring");

    private static readonly Dictionary<Transport, int> s_forceRendererRefresh =
        new Dictionary<Transport, int>();
    private static readonly object s_pipeColorStateLock = new object();
    private static readonly object s_refreshRequestLock = new object();
    private static readonly object s_transportSnapshotLock = new object();
    private static readonly HashSet<IEntityWithPorts> s_clusterRefreshRoots =
        new HashSet<IEntityWithPorts>();
    private static readonly HashSet<Transport> s_transportSnapshot =
        new HashSet<Transport>();

    private static IEntitiesManager? s_entitiesManager;
    private static IConstructionManager? s_constructionManager;
    private static PortProductsResolver? s_portProductsResolver;
    private static IGameLoopEvents? s_gameLoopEvents;
    private static FieldInfo? s_transportMbEntityField;
    private static MethodInfo? s_transportMbUpdatePipeColor;
    private static MethodInfo? s_transportsRendererPrepareTransportUpdates;
    private static FieldInfo? s_transportColorField;
    private static MethodInfo? s_transportAccentColorSetter;
    private static bool s_initialized;
    private static bool s_globalRefreshRequested;
    private static int s_refreshRequested;
    private static int s_forceRendererRefreshGeneration;

    private readonly struct PreviewColor
    {
        public readonly ColorRgba Body;
        public readonly ColorRgba Accent;

        public PreviewColor(ColorRgba body, ColorRgba accent)
        {
            Body = body;
            Accent = accent;
        }
    }

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            PatchPostfix(harmony, typeof(IoPortsManager), "OnPortConnectionChanged", nameof(PortConnectionChangedPostfix));
            PatchPostfix(harmony, typeof(Machine), "rebuildRecipes", nameof(RecipeChangedPostfix));
            PatchPostfix(harmony, typeof(Machine), "ReorderRecipe", nameof(RecipeChangedPostfix));
            PatchPostfix(harmony, typeof(ProductsSourceEntity), "SetProvidedProduct", nameof(SourceProductChangedPostfix));
            PatchPostfix(harmony, typeof(UniversalProductsSource), "SetProvidedProduct", nameof(SourceProductChangedPostfix));

            s_transportColorField = AccessTools.Field(typeof(Transport), "m_transportColor");
            PropertyInfo? accentColorProperty = typeof(Transport).GetProperty(
                nameof(Transport.TransportAccentColor),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            s_transportAccentColorSetter = accentColorProperty?.GetSetMethod(nonPublic: true);
            if (s_transportColorField == null || s_transportAccentColorSetter == null)
            {
                s_log.Warning("Vanilla transport color state could not be accessed — skipping pre-coloring.");
                return;
            }

            Type? transportMbType = typeof(Mafi.Unity.Entities.EntityMb).Assembly.GetType(
                "Mafi.Unity.Factory.Transports.TransportMb");
            if (transportMbType == null)
            {
                s_log.Warning("TransportMb not found — skipping pipe color render patch.");
                return;
            }

            s_transportMbEntityField = AccessTools.Field(transportMbType, "m_transportEntity");
            s_transportMbUpdatePipeColor = AccessTools.Method(transportMbType, "updatePipeColor");
            if (s_transportMbUpdatePipeColor == null)
                s_log.Warning("TransportMb.updatePipeColor not found — renderer cache refresh may be unavailable.");
            else
                PatchPostfix(harmony, transportMbType, "SimUpdateEnd", nameof(TransportMbSimUpdateEndPostfix));
            PatchGetter(harmony, transportMbType, "PipeColor", nameof(PipeColorGetterPostfix));
            PatchGetter(harmony, transportMbType, "PipeAccentColor", nameof(PipeAccentColorGetterPostfix));
            PatchGetter(harmony, transportMbType, "ArePipeColorsDirty", nameof(ArePipeColorsDirtyGetterPostfix));

            Type? transportsRendererType = typeof(Mafi.Unity.Entities.EntityMb).Assembly.GetType(
                "Mafi.Unity.Factory.Transports.InstancedChunkBasedTransportsRenderer");
            if (transportsRendererType == null)
            {
                s_log.Warning("InstancedChunkBasedTransportsRenderer not found — paused pipe color refresh may be unavailable.");
            }
            else
            {
                s_transportsRendererPrepareTransportUpdates =
                    AccessTools.Method(transportsRendererType, "prepareTransportUpdates");
                if (s_transportsRendererPrepareTransportUpdates == null)
                {
                    s_log.Warning("InstancedChunkBasedTransportsRenderer.prepareTransportUpdates not found — paused pipe color refresh may be unavailable.");
                }
                else
                {
                    PatchPostfix(
                        harmony,
                        transportsRendererType,
                        "prepareTransportUpdates",
                        nameof(TransportsRendererPrepareTransportUpdatesPostfix));
                    PatchPostfix(
                        harmony,
                        transportsRendererType,
                        "SyncUpdate",
                        nameof(TransportsRendererSyncUpdatePostfix));
                }
            }

            s_log.Info("Patched vanilla pipe color state and render refresh for pre-coloring.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "PipeColoring.ApplyPatches");
        }
    }

    internal static void Initialize(DependencyResolver resolver)
    {
        if (s_initialized)
            return;

        s_entitiesManager = resolver.Resolve<IEntitiesManager>();
        s_constructionManager = resolver.Resolve<IConstructionManager>();
        s_portProductsResolver = resolver.Resolve<PortProductsResolver>();
        s_entitiesManager.EntityAdded.AddNonSaveable(typeof(PipeColoring), OnEntityChanged);
        s_entitiesManager.EntityRemoved.AddNonSaveable(typeof(PipeColoring), OnEntityRemoved);
        s_constructionManager.EntityConstructed.AddNonSaveable(typeof(PipeColoring), OnEntityConstructed);
        PopulateTransportSnapshot();

        s_gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        s_gameLoopEvents.InputUpdateEnd.AddNonSaveable(typeof(PipeColoring), OnInputUpdateEnd);
        DesignerToolkitSettings.PreColorPipesEnabledChanged += OnSettingChanged;
        lock (s_refreshRequestLock)
        {
            s_clusterRefreshRoots.Clear();
            s_globalRefreshRequested = false;
        }
        Interlocked.Exchange(ref s_refreshRequested, 0);
        s_initialized = true;
        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-INIT] transports={s_transportSnapshot.Count} "
            + $"enabled={DesignerToolkitSettings.PreColorPipesEnabled}.");
    }

    internal static void Dispose()
    {
        if (s_entitiesManager != null)
        {
            try { s_entitiesManager.EntityAdded.RemoveNonSaveable(typeof(PipeColoring), OnEntityChanged); }
            catch { }
            try { s_entitiesManager.EntityRemoved.RemoveNonSaveable(typeof(PipeColoring), OnEntityRemoved); }
            catch { }
        }

        if (s_constructionManager != null)
        {
            try { s_constructionManager.EntityConstructed.RemoveNonSaveable(typeof(PipeColoring), OnEntityConstructed); }
            catch { }
        }

        if (s_gameLoopEvents != null)
        {
            try { s_gameLoopEvents.InputUpdateEnd.RemoveNonSaveable(typeof(PipeColoring), OnInputUpdateEnd); }
            catch { }
        }

        DesignerToolkitSettings.PreColorPipesEnabledChanged -= OnSettingChanged;
        lock (s_pipeColorStateLock)
        {
            s_forceRendererRefresh.Clear();
        }
        lock (s_refreshRequestLock)
        {
            s_clusterRefreshRoots.Clear();
            s_globalRefreshRequested = false;
        }
        lock (s_transportSnapshotLock)
            s_transportSnapshot.Clear();
        s_entitiesManager = null;
        s_constructionManager = null;
        s_portProductsResolver = null;
        s_gameLoopEvents = null;
        Interlocked.Exchange(ref s_refreshRequested, 0);
        s_initialized = false;
    }

    private static void PatchPostfix(Harmony harmony, Type type, string methodName, string postfixName)
    {
        MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == methodName)
            .ToArray();

        if (methods.Length > 0)
        {
            foreach (MethodInfo method in methods)
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(PipeColoring), postfixName));
                BdtDiagnostics.Debug(s_log,
                    $"[DEBUG-PC-PATCH] target={type.FullName}.{method.Name} postfix={postfixName}");
            }
        }
        else
        {
            s_log.Warning($"{type.Name}.{methodName} not found — topology changes may not refresh pipe colors immediately.");
        }
    }

    private static void PatchGetter(Harmony harmony, Type type, string propertyName, string postfixName)
    {
        MethodInfo? getter = AccessTools.PropertyGetter(type, propertyName);
        if (getter == null)
        {
            s_log.Warning($"{type.Name}.{propertyName} getter not found — skipping that pipe color hook.");
            return;
        }

        harmony.Patch(getter, postfix: new HarmonyMethod(typeof(PipeColoring), postfixName));
    }

    private static void PortConnectionChangedPostfix(IoPort port, IoPort otherPort)
    {
        RequestClusterRefresh(port.OwnerEntity, "port-connection");
        RequestClusterRefresh(otherPort.OwnerEntity, "port-connection");
    }

    private static void RecipeChangedPostfix(Machine __instance)
    {
        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-RECIPE] callback machine={__instance.Id} "
            + $"recipes={__instance.RecipesAssigned.Count}.");
        RequestClusterRefresh(__instance, "recipe-change");
    }

    private static void SourceProductChangedPostfix(object __instance)
    {
        if (__instance is IEntityWithPorts entity)
            RequestClusterRefresh(entity, "source-product");
        else
            s_log.Warning("A product-source change did not expose an entity with ports; skipping cluster refresh.");
    }

    private static void PopulateTransportSnapshot()
    {
        if (s_entitiesManager == null)
            return;

        foreach (Transport transport in s_entitiesManager.GetAllEntitiesOfType<Transport>())
        {
            lock (s_transportSnapshotLock)
                s_transportSnapshot.Add(transport);
        }
    }

    private static void OnEntityChanged(IEntity entity)
    {
        if (entity is Transport transport)
        {
            lock (s_transportSnapshotLock)
                s_transportSnapshot.Add(transport);
        }
        if (entity is IEntityWithPorts portsEntity)
            RequestClusterRefresh(portsEntity, "entity-added");
    }

    private static void OnEntityRemoved(IEntity entity)
    {
        if (entity is IEntityWithPorts portsEntity)
        {
            RequestClusterRefresh(portsEntity, "entity-removed");
            foreach (IoPort port in portsEntity.Ports)
            {
                if (port.IsConnected && port.ConnectedPort.HasValue)
                    RequestClusterRefresh(port.ConnectedPort.Value.OwnerEntity, "entity-removed");
            }
        }

        if (entity is Transport transport)
        {
            lock (s_transportSnapshotLock)
                s_transportSnapshot.Remove(transport);
            lock (s_pipeColorStateLock)
            {
                s_forceRendererRefresh.Remove(transport);
            }
        }
    }

    private static void OnEntityConstructed(IStaticEntity entity)
    {
        if (entity is IEntityWithPorts portsEntity)
            RequestClusterRefresh(portsEntity, "entity-constructed");
    }

    private static void OnSettingChanged(bool enabled)
    {
        if (enabled)
            RequestGlobalRefresh("setting-enabled");
        else
        {
            lock (s_pipeColorStateLock)
                s_forceRendererRefresh.Clear();
        }
    }

    private static void OnInputUpdateEnd()
    {
        if (!s_initialized || Interlocked.Exchange(ref s_refreshRequested, 0) == 0)
            return;

        List<IEntityWithPorts> clusterRoots;
        bool globalRefresh;
        lock (s_refreshRequestLock)
        {
            clusterRoots = new List<IEntityWithPorts>(s_clusterRefreshRoots);
            s_clusterRefreshRoots.Clear();
            globalRefresh = s_globalRefreshRequested;
            s_globalRefreshRequested = false;
        }

        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-CONSUME] InputUpdateEnd consumed refresh; "
            + $"enabled={DesignerToolkitSettings.PreColorPipesEnabled} "
            + $"global={globalRefresh} roots={clusterRoots.Count}.");
        if (!DesignerToolkitSettings.PreColorPipesEnabled)
            return;

        if (globalRefresh)
            RefreshAllPipeColors();
        else if (clusterRoots.Count != 0)
            RefreshPipeColorClusters(clusterRoots);
    }

    private static void RequestClusterRefresh(IEntityWithPorts entity, string reason)
    {
        if (!s_initialized)
            return;

        lock (s_refreshRequestLock)
            s_clusterRefreshRoots.Add(entity);
        Interlocked.Exchange(ref s_refreshRequested, 1);
        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-REQUEST] reason={reason} scope=cluster entity={entity.Id}.");
    }

    private static void RequestGlobalRefresh(string reason)
    {
        if (!s_initialized)
            return;

        lock (s_refreshRequestLock)
            s_globalRefreshRequested = true;
        Interlocked.Exchange(ref s_refreshRequested, 1);
        BdtDiagnostics.Debug(s_log, $"[DEBUG-PC-REQUEST] reason={reason} scope=global.");
    }

    private static void RequestRendererRefreshLocked(Transport transport)
    {
        s_forceRendererRefreshGeneration++;
        s_forceRendererRefresh[transport] = s_forceRendererRefreshGeneration;
    }

    private static void TransportsRendererPrepareTransportUpdatesPostfix()
    {
        lock (s_pipeColorStateLock)
        {
            if (s_forceRendererRefresh.Count == 0)
                return;

            int acknowledgedCount = s_forceRendererRefresh.Count;
            s_forceRendererRefresh.Clear();
            BdtDiagnostics.Debug(s_log,
                $"[DEBUG-PC-RENDER] prepared renderer color snapshot pending={acknowledgedCount}.");
        }
    }

    private static void TransportsRendererSyncUpdatePostfix(object __instance, GameTime time)
    {
        if (s_transportsRendererPrepareTransportUpdates == null || !time.IsGamePaused)
            return;

        int pendingCount;
        lock (s_pipeColorStateLock)
            pendingCount = s_forceRendererRefresh.Count;

        if (pendingCount == 0)
            return;

        try
        {
            // Recipe and topology callbacks can run after vanilla's sim-side
            // preparation, including while paused. Prepare one fresh UI-side
            // color snapshot after the normal sync swap so the next render pass
            // consumes the BDT color without touching simulation state.
            s_transportsRendererPrepareTransportUpdates.Invoke(__instance, null);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Could not prepare renderer pipe color snapshot: {ex.Message}");
        }
    }

    private static void TransportMbSimUpdateEndPostfix(object __instance)
    {
        if (s_transportMbUpdatePipeColor == null
            || !TryGetTransport(__instance, out Transport transport)
            || !IsPipeTransport(transport))
            return;

        lock (s_pipeColorStateLock)
        {
            if (!s_forceRendererRefresh.ContainsKey(transport))
                return;
        }

        try
        {
            // TransportMb.SimUpdateEnd is called from the renderer's
            // UpdateEndForUi loop. Refresh vanilla's MB cache here without
            // changing ProductsStateVersion or any simulation state.
            s_transportMbUpdatePipeColor.Invoke(__instance, null);
            BdtDiagnostics.Debug(s_log,
                $"[DEBUG-PC-RENDER] transport={transport.Id} refreshed TransportMb color cache.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Could not refresh TransportMb color cache for transport {transport.Id}: {ex.Message}");
        }
    }

    private static void RefreshPipeColorClusters(List<IEntityWithPorts> roots)
    {
        var visited = new HashSet<IEntityWithPorts>();
        var pipes = new HashSet<Transport>();
        var pending = new Queue<IEntityWithPorts>(roots);

        while (pending.Count != 0)
        {
            IEntityWithPorts entity = pending.Dequeue();
            if (!visited.Add(entity))
                continue;

            if (entity is Transport transport && IsPipeTransport(transport))
                pipes.Add(transport);

            foreach (IoPort port in entity.Ports)
            {
                if (port.IsConnected && port.ConnectedPort.HasValue)
                    pending.Enqueue(port.ConnectedPort.Value.OwnerEntity);
            }
        }

        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-CLUSTER] roots={roots.Count} entities={visited.Count} pipes={pipes.Count}.");
        RefreshPipeColors(pipes, "cluster", visited.Count);
    }

    private static void RefreshAllPipeColors()
    {
        if (s_entitiesManager == null || s_portProductsResolver == null)
            return;

        List<Transport> transports;
        lock (s_transportSnapshotLock)
            transports = new List<Transport>(s_transportSnapshot);

        RefreshPipeColors(transports, "global", transports.Count);
    }

    private static void RefreshPipeColors(
        IEnumerable<Transport> transports,
        string scope,
        int candidateCount)
    {
        if (s_portProductsResolver == null)
            return;

        int pipeCount = 0;
        int skippedFilledCount = 0;
        int resolvedCount = 0;
        int unresolvedCount = 0;
        int changedCount = 0;
        foreach (Transport transport in transports)
        {
            if (!IsPipeTransport(transport))
                continue;

            pipeCount++;
            if (transport.TransportedProducts.Count != 0)
            {
                skippedFilledCount++;
                BdtDiagnostics.Trace(s_log,
                    $"[DEBUG-PC-SKIP] transport={transport.Id} "
                    + $"reason=filled products={transport.TransportedProducts.Count}.");
                continue;
            }

            if (TryResolvePreview(transport, out PreviewColor preview))
            {
                resolvedCount++;
                if (TrySetVanillaColor(transport, preview))
                {
                    changedCount++;
                    lock (s_pipeColorStateLock)
                        RequestRendererRefreshLocked(transport);
                }
            }
            else
                unresolvedCount++;
        }

        BdtDiagnostics.Debug(s_log,
            $"[DEBUG-PC-REFRESH] scope={scope} candidates={candidateCount} pipes={pipeCount} "
            + $"skippedFilled={skippedFilledCount} resolved={resolvedCount} "
            + $"unresolved={unresolvedCount} changed={changedCount}.");
    }

    private static bool TrySetVanillaColor(Transport transport, PreviewColor preview)
    {
        if (AreEqual(
                new PreviewColor(transport.TransportColor, transport.TransportAccentColor),
                preview))
        {
            return false;
        }

        if (s_transportColorField == null || s_transportAccentColorSetter == null)
            return false;

        try
        {
            ColorRgba oldBody = transport.TransportColor;
            ColorRgba oldAccent = transport.TransportAccentColor;
            s_transportColorField.SetValue(transport, preview.Body);
            s_transportAccentColorSetter.Invoke(transport, new object[] { preview.Accent });
            BdtDiagnostics.Debug(s_log,
                $"[DEBUG-PC-APPLY] transport={transport.Id} "
                + $"body={FormatColor(oldBody)}->{FormatColor(preview.Body)} "
                + $"accent={FormatColor(oldAccent)}->{FormatColor(preview.Accent)}.");
            return true;
        }
        catch (Exception ex)
        {
            s_log.Warning($"Could not seed vanilla color for transport {transport.Id}: {ex.Message}");
            return false;
        }
    }

    private static bool IsPipeTransport(Transport transport)
    {
        if (!transport.IsConstructed || transport.IsDestroyed)
            return false;

        TransportProto.Gfx graphics = transport.Prototype.Graphics;
        if (!graphics.UsePerProductColoring || graphics.RenderProducts)
            return false;

        ProductType allowedType = transport.Prototype.PortsShape.AllowedProductType;
        return allowedType.Matches(FluidProductProto.ProductType)
            || allowedType.Matches(MoltenProductProto.ProductType);
    }

    private static bool AreEqual(PreviewColor left, PreviewColor right)
    {
        return left.Body == right.Body && left.Accent == right.Accent;
    }

    private static string FormatColor(ColorRgba color)
        => $"{color.R},{color.G},{color.B}";

    private static bool TryResolvePreview(Transport start, out PreviewColor preview)
    {
        preview = default(PreviewColor);
        if (s_portProductsResolver == null
            || !IsPipeTransport(start)
            || start.TransportedProducts.Count != 0)
            return false;

        var foundProducts = new Dictionary<ProductProto.ID, ProductProto>();
        var visited = new HashSet<IEntityWithPorts>();
        VisitTransportInput(start, 0, visited, foundProducts);
        if (foundProducts.Count == 0)
        {
            BdtDiagnostics.Trace(s_log,
                $"[DEBUG-PC-RESOLVE] transport={start.Id} "
                + "result=no-fluid-or-molten-source.");
            return false;
        }

        int bodyR = 0;
        int bodyG = 0;
        int bodyB = 0;
        int accentR = 0;
        int accentG = 0;
        int accentB = 0;
        int count = 0;

        foreach (ProductProto product in foundProducts.Values)
        {
            AddProductColor(product, ref bodyR, ref bodyG, ref bodyB, ref accentR, ref accentG, ref accentB);
            count++;
        }

        if (count == 0)
            return false;

        preview = new PreviewColor(
            new ColorRgba(bodyR / count, bodyG / count, bodyB / count),
            new ColorRgba(accentR / count, accentG / count, accentB / count));
        BdtDiagnostics.Trace(s_log,
            $"[DEBUG-PC-RESOLVE] transport={start.Id} "
            + $"sources={string.Join(",", foundProducts.Values.Select(product => product.Id.Value))} "
            + $"body={FormatColor(preview.Body)} accent={FormatColor(preview.Accent)}.");
        return true;
    }

    private static void VisitTransportInput(
        Transport transport,
        int transitions,
        HashSet<IEntityWithPorts> visited,
        Dictionary<ProductProto.ID, ProductProto> foundProducts)
    {
        if (transitions >= MAX_UPSTREAM_TRANSITIONS || !visited.Add(transport))
            return;

        IoPort input = transport.StartInputPort;
        if (input.IsConnected && input.ConnectedPort.HasValue)
        {
            VisitIncoming(input.ConnectedPort.Value, transitions + 1, visited, foundProducts);
        }
    }

    private static void VisitIncoming(
        IoPort connectedPort,
        int transitions,
        HashSet<IEntityWithPorts> visited,
        Dictionary<ProductProto.ID, ProductProto> foundProducts)
    {
        if (transitions > MAX_UPSTREAM_TRANSITIONS || !connectedPort.IsConnectedAsOutput)
            return;

        IEntityWithPorts owner = connectedPort.OwnerEntity;
        if (owner is ProductsSourceEntity source)
        {
            if (source.ProvidedProduct.HasValue)
                AddFluidOrMoltenProduct(source.ProvidedProduct.Value, foundProducts);
            return;
        }

        if (owner is UniversalProductsSource universalSource)
        {
            if (universalSource.ProvidedProduct.HasValue)
                AddFluidOrMoltenProduct(universalSource.ProvidedProduct.Value, foundProducts);
            return;
        }

        if (owner is Transport upstreamTransport)
        {
            if (IsPipeTransport(upstreamTransport))
                VisitTransportInput(upstreamTransport, transitions, visited, foundProducts);
            return;
        }

        if (owner is MiniZipper || owner is Zipper || owner is Lift)
        {
            if (!visited.Add(owner) || transitions >= MAX_UPSTREAM_TRANSITIONS)
                return;

            foreach (IoPort port in owner.Ports)
            {
                if (!port.IsConnectedAsInput || !port.ConnectedPort.HasValue)
                    continue;

                VisitIncoming(port.ConnectedPort.Value, transitions + 1, visited, foundProducts);
            }
            return;
        }

        ImmutableArray<ProductProto> products = s_portProductsResolver!.GetPortProducts(connectedPort);
        foreach (ProductProto product in products)
            AddFluidOrMoltenProduct(product, foundProducts);
    }

    private static void AddFluidOrMoltenProduct(
        ProductProto product,
        Dictionary<ProductProto.ID, ProductProto> foundProducts)
    {
        if (product is FluidProductProto || product is MoltenProductProto)
            foundProducts[product.Id] = product;
    }

    private static void AddProductColor(
        ProductProto product,
        ref int bodyR,
        ref int bodyG,
        ref int bodyB,
        ref int accentR,
        ref int accentG,
        ref int accentB)
    {
        ColorRgba body = product.Graphics.TransportColor;
        ColorRgba accent = product.Graphics.TransportAccentColor;
        bodyR += body.R;
        bodyG += body.G;
        bodyB += body.B;
        accentR += accent.R;
        accentG += accent.G;
        accentB += accent.B;
    }

    private static bool TryGetTransport(object instance, out Transport transport)
    {
        transport = null!;
        if (s_transportMbEntityField == null)
            return false;

        Transport? value = s_transportMbEntityField.GetValue(instance) as Transport;
        if (value == null)
            return false;

        transport = value;
        return true;
    }

    private static void PipeColorGetterPostfix(object __instance, ref ColorRgba __result)
    {
        if (TryGetTransport(__instance, out Transport transport)
            && IsPipeTransport(transport)
            && transport.TransportedProducts.Count == 0)
            __result = transport.TransportColor;
    }

    private static void PipeAccentColorGetterPostfix(object __instance, ref ColorRgba __result)
    {
        if (TryGetTransport(__instance, out Transport transport)
            && IsPipeTransport(transport)
            && transport.TransportedProducts.Count == 0)
            __result = transport.TransportAccentColor;
    }

    private static void ArePipeColorsDirtyGetterPostfix(object __instance, ref bool __result)
    {
        if (!TryGetTransport(__instance, out Transport transport))
            return;

        lock (s_pipeColorStateLock)
        {
            if (s_forceRendererRefresh.ContainsKey(transport))
                __result = true;
        }
    }
}
