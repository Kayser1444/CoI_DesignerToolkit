// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Products;
using Mafi.Core.Input;
using Mafi.Unity.UiToolkit.Component;

namespace CoIDesignerToolkit;

/// <summary>
/// Explicit opt-in contract for a modded entity that wants BDT transport
/// product-removal support. Implementations own their private buffers and
/// invariants; BDT only orchestrates the order and vehicle-facing buffer.
/// </summary>
public interface ITransportProductRemovalAdapter
{
    IEntity Entity { get; }
    IStaticEntity StaticEntity { get; }
    string AdapterKind { get; }
    string InspectorGroupId { get; }
    bool IsValid { get; }
    bool SupportsRegularRemoval { get; }
    bool SupportsQuickRemoval { get; }
    bool SupportsInspectorControl { get; }
    /// <summary>
    /// Must be true for any infinite product source or sink. Such entities are
    /// categorically excluded even when the remaining adapter contract is valid.
    /// </summary>
    bool IsProductSourceOrSink { get; }
    Quantity MaxBufferSize { get; }

    /// <summary>
    /// Replaces the destination contents with a complete snapshot of all
    /// products in every internal buffer, including pending/input buffers.
    /// </summary>
    void GetBufferedProducts(IList<ProductQuantity> destination);

    Quantity GetProductQuantity(ProductProto product);

    /// <summary>
    /// Removes at most <paramref name="maxQuantity"/> of one product from all
    /// internal buffers, preserving the entity's cached quantities and queue
    /// invariants. The adapter must use vanilla product accounting when
    /// <paramref name="reportAsCleared"/> is true.
    /// </summary>
    Quantity RemoveProduct(ProductProto product, Quantity maxQuantity, bool reportAsCleared);

    Upoints GetQuickRemoveCost(out bool canAfford);

    /// <summary>
    /// Clears all buffered products and reports them through vanilla product
    /// accounting. Unity is deliberately not consumed here: BDT charges the
    /// preflighted cost only after this method returns successfully.
    /// </summary>
    void ClearAllProductsForQuickRemove();

    /// <summary>
    /// Enables or disables the adapter's normal input/output gate for an
    /// active regular-removal order. This must not change the player's
    /// enabled/paused state and must be idempotent.
    /// </summary>
    void SetRegularRemovalActive(bool active);
}

/// <summary>
/// Prototype-keyed registration point for compatible modded entities.
/// Registrations are process-local runtime metadata and are never serialized.
/// </summary>
public static class TransportProductRemovalAdapterRegistry
{
    private sealed class Registration : IDisposable
    {
        private readonly string m_prototypeId;
        private readonly Func<IEntity, ITransportProductRemovalAdapter?> m_factory;
        private bool m_disposed;

        public Registration(string prototypeId, Func<IEntity, ITransportProductRemovalAdapter?> factory)
        {
            m_prototypeId = prototypeId;
            m_factory = factory;
        }

