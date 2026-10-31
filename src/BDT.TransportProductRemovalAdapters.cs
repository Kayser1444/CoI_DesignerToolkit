// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Lifts;
// PRE_4_3_COMPAT: legacy branches/API guards only; remove after Update 5 or when pre-4.3 support ends.
#if COI_UPDATE_4_3
using Mafi.Core.Factory.Transports;
#endif
using Mafi.Core.Factory.Sorters;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Products;

namespace CoIDesignerToolkit;

/// <summary>
/// Explicit adapter for the built-in non-Transport entities supported by BDT
/// product removal. Keeping reflection details here prevents the removal
/// manager, persistence, jobs, and UI from depending on four buffer shapes.
/// </summary>
internal sealed class TransportProductRemovalAdapter : ITransportProductRemovalAdapter
{
    private const BindingFlags INSTANCE_FIELD = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo? s_zipperInput = GetField<Zipper>("m_inputBuffer");
    private static readonly FieldInfo? s_zipperOutput = GetField<Zipper>("m_outputBuffer");
    private static readonly FieldInfo? s_zipperInputQuantity = GetField<Zipper>("<QuantityInInputBuffer>k__BackingField");
    private static readonly FieldInfo? s_zipperOutputQuantity = GetField<Zipper>("<QuantityInOutputBuffer>k__BackingField");
    private static readonly FieldInfo? s_liftPendingInput = GetField<Lift>("m_pendingInput");
    private static readonly FieldInfo? s_liftOutput = GetField<Lift>("m_outputBuffer");
    private static readonly FieldInfo? s_liftOutputQuantity = GetField<Lift>("<QuantityInOutputBuffer>k__BackingField");
    private static readonly FieldInfo? s_miniZipperInput = GetField<MiniZipper>("m_inputBuffer");
    private static readonly FieldInfo? s_miniZipperOutput = GetField<MiniZipper>("m_outputBuffer");
    private static readonly FieldInfo? s_miniZipperInputQuantity = GetField<MiniZipper>("m_quantityInInputBuffer");
    private static readonly FieldInfo? s_miniZipperOutputQuantity = GetField<MiniZipper>("m_quantityInOutputBuffer");
#if COI_UPDATE_4_3
    // Optional: b622 has only the input slots; b624 adds a shared reserve.
    private static readonly FieldInfo? s_miniZipperReserve = GetField<MiniZipper>("m_reservePile");
    private static readonly FieldInfo? s_miniZipperReservePort = GetField<MiniZipper>("m_reservePortIndex");
    private static readonly FieldInfo? s_miniZipperReserveStep = GetField<MiniZipper>("m_reserveStep");
    private static readonly FieldInfo? s_miniZipperInputSteps = GetField<MiniZipper>("m_inputBufferSteps");
#endif
    private static readonly FieldInfo? s_sorterOutput = GetField<Sorter>("m_outputBuffer");
    private static readonly FieldInfo? s_sorterOutputQuantity = GetField<Sorter>("<QuantityInOutputBuffer>k__BackingField");

    private readonly FieldInfo? m_inputArrayField;
    private readonly FieldInfo? m_pendingInputField;
    private readonly FieldInfo? m_outputQueueField;
    private readonly Queueue<ZipBuffProduct> m_emptyOutput = new();
    private readonly FieldInfo? m_inputQuantityField;
    private readonly FieldInfo? m_outputQuantityField;

    public IEntity Entity { get; }
    public IStaticEntity StaticEntity { get; }
    public EntityId Id => Entity.Id;
    public string AdapterName { get; }
    public string AdapterKind => AdapterName;
    public string InspectorGroupId => AdapterName;
    public bool IsValid => !IsDestroyed && StaticEntity != null;
    public bool SupportsRegularRemoval => true;
    public bool SupportsQuickRemoval => true;
    public bool SupportsInspectorControl => true;
    public bool IsProductSourceOrSink => false;
    public bool IsDestroyed => Entity.IsDestroyed;
    public ConstructionState ConstructionState => StaticEntity.ConstructionState;
    public Quantity MaxBufferSize => GetMaxBufferSize();

