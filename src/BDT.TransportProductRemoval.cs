// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using iKyIOCd8XMSWfDOjTs;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Input;
using Mafi.Core.Products;
using Mafi.Core.Population;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.Jobs;
using Mafi.Serialization;
using Mafi.Base.Prototypes.Buildings;
using Mafi.Base.Prototypes.Sandbox;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using CoI.AutoHelpers.VanillaAttachments;

namespace CoIDesignerToolkit;

/// <summary>
/// Input command used by the entity-level regular product-removal control.
/// </summary>
[GenerateSerializer(false, null, 0, null)]
public sealed class TransportProductRemovalCmd : InputCommand
{
    public readonly EntityId EntityId;

    private static readonly new Action<object, BlobWriter> s_serializeDataDelayedAction;
    private static readonly new Action<object, BlobReader> s_deserializeDataDelayedAction;

    public TransportProductRemovalCmd(EntityId entityId)
    {
        EntityId = entityId;
    }

    public static void Serialize(TransportProductRemovalCmd value, BlobWriter writer)
    {
        if (writer.TryStartClassSerialization(value))
            writer.EnqueueDataSerialization(value, s_serializeDataDelayedAction);
    }

    public override void SerializeData(BlobWriter writer)
    {
        base.SerializeData(writer);
        EntityId.Serialize(EntityId, writer);
    }

    public static new TransportProductRemovalCmd Deserialize(BlobReader reader)
    {
        if (reader.TryStartClassDeserialization(out TransportProductRemovalCmd obj, null, null, nullObjIsOk: false))
            reader.EnqueueDataDeserialization(obj, s_deserializeDataDelayedAction);
        return obj;
    }

    public override void DeserializeData(BlobReader reader)
    {
        base.DeserializeData(reader);
        reader.SetField(this, nameof(EntityId), EntityId.Deserialize(reader));
    }

    static TransportProductRemovalCmd()
    {
        s_serializeDataDelayedAction = (obj, writer) => ((TransportProductRemovalCmd)obj).SerializeData(writer);
        s_deserializeDataDelayedAction = (obj, reader) => ((TransportProductRemovalCmd)obj).DeserializeData(reader);
    }
}

public sealed class TransportProductRemovalCommandsProcessor : ICommandProcessor<TransportProductRemovalCmd>
{
    public void Invoke(TransportProductRemovalCmd cmd)
    {
        if (TransportProductRemovalManager.TryToggle(cmd.EntityId))
            cmd.SetResultSuccess();
        else
            cmd.SetResultError($"Entity '{cmd.EntityId}' was not found or does not support BDT product removal.");
    }
}

/// <summary>
/// One serializable simulation command for an AoE product-removal action.
/// The entity IDs are deliberately resolved when the command executes so the
/// selection can safely outlive entity destruction, loading, or a save/load
/// boundary before the command is processed.
/// </summary>
[GenerateSerializer(false, null, 0, null)]
public sealed class TransportProductRemovalBatchCmd : InputCommand
{
    public readonly ImmutableArray<EntityId> EntityIds;
    public readonly bool Quick;
    public readonly bool CancelRegular;

    private static readonly new Action<object, BlobWriter> s_serializeDataDelayedAction;
    private static readonly new Action<object, BlobReader> s_deserializeDataDelayedAction;

    public TransportProductRemovalBatchCmd(
        ImmutableArray<EntityId> entityIds,
        bool quick,
        bool cancelRegular = false)
    {
        EntityIds = entityIds;
        Quick = quick;
        CancelRegular = cancelRegular;
    }

    public static void Serialize(TransportProductRemovalBatchCmd value, BlobWriter writer)
    {
        if (writer.TryStartClassSerialization(value))
            writer.EnqueueDataSerialization(value, s_serializeDataDelayedAction);
    }

    public override void SerializeData(BlobWriter writer)
    {
        base.SerializeData(writer);
        ImmutableArray<EntityId>.Serialize(EntityIds, writer);
        writer.WriteBool(Quick);
        writer.WriteBool(CancelRegular);
    }

    public static new TransportProductRemovalBatchCmd Deserialize(BlobReader reader)
    {
        if (reader.TryStartClassDeserialization(out TransportProductRemovalBatchCmd obj, null, null, nullObjIsOk: false))
            reader.EnqueueDataDeserialization(obj, s_deserializeDataDelayedAction);
        return obj;
    }

    public override void DeserializeData(BlobReader reader)
    {
        base.DeserializeData(reader);
        reader.SetField(this, nameof(EntityIds), ImmutableArray<EntityId>.Deserialize(reader));
        reader.SetField(this, nameof(Quick), reader.ReadBool());
        reader.SetField(this, nameof(CancelRegular), reader.ReadBool());
    }

    static TransportProductRemovalBatchCmd()
    {
        s_serializeDataDelayedAction = (obj, writer) => ((TransportProductRemovalBatchCmd)obj).SerializeData(writer);
        s_deserializeDataDelayedAction = (obj, reader) => ((TransportProductRemovalBatchCmd)obj).DeserializeData(reader);
    }
}

public sealed class TransportProductRemovalBatchCommandsProcessor : ICommandProcessor<TransportProductRemovalBatchCmd>
{
    public void Invoke(TransportProductRemovalBatchCmd cmd)
    {
        TransportProductRemovalManager.ExecuteBatch(cmd.EntityIds, cmd.Quick, cmd.CancelRegular);
        // Vanilla removal commands fail silently for stale or ineligible
        // targets. AoE batches deliberately do the same from the player's
        // perspective; individual diagnostics remain in the log.
        cmd.SetResultSuccess();
    }
}