        public Func<IEntity, ITransportProductRemovalAdapter?> Factory => m_factory;

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            lock (s_registrations)
            {
                if (s_registrations.TryGetValue(m_prototypeId, out Registration? current) &&
                    ReferenceEquals(current, this))
                {
                    s_registrations.Remove(m_prototypeId);
                }
            }
        }
    }

    private static readonly Dictionary<string, Registration> s_registrations =
        new Dictionary<string, Registration>(StringComparer.Ordinal);

    /// <summary>
    /// Registers a factory for one exact prototype ID. The returned handle
    /// unregisters the factory and should be disposed with the registering
    /// mod's runtime lifecycle.
    /// </summary>
    public static IDisposable Register(
        string prototypeId,
        Func<IEntity, ITransportProductRemovalAdapter?> factory)
    {
        if (string.IsNullOrWhiteSpace(prototypeId))
            throw new ArgumentException("A stable prototype ID is required.", nameof(prototypeId));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        string normalizedPrototypeId = prototypeId.Trim();
        lock (s_registrations)
        {
            if (s_registrations.ContainsKey(normalizedPrototypeId))
                throw new InvalidOperationException(
                    $"A transport product-removal adapter is already registered for prototype '{normalizedPrototypeId}'.");

            Registration registration = new Registration(normalizedPrototypeId, factory);
            s_registrations.Add(normalizedPrototypeId, registration);
            return registration;
        }
    }

    /// <summary>
    /// Removes a registration by stable prototype ID. This is intended for
    /// mod disposal and is safe to call repeatedly.
    /// </summary>
    public static bool Unregister(string prototypeId)
    {
        if (string.IsNullOrWhiteSpace(prototypeId))
            return false;
        lock (s_registrations)
            return s_registrations.Remove(prototypeId.Trim());
    }

    internal static bool TryCreate(
        IEntity? entity,
        out ITransportProductRemovalAdapter adapter,
        out string failure)
    {
        adapter = null!;
        failure = string.Empty;
        if (entity == null || entity.IsDestroyed)
        {
            failure = "entity is null or destroyed";
            return false;
        }

        string prototypeId = entity.Prototype.Id.ToString();
        Registration? registration;
        lock (s_registrations)
            s_registrations.TryGetValue(prototypeId, out registration);
        if (registration == null)
        {
            failure = "no explicit registration";
            return false;
        }

        try
        {
            ITransportProductRemovalAdapter? candidate = registration.Factory(entity);
            if (candidate == null)
            {
                failure = "factory returned null";
                return false;
            }
            if (!ReferenceEquals(candidate.Entity, entity) && candidate.Entity.Id != entity.Id)
            {
                failure = "adapter entity does not match the registered entity";
                return false;
            }
            if (!string.Equals(candidate.Entity.Prototype.Id.ToString(), prototypeId, StringComparison.Ordinal))
            {
                failure = $"adapter entity prototype does not match registered prototype '{prototypeId}'";
                return false;
            }
            if (candidate.StaticEntity == null ||
                !ReferenceEquals(candidate.StaticEntity, candidate.Entity) && candidate.StaticEntity.Id != candidate.Entity.Id)
            {
                failure = "adapter does not expose the registered entity as a static entity";
                return false;
            }
            if (string.IsNullOrWhiteSpace(candidate.AdapterKind))
            {
                failure = "adapter '<missing>' has no adapter kind";
                return false;
            }
            if (string.IsNullOrWhiteSpace(candidate.InspectorGroupId))
            {
                failure = $"adapter '{candidate.AdapterKind}' has no inspector group ID";
                return false;
            }
            if (!candidate.IsValid ||
                !candidate.SupportsRegularRemoval ||
                !candidate.SupportsQuickRemoval ||
                !candidate.SupportsInspectorControl)
            {
                failure = $"adapter '{candidate.AdapterKind}' does not support the complete regular/quick/inspector contract";
                return false;
            }
            if (candidate.IsProductSourceOrSink)
            {
                failure = $"adapter '{candidate.AdapterKind}' represents an excluded product source or sink";
                return false;
            }

            adapter = candidate;
            return true;
        }
        catch (Exception ex)
        {
            failure = $"factory or validation threw {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    internal static bool HasRegistration(IEntity? entity)
    {
        if (entity == null || entity.IsDestroyed)
            return false;
        lock (s_registrations)
            return s_registrations.ContainsKey(entity.Prototype.Id.ToString());
    }
}

/// <summary>
/// UI seam for a modded entity's inspector. The registering mod remains
/// responsible for patching its inspector constructor and choosing the
/// appropriate buffer panel; this helper contributes BDT's shared combined
/// regular/quick interaction to that panel.
/// </summary>
public static class TransportProductRemovalUi
{
    public static void AddCombinedRemovalControl(
        UiComponent parent,
        IInputScheduler scheduler,
        Func<IEntity?> entityProvider,
        bool absolute = true)
    {
        ContentDisplayPatches.AddRemovalControl(parent, scheduler, entityProvider, absolute);
    }
}