    private TransportProductRemovalAdapter(
        IEntity entity,
        IStaticEntity staticEntity,
        string adapterName,
        FieldInfo? inputArrayField,
        FieldInfo? pendingInputField,
        FieldInfo? outputQueueField,
        FieldInfo? inputQuantityField,
        FieldInfo? outputQuantityField)
    {
        Entity = entity;
        StaticEntity = staticEntity;
        AdapterName = adapterName;
        m_inputArrayField = inputArrayField;
        m_pendingInputField = pendingInputField;
        m_outputQueueField = outputQueueField;
        m_inputQuantityField = inputQuantityField;
        m_outputQuantityField = outputQuantityField;
    }

    public static bool TryCreateBuiltIn(IEntity? entity, out TransportProductRemovalAdapter adapter)
    {
        adapter = null!;
        if (entity is not IStaticEntity staticEntity)
            return false;

        string name;
        FieldInfo? inputArray = null;
        FieldInfo? pendingInput = null;
        FieldInfo? inputQuantity = null;
        FieldInfo? outputQueue;
        FieldInfo? outputQuantity;
        switch (entity)
        {
            case Zipper:
                name = nameof(Zipper);
                inputArray = s_zipperInput;
                inputQuantity = s_zipperInputQuantity;
                outputQueue = s_zipperOutput;
                outputQuantity = s_zipperOutputQuantity;
                break;
            case Lift:
                name = nameof(Lift);
                pendingInput = s_liftPendingInput;
                outputQueue = s_liftOutput;
                outputQuantity = s_liftOutputQuantity;
                break;
            case MiniZipper:
                name = nameof(MiniZipper);
                inputArray = s_miniZipperInput;
                inputQuantity = s_miniZipperInputQuantity;
                outputQueue = s_miniZipperOutput;
                outputQuantity = s_miniZipperOutputQuantity;
                break;
            case Sorter:
                name = nameof(Sorter);
                outputQueue = s_sorterOutput;
                outputQuantity = s_sorterOutputQuantity;
                break;
            default:
                return false;
        }

        bool requiresOutputBuffer = true;
#if COI_UPDATE_4_3
        // Mini-zippers now stage input piles and forward directly to ports.
        requiresOutputBuffer = entity is not MiniZipper;
        if (entity is MiniZipper && s_miniZipperReserve != null &&
            (s_miniZipperReservePort == null || s_miniZipperReserveStep == null || s_miniZipperInputSteps == null))
            return false;
#endif
        if ((requiresOutputBuffer && (outputQueue == null || outputQuantity == null)) ||
            (entity is Zipper && (inputArray == null || inputQuantity == null)) ||
            (entity is Lift && pendingInput == null) ||
            (entity is MiniZipper && (inputArray == null || inputQuantity == null)))
        {
            return false;
        }

        adapter = new TransportProductRemovalAdapter(
            entity,
            staticEntity,
            name,
            inputArray,
            pendingInput,
            outputQueue,
            inputQuantity,
            outputQuantity);
        return true;
    }

    public void AddBufferedProducts(Dict<ProductProto, Quantity> products)
    {
        products.Clear();
        ForEachBufferedProduct(productQuantity => AddProduct(products, productQuantity));
    }

    public void GetBufferedProducts(IList<ProductQuantity> products)
    {
        products.Clear();
        ForEachBufferedProduct(products.Add);
    }

    public Quantity GetProductQuantity(ProductProto product)
    {
        Quantity result = Quantity.Zero;
        ForEachBufferedProduct(productQuantity =>
        {
            if (productQuantity.Product == product)
                result += productQuantity.Quantity;
        });
        return result;
    }

    public int GetBufferStateHash()
    {
        if (IsDestroyed)
            return 0;

        int hash = 17;
        hash = hash * 31 + GetTotalQuantity().Value.GetHashCode();
        hash = hash * 31 + GetMaxBufferSize().Value.GetHashCode();
        ForEachBufferedProduct(productQuantity =>
        {
            hash = hash * 31 + productQuantity.Product.Id.Value.GetHashCode();
            hash = hash * 31 + productQuantity.Quantity.Value.GetHashCode();
        });
        return hash;
    }

    public Quantity GetMaxBufferSize()
    {
        return Entity switch
        {
            Zipper zipper => zipper.MaxBufferSize,
            Lift lift => lift.MaxBufferSize,
            MiniZipper miniZipper => miniZipper.MaxBufferSize,
            Sorter sorter => sorter.MaxBufferSize,
            _ => Quantity.Zero,
        };
    }