/// <summary>
/// Shared simulation-side removal behavior for supported non-Transport entities.
/// Runtime clearing buffers are deliberately kept outside vanilla save graphs and
/// are rebuilt from live entity buffers after save traversal completes.
/// </summary>
public static class TransportProductRemovalManager
{
    private const int SCHEMA_VERSION = 1;
    public const string CONFIG_KEY = "bdtTransportRemovalOrdersStateJson";

    private sealed class PersistedOrder
    {
        public int EntityId;
        public string PrototypeId = string.Empty;
        public readonly Dictionary<string, int> RemainingProducts = new Dictionary<string, int>();
    }

    private sealed class EntityRemovalOrder
    {
        public readonly ITransportProductRemovalAdapter Adapter;
        public readonly List<EntityClearingBuffer> Buffers = new List<EntityClearingBuffer>();

        public EntityRemovalOrder(ITransportProductRemovalAdapter adapter)
        {
            Adapter = adapter;
        }
    }

    private sealed class QuickRemovalTarget
    {
        public readonly IEntity Entity;
        public readonly ITransportProductRemovalAdapter? Adapter;
        public readonly Upoints Cost;

        public QuickRemovalTarget(IEntity entity, ITransportProductRemovalAdapter? adapter, Upoints cost)
        {
            Entity = entity;
            Adapter = adapter;
            Cost = cost;
        }
    }

    private static readonly MethodInfo? s_transportClearAllProductsMethod = typeof(Transport).GetMethod(
        "clearAllProducts",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly ModLogger s_log = new ModLogger("BDT.TransportProductRemoval");
    private static readonly Dictionary<int, EntityRemovalOrder> s_orders = new Dictionary<int, EntityRemovalOrder>();
    private static readonly FieldInfo? s_registeredOutputJobsField =
        typeof(RegisteredOutputBuffer).GetField("m_jobs", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? s_allReservedJobsProperty =
        typeof(VehicleJobs).GetProperty("AllJobs", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? s_cargoPickupVehicleField =
        typeof(CargoPickUpJob).GetField("m_vehicleForCargoJob", BindingFlags.Instance | BindingFlags.NonPublic);

    private static EntitiesManager? s_entitiesManager;
    private static IVehicleBuffersRegistry? s_vehicleBuffersRegistry;
    private static VanillaAttachmentManager? s_vanillaAttachments;
    private static IModStateJsonStore? s_stateStore;
    private static bool s_stateDirty;

    public static void Initialize(
        EntitiesManager entitiesManager,
        IVehicleBuffersRegistry vehicleBuffersRegistry,
        VanillaAttachmentManager vanillaAttachments,
        IModStateJsonStore stateStore)
    {
        Clear();
        s_entitiesManager = entitiesManager;
        s_vehicleBuffersRegistry = vehicleBuffersRegistry;
        s_vanillaAttachments = vanillaAttachments;
        s_stateStore = stateStore;
        LoadStateAndRestoreOrders();
    }

    public static void Clear()
    {
        foreach (EntityRemovalOrder order in new List<EntityRemovalOrder>(s_orders.Values))
            CancelOrder(order);

        s_orders.Clear();
        s_entitiesManager = null;
        s_vehicleBuffersRegistry = null;
        s_vanillaAttachments = null;
        s_stateStore = null;
        s_stateDirty = false;
    }

    public static void SaveState()
    {
        if (s_stateStore == null || !s_stateDirty)
            return;

        try
        {
            string json = BuildStateJson();
            ModStateJsonSaveResult result = s_stateStore.SaveJson(json);
            if (result.Succeeded)
                s_stateDirty = false;
            else
                s_log.Warning($"Failed to save transport removal order state: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            s_log.Error($"Failed to serialize transport removal order state: {ex.Message}");
        }
    }

    public static void OnEntityRemoved(IEntity entity)
    {
        Cancel(entity.Id);
    }

    public static bool IsRegularRemovalActive(IEntity? entity)
    {
        if (entity is Transport transport)
            return transport.IsProductsRemovalInProgress;
        return entity != null && s_orders.ContainsKey(entity.Id.Value);
    }

    public static bool SupportsEntity(IEntity? entity)
    {
        if (IsSourceOrSink(entity))
            return false;
        if (entity is Transport)
            return true;
        return TryCreateAdapter(entity, out _, logFailure: false);
    }

    public static bool TryGetInspectorGroupId(IEntity? entity, out string groupId)
    {
        if (entity is Transport)
        {
            groupId = nameof(Transport);
            return true;
        }

        if (TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false))
        {
            groupId = adapter.InspectorGroupId;
            return true;
        }

        groupId = string.Empty;
        return false;
    }

    public static Quantity GetTotalBufferedProductQuantity(IEntity? entity)
    {
        try
        {
            if (entity is Transport transport)
            {
                Quantity total = Quantity.Zero;
                foreach (TransportedProductMutable product in transport.TransportedProducts)
                    total += product.Quantity;
                return total;
            }

            if (TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false))
                return GetTotalQuantity(adapter);
        }
        catch
        {
        }
        return Quantity.Zero;
    }

    public static bool WouldReceiveRegularRemoval(IEntity? entity)
    {
        try
        {
            if (entity == null || entity.IsDestroyed || IsSourceOrSink(entity))
                return false;

            Dict<ProductProto, Quantity> products = new Dict<ProductProto, Quantity>();
            if (entity is Transport transport)
            {
                if (transport.ConstructionState != ConstructionState.Constructed)
                    return false;
                foreach (TransportedProductMutable transportedProduct in transport.TransportedProducts)
                {
                    if (!transportedProduct.Quantity.IsPositive)
                        continue;
                    ProductProto product = transport.Context.ProductsManager.SlimIdManager.ResolveOrPhantom(transportedProduct.SlimId);
                    if ((product.CanBeDiscarded && !product.IsWaste) || product.CanBeLoadedOnTruck)
                        return true;
                }
                return false;
            }

            if (!TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false) ||
                adapter.StaticEntity.ConstructionState != ConstructionState.Constructed ||
                !TryAddBufferedProducts(adapter, products))
                return false;

            foreach (KeyValuePair<ProductProto, Quantity> product in products)
            {
                if (product.Value.IsPositive &&
                    ((product.Key.CanBeDiscarded && !product.Key.IsWaste) || product.Key.CanBeLoadedOnTruck))
                    return true;
            }
        }
        catch
        {
            // UI preflight mirrors the simulation's silent skip behavior.
        }
        return false;
    }

    public static Upoints GetQuickRemoveCost(
        IList<IEntity> entities,
        ISet<string> enabledGroups,
        out bool canAfford)
    {
        Upoints total = Upoints.Zero;
        canAfford = true;
        IUpointsManager? upointsManager = null;
        bool hasTarget = false;

        foreach (IEntity entity in entities)
        {
            if (entity == null || entity.IsDestroyed ||
                !TryGetInspectorGroupId(entity, out string groupId) ||
                !enabledGroups.Contains(groupId))
                continue;

            try
            {
                Upoints cost;
                bool entityCanAfford;
                if (entity is IEntityWithQuickRemove vanillaQuickRemove)
                {
                    cost = vanillaQuickRemove.GetQuickRemoveCost(out entityCanAfford);
                }
                else if (TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false))
                {
                    cost = adapter.GetQuickRemoveCost(out entityCanAfford);
                }
                else
                {
                    continue;
                }

                hasTarget = true;
                total += cost;
                canAfford &= entityCanAfford;
                upointsManager ??= entity.Context.UpointsManager;
            }
            catch (Exception ex)
            {
                s_log.Warning($"Failed to calculate AoE quick-removal cost for entity {entity.Id}: {ex.Message}");
            }
        }

        if (hasTarget && upointsManager != null)
            canAfford &= upointsManager.CanConsume(total);
        return total;
    }

