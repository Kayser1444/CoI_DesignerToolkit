using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Storages.NuclearWaste;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.NuclearReactors;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Products;
using Mafi.Core.Simulation;
using Mafi.Core.Vehicles.Trucks;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Samples the unsafe radioactive product inventory at the same daily cadence used by vanilla's
/// radiation manager. The value is deliberately inventory-based: transport events do not create
/// additional radiation in vanilla, while product held outside the two safe-storage implementations does.
/// </summary>
internal sealed class RadiationManager : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.RadiationManager");
    private static RadiationManager? s_instance;

    private readonly Dict<int, EntityRadiationState> m_states = new Dict<int, EntityRadiationState>();
    private IEntitiesManager? m_entitiesManager;
    private IProductsManager? m_productsManager;
    private Calendar? m_calendar;

    public static RadiationManager? Instance => s_instance;

    public RadiationManager()
    {
        s_instance = this;
    }

    public void Initialize(DependencyResolver resolver)
    {
        m_entitiesManager = resolver.Resolve<IEntitiesManager>();
        m_productsManager = resolver.Resolve<IProductsManager>();
        m_calendar = resolver.Resolve<Calendar>();
        m_calendar.NewDay.AddNonSaveable(this, OnNewDay);
        m_entitiesManager.EntityRemoved.AddNonSaveable(this, OnEntityRemoved);
        s_log.Info("RadiationManager initialized.");
    }

    public void Dispose()
    {
        if (m_calendar != null)
        {
            try { m_calendar.NewDay.RemoveNonSaveable(this, OnNewDay); } catch { }
        }

        if (m_entitiesManager != null)
        {
            try { m_entitiesManager.EntityRemoved.RemoveNonSaveable(this, OnEntityRemoved); } catch { }
        }

        m_states.Clear();
        m_calendar = null;
        m_entitiesManager = null;
        m_productsManager = null;
        if (ReferenceEquals(s_instance, this))
            s_instance = null;
    }

    public Dict<int, EntityRadiationState> GetAllStates()
    {
        return m_states;
    }

    public void OnNewDay()
    {
        int daysToAverage = DesignerToolkitSettings.RadiationDaysToAverage;
        if (daysToAverage <= 0 || m_entitiesManager == null)
        {
            m_states.Clear();
            return;
        }

        // Every state receives a sample, including entities that were empty today. This makes the
        // history represent inventory over time rather than only the days on which a source existed.
        foreach (var state in m_states.Values)
            state.CurrentDayRadiation = 0f;

        foreach (IEntity entity in m_entitiesManager.Entities)
        {
            if (entity.IsDestroyed)
                continue;

            float radiation = GetLocalRadiation(entity);
            if (radiation <= 0f)
                continue;

            EntityRadiationState state = GetOrCreateState(entity.Id.Value);
            state.CurrentDayRadiation = radiation;
        }

        foreach (var state in m_states.Values)
        {
            state.DailyHistory[state.HistoryHead] = state.CurrentDayRadiation;
            state.HistoryHead = (state.HistoryHead + 1) % EntityRadiationState.HISTORY_LENGTH;
            if (state.HistoryCount < EntityRadiationState.HISTORY_LENGTH)
                state.HistoryCount++;
            state.CurrentDayRadiation = 0f;
            state.RecalculateAverage(daysToAverage);
        }
    }

    private EntityRadiationState GetOrCreateState(int entityId)
    {
        if (!m_states.TryGetValue(entityId, out EntityRadiationState state))
        {
            state = new EntityRadiationState();
            m_states[entityId] = state;
        }
        return state;
    }

    private float GetLocalRadiation(IEntity entity)
    {
        // Vanilla explicitly reports these buffers as safely stored to RadiationManager. Do not
        // show them in the overlay, or the overlay would disagree with the game's penalty source.
        if (entity is NuclearReactor || entity is NuclearWasteStorage)
            return 0f;

        float total = 0f;
        var seenBuffers = new HashSet<IProductBufferReadOnly>();

        if (entity is IEntityWithStoredProductForUi storedEntity && storedEntity.StoredProduct.HasValue)
        {
            AddProduct(storedEntity.StoredProduct.Value, storedEntity.CurrentQuantity, ref total);
        }

        if (entity is IEntityWithInputBuffersForUi inputEntity)
            AddBuffers(inputEntity.InputBuffers, seenBuffers, ref total);
        if (entity is IEntityWithOutputBuffersForUi outputEntity)
            AddBuffers(outputEntity.OutputBuffers, seenBuffers, ref total);
        if (entity is IEntityWithStorageBuffersForUi storageEntity)
            AddBuffers(storageEntity.StorageBuffers, seenBuffers, ref total);

        // Machine does not expose its ordinary recipe buffers through the UI interfaces, but its
        // publicized fields are available to mods through the vanilla reference in the project file.
        if (entity is Machine machine)
        {
            var inputEnumerator = machine.m_inputBuffers.GetEnumerator();
            while (inputEnumerator.MoveNext())
                AddBuffer(inputEnumerator.Current, seenBuffers, ref total);

            var outputEnumerator = machine.m_outputBuffers.GetEnumerator();
            while (outputEnumerator.MoveNext())
                AddBuffer(outputEnumerator.Current, seenBuffers, ref total);
        }

        if (entity is Zipper zipper)
        {
            for (int i = 0; i < zipper.m_inputBuffer.Length; i++)
                AddProduct(zipper.m_inputBuffer[i], ref total);

            var outputEnumerator = zipper.m_outputBuffer.GetEnumerator();
            while (outputEnumerator.MoveNext())
                AddProduct(outputEnumerator.Current.ProductQuantity, ref total);
        }

        if (entity is Transport transport)
            AddFlatTransportProducts(transport, ref total);
        else if (entity is Truck truck)
            AddTruckCargo(truck, ref total);
        else if (entity is MiniZipper miniZipper)
            AddTransportProducts(miniZipper, ref total);
        else if (entity is Lift lift)
            AddTransportProducts(lift, ref total);
        else if (entity is Grate grate)
            AddTransportProducts(grate, ref total);

        return total;
    }

    private static void AddBuffers(IEnumerable<IProductBufferReadOnly> buffers, HashSet<IProductBufferReadOnly> seenBuffers, ref float total)
    {
        foreach (IProductBufferReadOnly buffer in buffers)
            AddBuffer(buffer, seenBuffers, ref total);
    }

    private static void AddBuffer(IProductBufferReadOnly buffer, HashSet<IProductBufferReadOnly> seenBuffers, ref float total)
    {
        if (buffer == null || !seenBuffers.Add(buffer))
            return;

        AddProduct(buffer.Product, buffer.Quantity, ref total);
    }

    private static void AddTransportProducts(MiniZipper zipper, ref float total)
    {
        var products = new Lyst<ProductQuantity>(8);
        zipper.GetAllBufferedProducts(products);
        AddProducts(products, ref total);
    }

    private static void AddTransportProducts(Lift lift, ref float total)
    {
        var products = new Lyst<ProductQuantity>(8);
        lift.GetAllBufferedProducts(products);
        AddProducts(products, ref total);
    }

    private static void AddTransportProducts(Grate grate, ref float total)
    {
        var products = new Lyst<ProductQuantity>(8);
        grate.GetAllBufferedProducts(products);
        AddProducts(products, ref total);
    }

    private void AddFlatTransportProducts(Transport transport, ref float total)
    {
        if (m_productsManager == null)
            return;

        var enumerator = transport.TransportedProducts.GetEnumerator();
        while (enumerator.MoveNext())
        {
            TransportedProductMutable transportedProduct = enumerator.Current;
            ProductProto product = m_productsManager.SlimIdManager.ResolveOrPhantom(transportedProduct.SlimId);
            AddProduct(product, transportedProduct.Quantity, ref total);
        }
    }

    private static void AddTruckCargo(Truck truck, ref float total)
    {
        var products = new Lyst<ProductQuantity>(8);
        truck.Cargo.GetCargoProducts(products);
        AddProducts(products, ref total);
    }

    private static void AddProducts(Lyst<ProductQuantity> products, ref float total)
    {
        var enumerator = products.GetEnumerator();
        while (enumerator.MoveNext())
            AddProduct(enumerator.Current, ref total);
    }

    private static void AddProduct(ProductQuantity productQuantity, ref float total)
    {
        AddProduct(productQuantity.Product, productQuantity.Quantity, ref total);
    }

    private static void AddProduct(ProductProto product, Quantity quantity, ref float total)
    {
        if (product == null || product.Radioactivity == 0 || quantity.IsZero)
            return;

        total += quantity.Value * product.Radioactivity;
    }

    private void OnEntityRemoved(IEntity entity)
    {
        m_states.Remove(entity.Id.Value);
    }
}

internal sealed class EntityRadiationState
{
    public const int HISTORY_LENGTH = 360;

    public readonly float[] DailyHistory = new float[HISTORY_LENGTH];
    public int HistoryCount;
    public int HistoryHead;
    public float CurrentDayRadiation;
    public float CachedAverageRadiation;

    public void RecalculateAverage(int daysToAverage)
    {
        if (daysToAverage <= 0 || HistoryCount == 0)
        {
            CachedAverageRadiation = 0f;
            return;
        }

        int take = Math.Min(daysToAverage, HistoryCount);
        float sum = 0f;
        int index = HistoryHead;
        for (int i = 0; i < take; i++)
        {
            index = (index - 1 + HISTORY_LENGTH) % HISTORY_LENGTH;
            sum += DailyHistory[index];
        }

        CachedAverageRadiation = sum / take;
    }
}