    public Upoints GetQuickRemoveCost(out bool canAfford)
    {
        switch (Entity)
        {
            case Lift lift:
                return lift.GetQuickRemoveCost(out canAfford);
            case MiniZipper miniZipper:
                return miniZipper.GetQuickRemoveCost(out canAfford);
            case Sorter sorter:
                return sorter.GetQuickRemoveCost(out canAfford);
            case Zipper zipper:
                Upoints cost = QuickDeliverCostHelper.QuantityToUnityCost(
                    GetTotalQuantity().Value,
                    zipper.Context.UpointsManager.QuickActionCostMultiplier,
                    applyDiscount: true) ?? Upoints.Zero;
                canAfford = zipper.Context.UpointsManager.CanConsume(cost);
                return cost;
            default:
                canAfford = false;
                return Upoints.Zero;
        }
    }

    public void ClearAllProductsForQuickRemove()
    {
        ClearAllProducts();
    }

    public void SetRegularRemovalActive(bool active)
    {
        // Built-in entities are gated by the shared Harmony simulation hooks.
        // The public adapter contract still requires external adapters to own
        // their equivalent idempotent gate.
    }

#if COI_UPDATE_4_3
    public Quantity RemoveProduct(ProductProto product, Quantity maxQuantity, bool reportAsCleared)
    {
        Quantity remaining = maxQuantity;
        Quantity removedInput = Quantity.Zero, removedOutput = Quantity.Zero;
        if (m_pendingInputField?.GetValue(Entity) is MixedPile pending)
        {
            MixedPile kept = RemoveFromPile(pending, product, ref remaining, out Quantity removed);
            m_pendingInputField.SetValue(Entity, kept);
            removedInput += removed;
        }
        if (m_inputArrayField?.GetValue(Entity) is MixedPile[] inputs)
        {
            for (int i = 0; i < inputs.Length && remaining.IsPositive; i++)
            {
                inputs[i] = RemoveFromPile(inputs[i], product, ref remaining, out Quantity removed);
                removedInput += removed;
            }
        }
        if (Entity is MiniZipper && s_miniZipperReserve?.GetValue(Entity) is MixedPile reserve)
        {
            SetMiniZipperReserve(RemoveFromPile(reserve, product, ref remaining, out Quantity removed));
            removedInput += removed;
            PromoteMiniZipperReserveIfInputEmpty();
        }
        Queueue<ZipBuffProduct> outputs = GetOutputBuffer();
        for (int i = 0; i < outputs.Count && remaining.IsPositive; i++)
        {
            ZipBuffProduct current = outputs[i];
            MixedPile kept = RemoveFromPile(current.Pile, product, ref remaining, out Quantity removed);
            if (removed.IsNotPositive) continue;
            if (kept.IsEmpty) outputs.RemoveAt(i--);
            else outputs[i] = new ZipBuffProduct(kept, current.EnqueuedAtStep);
            removedOutput += removed;
        }
        DecreaseCachedQuantity(m_inputQuantityField, removedInput);
        DecreaseCachedQuantity(m_outputQuantityField, removedOutput);
        Quantity total = removedInput + removedOutput;
        if (reportAsCleared && total.IsPositive)
            GetContext().ProductsManager.ProductClearedNoChecks(product, total);
        return total;
    }

    private void SetMiniZipperReserve(MixedPile pile)
    {
        if (Entity is not MiniZipper || s_miniZipperReserve == null) return;
        s_miniZipperReserve.SetValue(Entity, pile);
        if (pile.IsEmpty)
        {
            s_miniZipperReservePort!.SetValue(Entity, -1);
            s_miniZipperReserveStep!.SetValue(Entity, default(SimStep));
        }
    }