    public static bool ExecuteBatch(
        ImmutableArray<EntityId> entityIds,
        bool quick,
        bool cancelRegular = false)
    {
        if (s_entitiesManager == null || entityIds.IsEmpty)
            return false;

        List<IEntity> entities = ResolveUniqueEntities(entityIds);
        if (entities.Count == 0)
            return false;

        if (quick)
        {
            List<QuickRemovalTarget> targets = PreflightQuickRemoval(entities, out Upoints total, out IUpointsManager? upointsManager);
            if (targets.Count == 0)
                return false;
            if (upointsManager == null || !upointsManager.CanConsume(total))
                return false;

            bool anySucceeded = false;
            foreach (QuickRemovalTarget target in targets)
            {
                try
                {
                    ClearQuickRemovalTarget(target);
                    target.Entity.Context.UpointsManager.ConsumeExactly(IdsCore.UpointsCategories.QuickRemove, target.Cost);
                    CancelRegularRemovalAfterSuccessfulQuickRemove(target.Entity);
                    anySucceeded = true;
                }
                catch (Exception ex)
                {
                    string adapterKind = target.Adapter?.AdapterKind ?? nameof(Transport);
                    s_log.Error($"AoE quick product removal failed for entity {target.Entity.Id}, prototype '{target.Entity.Prototype.Id}', adapter '{adapterKind}': {ex.Message}");
                }
            }
            return anySucceeded;
        }

        if (cancelRegular)
        {
            bool anyCancelled = false;
            foreach (IEntity entity in entities)
            {
                try
                {
                    if (entity is Transport transport)
                    {
                        if (!transport.IsProductsRemovalInProgress)
                            continue;
                        transport.CancelProductsRemoval();
                        anyCancelled = true;
                    }
                    else if (Cancel(entity.Id))
                    {
                        anyCancelled = true;
                    }
                }
                catch (Exception ex)
                {
                    s_log.Error($"AoE regular product-removal cancellation failed for entity {entity.Id}, prototype '{entity.Prototype.Id}': {ex.Message}");
                }
            }
            return anyCancelled;
        }

        bool anyRegularAction = false;
        foreach (IEntity entity in entities)
        {
            try
            {
                if (entity is Transport transport)
                {
                    transport.RequestProductsRemoval();
                    anyRegularAction = true;
                }
                else if (TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: true))
                {
                    // Match Transport.RequestProductsRemoval(): an explicit
                    // request replaces an existing removal request with one
                    // scoped from the entity's current contents.
                    Cancel(entity.Id);
                    Request(adapter);
                    anyRegularAction = true;
                }
            }
            catch (Exception ex)
            {
                s_log.Error($"AoE regular product removal failed for entity {entity.Id}, prototype '{entity.Prototype.Id}': {ex.Message}");
            }
        }
        return anyRegularAction;
    }

    private static List<IEntity> ResolveUniqueEntities(ImmutableArray<EntityId> entityIds)
    {
        List<IEntity> result = new List<IEntity>();
        HashSet<int> seen = new HashSet<int>();
        foreach (EntityId entityId in entityIds)
        {
            if (!seen.Add(entityId.Value) ||
                s_entitiesManager == null ||
                !s_entitiesManager.TryGetEntity<IEntity>(entityId, out IEntity entity) ||
                entity.IsDestroyed)
                continue;

            // Preserve the command's fixed ID set through to operation-time
            // preflight. Unsupported and newly invalid entities are skipped
            // by the mode-specific path, where registered adapter failures
            // can also produce their required diagnostic.
            if (!IsSourceOrSink(entity))
                result.Add(entity);
        }
        return result;
    }

    public static bool IsExternalAdapter(IEntity? entity)
    {
        if (entity == null || TransportProductRemovalAdapter.TryCreateBuiltIn(entity, out _))
            return false;
        return TryCreateAdapter(entity, out _, logFailure: false);
    }

    public static bool HasBufferedProducts(IEntity? entity)
    {
        try
        {
            return TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false) &&
                GetTotalQuantity(adapter).IsPositive;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryToggle(EntityId entityId)
    {
        if (s_entitiesManager == null ||
            !s_entitiesManager.TryGetEntity<IEntity>(entityId, out IEntity entity) ||
            !TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: true))
            return false;

        if (s_orders.ContainsKey(entityId.Value))
        {
            Cancel(entityId);
            return true;
        }

        Request(adapter);
        return true;
    }

    public static bool Cancel(EntityId entityId)
    {
        if (!s_orders.TryGetValue(entityId.Value, out EntityRemovalOrder? order))
            return false;

        CancelOrder(order);
        return true;
    }

    public static bool AddBufferedProducts(IEntity entity, Dict<ProductProto, Quantity> products)
    {
        if (entity is Transport transport)
        {
            products.Clear();
            foreach (TransportedProductMutable transportedProduct in transport.TransportedProducts)
            {
                ProductProto product = transport.Context.ProductsManager.SlimIdManager.ResolveOrPhantom(transportedProduct.SlimId);
                Quantity quantity = transportedProduct.Quantity;
                if (products.TryGetValue(product, out Quantity existing))
                    products[product] = existing + quantity;
                else
                    products.Add(product, quantity);
            }
            return true;
        }
        if (!TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: true))
            return false;
        return TryAddBufferedProducts(adapter, products);
    }

    public static int GetBufferStateHash(IEntity? entity)
    {
        try
        {
            return TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false)
                ? GetBufferStateHash(adapter)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static Quantity GetMaxBufferSize(IEntity entity)
    {
        return TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false)
            ? adapter.MaxBufferSize
            : Quantity.Zero;
    }

    public static Upoints GetQuickRemoveCost(IEntity entity, out bool canAfford)
    {
        try
        {
            if (TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: false))
                return adapter.GetQuickRemoveCost(out canAfford);
        }
        catch
        {
        }
        canAfford = false;
        return Upoints.Zero;
    }

    public static bool QuickRemove(IEntity entity)
    {
        try
        {
            if (!TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: true) || adapter.Entity.IsDestroyed)
                return false;
            Upoints cost = adapter.GetQuickRemoveCost(out bool canAfford);
            if (!cost.IsPositive || !canAfford)
                return true;

            adapter.ClearAllProductsForQuickRemove();
            adapter.Entity.Context.UpointsManager.ConsumeExactly(IdsCore.UpointsCategories.QuickRemove, cost);
            CancelRegularRemovalAfterSuccessfulQuickRemove(adapter.Entity);
            return true;
        }
        catch (Exception ex)
        {
            s_log.Error($"Quick product removal failed for entity {entity.Id}, prototype '{entity.Prototype.Id}': {ex.Message}");
            return false;
        }
    }

    private static List<QuickRemovalTarget> PreflightQuickRemoval(
        IList<IEntity> entities,
        out Upoints total,
        out IUpointsManager? upointsManager)
    {
        List<QuickRemovalTarget> targets = new List<QuickRemovalTarget>();
        total = Upoints.Zero;
        upointsManager = null;
        foreach (IEntity entity in entities)
        {
            try
            {
                ITransportProductRemovalAdapter? adapter = null;
                Upoints cost;
                if (entity is Transport transport)
                {
                    cost = transport.GetQuickRemoveCost(out _);
                    if (s_transportClearAllProductsMethod == null)
                        throw new MissingMethodException(typeof(Transport).FullName, "clearAllProducts");
                }
                else
                {
                    if (!TryCreateAdapter(entity, out adapter, logFailure: true))
                        continue;
                    cost = adapter.GetQuickRemoveCost(out _);
                }

                if (!cost.IsPositive)
                    continue;
                targets.Add(new QuickRemovalTarget(entity, adapter, cost));
                total += cost;
                upointsManager ??= entity.Context.UpointsManager;
            }
            catch (Exception ex)
            {
                s_log.Error($"AoE quick-removal preflight skipped entity {entity.Id}, prototype '{entity.Prototype.Id}': {ex.Message}");
            }
        }
        return targets;
    }

    private static void ClearQuickRemovalTarget(QuickRemovalTarget target)
    {
        if (target.Adapter != null)
        {
            target.Adapter.ClearAllProductsForQuickRemove();
            return;
        }

        if (target.Entity is Transport transport && s_transportClearAllProductsMethod != null)
        {
            s_transportClearAllProductsMethod.Invoke(transport, null);
            return;
        }
        throw new InvalidOperationException("Quick-removal target has no clearing implementation.");
    }

    private static void CancelRegularRemovalAfterSuccessfulQuickRemove(IEntity entity)
    {
        if (entity is Transport transport)
        {
            if (transport.IsProductsRemovalInProgress)
                transport.CancelProductsRemoval();
            return;
        }
        Cancel(entity.Id);
    }

    private static bool IsSourceOrSink(IEntity? entity)
    {
        return entity is IProductSourceSinkEntity || entity is CheatingProductsSourceSink;
    }

    private static void Request(ITransportProductRemovalAdapter adapter)
    {
        try
        {
            RequestCore(adapter);
        }
        catch (Exception ex)
        {
            s_log.Error(
                $"Regular product removal failed for entity {adapter.Entity.Id}, prototype '{adapter.Entity.Prototype.Id}', " +
                $"adapter '{adapter.AdapterKind}': {ex.Message}");
            Cancel(adapter.Entity.Id);
        }
    }

    private static void RequestCore(ITransportProductRemovalAdapter adapter)
    {
        if (s_vehicleBuffersRegistry == null || s_vanillaAttachments == null ||
            adapter.Entity.IsDestroyed || adapter.StaticEntity.ConstructionState != ConstructionState.Constructed)
            return;

        Dict<ProductProto, Quantity> products = new Dict<ProductProto, Quantity>();
        if (!TryAddBufferedProducts(adapter, products))
            return;
        if (products.Count == 0)
            return;

        List<ProductProto> immediateProducts = new List<ProductProto>();
        Dictionary<ProductProto, Quantity> truckProducts = new Dictionary<ProductProto, Quantity>();
        foreach (KeyValuePair<ProductProto, Quantity> product in products)
        {
            if (product.Key.CanBeDiscarded && !product.Key.IsWaste)
            {
                immediateProducts.Add(product.Key);
            }
            else if (product.Key.CanBeLoadedOnTruck && adapter.GetProductQuantity(product.Key).IsPositive)
            {
                truckProducts[product.Key] = product.Value;
            }
        }

        if (truckProducts.Count > 0)
        {
            if (!TryCreateOrder(adapter, truckProducts, markStateDirty: true))
                return;
        }

        foreach (ProductProto product in immediateProducts)
            adapter.RemoveProduct(product, Quantity.MaxValue, reportAsCleared: true);
    }

    private static void OnBufferExhausted(EntityClearingBuffer buffer)
    {
        if (!s_orders.TryGetValue(buffer.Adapter.Entity.Id.Value, out EntityRemovalOrder? order))
            return;

        order.Buffers.Remove(buffer);
        s_vanillaAttachments?.Unregister(buffer);
        s_stateDirty = true;
        if (order.Buffers.Count == 0)
        {
            try
            {
                buffer.Adapter.SetRegularRemovalActive(false);
            }
            catch (Exception ex)
            {
                s_log.Error($"Failed to release regular-removal gate for entity {buffer.Adapter.Entity.Id}: {ex.Message}");
            }
            s_orders.Remove(buffer.Adapter.Entity.Id.Value);
        }
    }

    private static void CancelOrder(EntityRemovalOrder order)
    {
        s_orders.Remove(order.Adapter.Entity.Id.Value);
        try
        {
            order.Adapter.SetRegularRemovalActive(false);
        }
        catch (Exception ex)
        {
            s_log.Error($"Failed to release regular-removal gate for entity {order.Adapter.Entity.Id}: {ex.Message}");
        }
        foreach (EntityClearingBuffer buffer in order.Buffers.ToArray())
            s_vanillaAttachments?.Unregister(buffer);
        order.Buffers.Clear();
        s_stateDirty = true;
    }

    private static bool TryCreateOrder(
        ITransportProductRemovalAdapter adapter,
        Dictionary<ProductProto, Quantity> productScopes,
        bool markStateDirty)
    {
        if (s_vehicleBuffersRegistry == null || s_vanillaAttachments == null || productScopes.Count == 0)
            return false;

        EntityRemovalOrder order = new EntityRemovalOrder(adapter);
        s_orders[adapter.Entity.Id.Value] = order;
        try
        {
            foreach (KeyValuePair<ProductProto, Quantity> productScope in productScopes)
            {
                if (!productScope.Value.IsPositive)
                    continue;

                EntityClearingBuffer buffer = new EntityClearingBuffer(
                    adapter,
                    productScope.Key,
                    productScope.Value,
                    s_vehicleBuffersRegistry,
                    OnBufferExhausted);
                order.Buffers.Add(buffer);
                s_vanillaAttachments.Register(buffer);
                if (!buffer.IsAttachedToVanilla)
                    throw new InvalidOperationException($"Failed to register clearing buffer for product '{productScope.Key.Id}'.");
            }

            if (order.Buffers.Count == 0)
            {
                s_orders.Remove(adapter.Entity.Id.Value);
                return false;
            }

            adapter.SetRegularRemovalActive(true);
            if (markStateDirty)
                s_stateDirty = true;
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                adapter.SetRegularRemovalActive(false);
            }
            catch { }
            s_log.Error(
                $"Failed to start regular removal for entity {adapter.Entity.Id}, prototype '{adapter.Entity.Prototype.Id}', " +
                $"adapter '{adapter.AdapterKind}': {ex.Message}");
            CancelOrder(order);
            return false;
        }
    }

    private static void LoadStateAndRestoreOrders()
    {
        if (s_stateStore == null || s_entitiesManager == null)
            return;

        string json = s_stateStore.LoadJson();
        if (string.IsNullOrWhiteSpace(json))
            return;

        List<PersistedOrder> persistedOrders;
        try
        {
            persistedOrders = ParseState(json);
        }
        catch (Exception ex)
        {
            s_log.Error($"Failed to load transport removal order state; resetting it: {ex.Message}");
            s_stateDirty = true;
            SaveState();
            return;
        }

        List<int> missingEntityIds = new List<int>();
        foreach (PersistedOrder persistedOrder in persistedOrders)
        {
            if (s_orders.ContainsKey(persistedOrder.EntityId))
            {
                s_log.Error($"Skipping duplicate transport removal order for entity {persistedOrder.EntityId}.");
                s_stateDirty = true;
                continue;
            }

            EntityId entityId = new EntityId(persistedOrder.EntityId);
            if (!s_entitiesManager.TryGetEntity<IEntity>(entityId, out IEntity entity))
            {
                missingEntityIds.Add(persistedOrder.EntityId);
                s_stateDirty = true;
                continue;
            }

            string currentPrototypeId = entity.Prototype.Id.ToString();
            if (!string.Equals(currentPrototypeId, persistedOrder.PrototypeId, StringComparison.Ordinal))
            {
                s_log.Error(
                    $"Skipping transport removal order for entity {persistedOrder.EntityId}: persisted prototype " +
                    $"'{persistedOrder.PrototypeId}' does not match current prototype '{currentPrototypeId}'.");
                s_stateDirty = true;
                continue;
            }
            if (!TryCreateAdapter(entity, out ITransportProductRemovalAdapter adapter, logFailure: true))
            {
                s_log.Error(
                    $"Skipping transport removal order for entity {persistedOrder.EntityId}, prototype " +
                    $"'{currentPrototypeId}': no compatible adapter is registered.");
                s_stateDirty = true;
                continue;
            }

            Dict<ProductProto, Quantity> currentProducts = new Dict<ProductProto, Quantity>();
            if (!TryAddBufferedProducts(adapter, currentProducts))
            {
                s_stateDirty = true;
                continue;
            }
            Dictionary<ProductProto, Quantity> restoredScopes = new Dictionary<ProductProto, Quantity>();
            foreach (KeyValuePair<string, int> persistedProduct in persistedOrder.RemainingProducts)
            {
                bool restoredProduct = false;
                foreach (KeyValuePair<ProductProto, Quantity> currentProduct in currentProducts)
                {
                    if (!string.Equals(currentProduct.Key.Id.ToString(), persistedProduct.Key, StringComparison.Ordinal))
                        continue;

                    int cappedQuantity = Math.Min(currentProduct.Value.Value, persistedProduct.Value);
                    if (cappedQuantity != persistedProduct.Value)
                        s_stateDirty = true;
                    if (cappedQuantity > 0 && currentProduct.Key.CanBeLoadedOnTruck)
                    {
                        restoredScopes[currentProduct.Key] = new Quantity(cappedQuantity);
                        restoredProduct = true;
                    }
                    break;
                }
                if (!restoredProduct)
                    s_stateDirty = true;
            }

            if (restoredScopes.Count == 0)
            {
                s_stateDirty = true;
                continue;
            }

            if (!TryCreateOrder(adapter, restoredScopes, markStateDirty: false))
                s_stateDirty = true;
        }

        if (missingEntityIds.Count > 0)
        {
            int shownCount = Math.Min(10, missingEntityIds.Count);
            s_log.Info(
                $"Pruned {missingEntityIds.Count} missing transport removal order entity ID(s): " +
                string.Join(", ", missingEntityIds.GetRange(0, shownCount)));
        }
    }

    private static List<PersistedOrder> ParseState(string json)
    {
        object parsed = new JsonParser().Parse(new StringReader(json));
        if (parsed is not Dict<string, object> root)
            throw new InvalidDataException("Root value is not an object.");

        if (!root.TryGetValue("schemaVersion", out object? rawSchema) ||
            !TryReadInt(rawSchema, out int schemaVersion))
        {
            throw new InvalidDataException("schemaVersion is missing or invalid.");
        }
        if (schemaVersion != SCHEMA_VERSION)
            throw new InvalidDataException($"Unsupported schemaVersion {schemaVersion}.");

        if (!root.TryGetValue("orders", out object? rawOrders))
            return new List<PersistedOrder>();
        if (rawOrders is not object[] orders)
            throw new InvalidDataException("orders is not an array.");

        List<PersistedOrder> result = new List<PersistedOrder>();
        foreach (object rawOrder in orders)
        {
            if (rawOrder is not Dict<string, object> order ||
                !order.TryGetValue("entityId", out object? rawEntityId) ||
                !TryReadInt(rawEntityId, out int entityId) || entityId <= 0 ||
                !order.TryGetValue("prototypeId", out object? rawPrototypeId) ||
                rawPrototypeId is not string prototypeId || string.IsNullOrWhiteSpace(prototypeId) ||
                !order.TryGetValue("remainingProducts", out object? rawProducts) ||
                rawProducts is not Dict<string, object> products)
            {
                throw new InvalidDataException("An order record is malformed.");
            }

            PersistedOrder persistedOrder = new PersistedOrder
            {
                EntityId = entityId,
                PrototypeId = prototypeId,
            };
            foreach (KeyValuePair<string, object> product in products)
            {
                if (!TryReadInt(product.Value, out int quantity) || quantity < 0)
                    throw new InvalidDataException($"Invalid remaining quantity for product '{product.Key}'.");
                if (quantity > 0)
                    persistedOrder.RemainingProducts[product.Key] = quantity;
            }
            result.Add(persistedOrder);
        }
        return result;
    }

    private static string BuildStateJson()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"schemaVersion\":").Append(SCHEMA_VERSION).Append(",\"orders\":[");
        bool firstOrder = true;
        foreach (EntityRemovalOrder order in s_orders.Values)
        {
            List<EntityClearingBuffer> activeBuffers = order.Buffers.FindAll(buffer => buffer.RemainingQuantity.IsPositive);
            if (activeBuffers.Count == 0)
                continue;

            if (!firstOrder)
                builder.Append(',');
            firstOrder = false;
            builder.Append("{\"entityId\":").Append(order.Adapter.Entity.Id.Value)
                .Append(",\"prototypeId\":\"").Append(EscapeJson(order.Adapter.Entity.Prototype.Id.ToString()))
                .Append("\",\"remainingProducts\":{");

            bool firstProduct = true;
            foreach (EntityClearingBuffer buffer in activeBuffers)
            {
                if (!firstProduct)
                    builder.Append(',');
                firstProduct = false;
                builder.Append('\"').Append(EscapeJson(buffer.Product.Id.ToString())).Append("\":")
                    .Append(buffer.RemainingQuantity.Value);
            }
            builder.Append("}}");
        }
        builder.Append("]}");
        return builder.ToString();
    }

    private static bool TryReadInt(object? value, out int result)
    {
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }
        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }
        if (value is double doubleValue && doubleValue >= int.MinValue && doubleValue <= int.MaxValue &&
            Math.Abs(doubleValue % 1d) < double.Epsilon)
        {
            result = (int)doubleValue;
            return true;
        }
        result = 0;
        return false;
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool TryCreateAdapter(
        IEntity? entity,
        out ITransportProductRemovalAdapter adapter,
        bool logFailure)
    {
        if (TransportProductRemovalAdapter.TryCreateBuiltIn(entity, out TransportProductRemovalAdapter builtIn))
        {
            adapter = builtIn;
            return true;
        }

        if (TransportProductRemovalAdapterRegistry.TryCreate(entity, out adapter, out string failure))
            return true;

        if (logFailure && entity != null && TransportProductRemovalAdapterRegistry.HasRegistration(entity))
        {
            s_log.Error(
                $"Transport product-removal adapter rejected for entity {entity.Id}, " +
                $"prototype '{entity.Prototype.Id}': {failure}");
        }
        adapter = null!;
        return false;
    }

    private static bool TryAddBufferedProducts(
        ITransportProductRemovalAdapter adapter,
        Dict<ProductProto, Quantity> products)
    {
        try
        {
            products.Clear();
            List<ProductQuantity> snapshot = new List<ProductQuantity>();
            adapter.GetBufferedProducts(snapshot);
            foreach (ProductQuantity productQuantity in snapshot)
            {
                if (productQuantity.IsEmpty)
                    continue;
                if (products.TryGetValue(productQuantity.Product, out Quantity existing))
                    products[productQuantity.Product] = existing + productQuantity.Quantity;
                else
                    products.Add(productQuantity.Product, productQuantity.Quantity);
            }
            return true;
        }
        catch (Exception ex)
        {
            s_log.Error(
                $"Failed to enumerate products for entity {adapter.Entity.Id}, prototype '{adapter.Entity.Prototype.Id}', " +
                $"adapter '{adapter.AdapterKind}': {ex.Message}");
            products.Clear();
            return false;
        }
    }

    private static Quantity GetTotalQuantity(ITransportProductRemovalAdapter adapter)
    {
        List<ProductQuantity> snapshot = new List<ProductQuantity>();
        adapter.GetBufferedProducts(snapshot);
        Quantity total = Quantity.Zero;
        foreach (ProductQuantity productQuantity in snapshot)
        {
            if (productQuantity.IsNotEmpty)
                total += productQuantity.Quantity;
        }
        return total;
    }

    private static int GetBufferStateHash(ITransportProductRemovalAdapter adapter)
    {
        if (adapter.Entity.IsDestroyed)
            return 0;

        int hash = 17;
        hash = hash * 31 + GetTotalQuantity(adapter).Value.GetHashCode();
        hash = hash * 31 + adapter.MaxBufferSize.Value.GetHashCode();
        List<ProductQuantity> snapshot = new List<ProductQuantity>();
        adapter.GetBufferedProducts(snapshot);
        foreach (ProductQuantity productQuantity in snapshot)
        {
            if (!productQuantity.IsNotEmpty)
                continue;
            hash = hash * 31 + productQuantity.Product.Id.Value.GetHashCode();
            hash = hash * 31 + productQuantity.Quantity.Value.GetHashCode();
        }
        return hash;
    }

    private sealed class EntityClearingBuffer : IProductBuffer, ISaveDetachedVanillaAttachment
    {
        private readonly IVehicleBuffersRegistry m_vehicleBuffersRegistry;
        private readonly Action<EntityClearingBuffer> m_onExhausted;
        private readonly StaticPriorityProvider m_priorityProvider =
            new StaticPriorityProvider(BufferStrategy.FullFillAtAnyCost(7));
        private Quantity m_remainingQuantity;
        private RegisteredOutputBuffer? m_registeredWrapper;
        private bool m_isAttached;
        private bool m_isDisposed;

        public ITransportProductRemovalAdapter Adapter { get; }
        public ProductProto Product { get; }
        public Quantity RemainingQuantity => m_remainingQuantity;
        public Quantity UsableCapacity => Quantity.Zero;
        public Quantity Capacity => Quantity;
        public Quantity Quantity =>
            Adapter.GetProductQuantity(Product).Min(m_remainingQuantity);
        public bool IsAttachedToVanilla => m_isAttached;
        public string SaveDetachmentReason => "BDT regular product-removal clearing buffer is runtime-only.";

        public EntityClearingBuffer(
            ITransportProductRemovalAdapter adapter,
            ProductProto product,
            Quantity remainingQuantity,
            IVehicleBuffersRegistry vehicleBuffersRegistry,
            Action<EntityClearingBuffer> onExhausted)
        {
            Adapter = adapter;
            Product = product;
            m_remainingQuantity = remainingQuantity;
            m_vehicleBuffersRegistry = vehicleBuffersRegistry;
            m_onExhausted = onExhausted;
        }

        public Quantity StoreAsMuchAs(Quantity quantity)
        {
            return quantity;
        }

        public Quantity RemoveAsMuchAs(Quantity maxQuantity)
        {
            if (m_isDisposed)
                return Quantity.Zero;

            Quantity removed = Adapter.RemoveProduct(Product, maxQuantity.Min(m_remainingQuantity), reportAsCleared: false);
            m_remainingQuantity -= removed;
            if (removed.IsPositive)
                s_stateDirty = true;
            if (Quantity.IsNotPositive)
                m_onExhausted(this);
            return removed;
        }

        public void AttachToVanilla()
        {
            if (m_isDisposed || m_isAttached || Adapter.Entity.IsDestroyed)
                return;

            m_isAttached = m_vehicleBuffersRegistry.TryRegisterOutputBuffer(
                Adapter.StaticEntity,
                this,
                m_priorityProvider,
                alwaysEnabled: true,
                useFallbackIfNeeded: true,
                allowPickupAtDistanceWhenBlocked: true);
            RegisteredOutputBuffer? registered =
                m_vehicleBuffersRegistry.TryGetOutputBuffer(Adapter.StaticEntity, Product).ValueOrNull;
            if (m_isAttached)
            {
                RegisteredOutputBuffer? wrapper = registered;
                if (wrapper?.WrapsBuffer(this) == true)
                    m_registeredWrapper = wrapper;
            }
        }

        public void DetachFromVanilla()
        {
            RegisteredOutputBuffer? wrapperBefore = m_registeredWrapper;
            if (wrapperBefore == null)
            {
                RegisteredOutputBuffer? registryWrapper =
                    m_vehicleBuffersRegistry.TryGetOutputBuffer(Adapter.StaticEntity, Product).ValueOrNull;
                if (registryWrapper?.WrapsBuffer(this) == true)
                    wrapperBefore = registryWrapper;
            }
            if (wrapperBefore != null)
                cancelReservedVehicleJobs(wrapperBefore);
            bool unregistered = m_vehicleBuffersRegistry.TryUnregisterOutputBuffer(this);
            m_registeredWrapper = null;
            m_isAttached = false;
            if (!unregistered && wrapperBefore != null)
                s_log.Warning($"Failed to unregister clearing buffer for entity {Adapter.Entity.Id}, product '{Product.Id}'.");
        }

        public void DisposeRuntime()
        {
            DetachFromVanilla();
            m_isDisposed = true;
        }

        private static int cancelReservedVehicleJobs(RegisteredOutputBuffer wrapper)
        {
            if (s_registeredOutputJobsField?.GetValue(wrapper) is not VehicleJobs jobs ||
                s_allReservedJobsProperty?.GetValue(jobs) is not IEnumerable<IVehicleJob> reservedJobs)
            {
                s_log.Warning("Could not inspect reserved output-buffer jobs before detachment.");
                return 0;
            }

            List<PathFindingEntity> vehicles = new List<PathFindingEntity>();
            foreach (IVehicleJob job in reservedJobs)
            {
                if (job is not CargoPickUpJob cargoPickupJob)
                    continue;

                if (s_cargoPickupVehicleField?.GetValue(cargoPickupJob) is PathFindingEntity vehicle &&
                    !vehicles.Contains(vehicle))
                {
                    vehicles.Add(vehicle);
                }
            }

            foreach (PathFindingEntity vehicle in vehicles)
                vehicle.CancelAllJobsAndResetState();

            return vehicles.Count;
        }
    }

}