    private void PromoteMiniZipperReserveIfInputEmpty()
    {
        if (s_miniZipperReserve!.GetValue(Entity) is not MixedPile reserve || reserve.IsEmpty) return;
        int port = (int)s_miniZipperReservePort!.GetValue(Entity);
        var inputs = (MixedPile[])m_inputArrayField!.GetValue(Entity);
        if (port < 0 || port >= inputs.Length || inputs[port].IsNotEmpty) return;
        // Native SimUpdate promotes the reserve only after sending a nonempty
        // input slot. Removal can empty it first, leaving the reserve stranded.
        inputs[port] = reserve;
        ((SimStep[])s_miniZipperInputSteps!.GetValue(Entity))[port] = (SimStep)s_miniZipperReserveStep!.GetValue(Entity);
        SetMiniZipperReserve(MixedPile.None);
    }

    private static MixedPile RemoveFromPile(MixedPile pile, ProductProto product,
        ref Quantity remaining, out Quantity removed)
    {
        removed = Quantity.Zero;
        if (pile.IsEmpty || remaining.IsNotPositive) return pile;
        if (!pile.HasMembers)
        {
            if (pile.Product != product) return pile;
            removed = pile.Quantity.Min(remaining);
            remaining -= removed;
            return MixedPile.FromPlain(pile.Product, pile.Quantity - removed);
        }
        MixedPile kept = MixedPile.None;
        var category = pile.Product.MixedCategory.Value;
        for (int i = 0; i < pile.Quantity.Value; i++)
        {
            byte slot = pile.GetSlot(i);
            if (remaining.IsPositive && category.TryGetMemberByIndex(slot).ValueOrNull == product)
            {
                remaining -= 1.Quantity();
                removed += 1.Quantity();
            }
            else
            {
                MixedPile unit = MixedPile.CreateHomogeneous(pile.Product, slot, 1);
                if (kept.IsEmpty) kept = unit;
                else MixedPile.Stack(ref kept, ref unit);
            }
        }
        return kept;
    }


#else
    public Quantity RemoveProduct(ProductProto product, Quantity maxQuantity, bool reportAsCleared)
    {
        Quantity remaining = maxQuantity;
        Quantity removed = Quantity.Zero;
        Quantity removedFromInput = Quantity.Zero;
        Quantity removedFromOutput = Quantity.Zero;

        if (m_pendingInputField?.GetValue(Entity) is ProductQuantity pending &&
            pending.IsNotEmpty && pending.Product == product && remaining.IsPositive)
        {
            Quantity amount = pending.Quantity.Min(remaining);
            Quantity left = pending.Quantity - amount;
            m_pendingInputField.SetValue(Entity, left.IsPositive ? pending.WithNewQuantity(left) : ProductQuantity.None);
            removed += amount;
            remaining -= amount;
        }

        if (m_inputArrayField?.GetValue(Entity) is ProductQuantity[] inputBuffer)
        {
            for (int i = 0; i < inputBuffer.Length && remaining.IsPositive; i++)
            {
                ProductQuantity current = inputBuffer[i];
                if (current.IsEmpty || current.Product != product)
                    continue;

                Quantity amount = current.Quantity.Min(remaining);
                Quantity left = current.Quantity - amount;
                inputBuffer[i] = left.IsPositive ? current.WithNewQuantity(left) : ProductQuantity.None;
                removed += amount;
                removedFromInput += amount;
                remaining -= amount;
            }
        }

        Queueue<ZipBuffProduct> outputBuffer = GetOutputBuffer();
        for (int i = 0; i < outputBuffer.Count && remaining.IsPositive; i++)
        {
            ZipBuffProduct current = outputBuffer[i];
            ProductQuantity currentProduct = current.ProductQuantity;
            if (currentProduct.IsEmpty || currentProduct.Product != product)
                continue;

            Quantity amount = currentProduct.Quantity.Min(remaining);
            Quantity left = currentProduct.Quantity - amount;
            if (left.IsPositive)
                outputBuffer[i] = new ZipBuffProduct(currentProduct.WithNewQuantity(left), current.EnqueuedAtStep);
            else
            {
                outputBuffer.RemoveAt(i);
                i--;
            }
            removed += amount;
            removedFromOutput += amount;
            remaining -= amount;
        }

        DecreaseCachedQuantity(m_inputQuantityField, removedFromInput);
        DecreaseCachedQuantity(m_outputQuantityField, removedFromOutput);
        if (reportAsCleared && removed.IsPositive)
            GetContext().ProductsManager.ProductClearedNoChecks(product, removed);
        return removed;
    }


#endif
    public Quantity GetTotalQuantity()
    {
        Quantity total = Quantity.Zero;
        ForEachBufferedProduct(productQuantity => total += productQuantity.Quantity);
        return total;
    }

#if COI_UPDATE_4_3
    private void ForEachBufferedProduct(Action<ProductQuantity> action)
    {
        var products = new Lyst<ProductQuantity>(8);
        if (m_pendingInputField?.GetValue(Entity) is MixedPile pending) AddPileProducts(pending, products);
        if (m_inputArrayField?.GetValue(Entity) is MixedPile[] inputs)
            foreach (MixedPile pile in inputs) AddPileProducts(pile, products);
        if (Entity is MiniZipper && s_miniZipperReserve?.GetValue(Entity) is MixedPile reserve)
            AddPileProducts(reserve, products);
        foreach (ZipBuffProduct output in GetOutputBuffer()) AddPileProducts(output.Pile, products);
        foreach (ProductQuantity product in products) action(product);
    }

    private static void AddPileProducts(MixedPile pile, Lyst<ProductQuantity> products)
    {
        // AggregateInto expands mixed members only; plain piles need their own row.
        if (pile.HasMembers) pile.AggregateInto(products);
        else if (pile.IsNotEmpty) products.AddQuantity(pile.Product, pile.Quantity);
    }


#else
    private void ForEachBufferedProduct(Action<ProductQuantity> action)
    {
        if (m_pendingInputField?.GetValue(Entity) is ProductQuantity pending && pending.IsNotEmpty)
            action(pending);
        if (m_inputArrayField?.GetValue(Entity) is ProductQuantity[] inputBuffer)
        {
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                if (inputBuffer[i].IsNotEmpty)
                    action(inputBuffer[i]);
            }
        }
        Queueue<ZipBuffProduct>.Enumerator enumerator = GetOutputBuffer().GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.ProductQuantity.IsNotEmpty)
                action(enumerator.Current.ProductQuantity);
        }
    }


#endif
    private Queueue<ZipBuffProduct> GetOutputBuffer()
    {
        return m_outputQueueField?.GetValue(Entity) as Queueue<ZipBuffProduct> ?? m_emptyOutput;
    }

    private void DecreaseCachedQuantity(FieldInfo? quantityField, Quantity removed)
    {
        if (quantityField == null || !removed.IsPositive)
            return;
        Quantity current = (Quantity)quantityField.GetValue(Entity);
        quantityField.SetValue(Entity, current - removed);
    }

    private EntityContext GetContext()
    {
        return Entity switch
        {
            Zipper zipper => zipper.Context,
            Lift lift => lift.Context,
            MiniZipper miniZipper => miniZipper.Context,
            Sorter sorter => sorter.Context,
            _ => throw new InvalidOperationException("Unsupported product-removal entity."),
        };
    }

    private void ClearAllProducts()
    {
        ForEachBufferedProduct(GetContext().AssetTransactionManager.StoreClearedProduct);
        if (m_inputArrayField?.GetValue(Entity) is ProductQuantity[] inputBuffer)
        {
            for (int i = 0; i < inputBuffer.Length; i++)
                inputBuffer[i] = ProductQuantity.None;
        }
#if COI_UPDATE_4_3
        if (m_pendingInputField != null) m_pendingInputField.SetValue(Entity, MixedPile.None);
        if (m_inputArrayField?.GetValue(Entity) is MixedPile[] mixedInput)
            for (int i = 0; i < mixedInput.Length; i++) mixedInput[i] = MixedPile.None;
        SetMiniZipperReserve(MixedPile.None);
#endif
        GetOutputBuffer().Clear();
        m_inputQuantityField?.SetValue(Entity, Quantity.Zero);
        m_outputQuantityField?.SetValue(Entity, Quantity.Zero);
    }

    private static void AddProduct(Dict<ProductProto, Quantity> products, ProductQuantity productQuantity)
    {
        if (products.TryGetValue(productQuantity.Product, out Quantity existing))
            products[productQuantity.Product] = existing + productQuantity.Quantity;
        else
            products.Add(productQuantity.Product, productQuantity.Quantity);
    }

    private static FieldInfo? GetField<T>(string name)
    {
        return typeof(T).GetField(name, INSTANCE_FIELD);
    }
}
