using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Economy;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Ports.Io;
using Mafi.Core.Products;

public static partial class Program
{
    private static void MiniZipperReserveContracts(string mod, MixedPile pile, ProductProto a, ProductProto b)
    {
        FieldInfo? reserveField = typeof(MiniZipper).GetField("m_reservePile", BindingFlags.Instance | BindingFlags.NonPublic);
        if (reserveField == null)
        {
            Console.WriteLine("SKIP: this game has no mini-zipper reserve; input-only contracts ran above.");
            return;
        }
        Type adapterType = Assembly.LoadFrom(mod).GetType("CoIDesignerToolkit.TransportProductRemovalAdapter", true)!;
        var reserveStep = new SimStep(17);

        (MiniZipper Entity, object Adapter, StoreProxy Store) Fixture(MixedPile input, MixedPile reserve)
        {
            var entity = (MiniZipper)RuntimeHelpers.GetUninitializedObject(typeof(MiniZipper));
            var context = (EntityContext)RuntimeHelpers.GetUninitializedObject(typeof(EntityContext));
            var store = DispatchProxy.Create<IAssetTransactionManager, StoreProxy>();
            Set(context, "AssetTransactionManager", store);
            Set(entity, "<Context>k__BackingField", context);
            Set(entity, "m_inputBuffer", new[] { input });
            Set(entity, "m_inputBufferSteps", new[] { new SimStep(9) });
            Set(entity, "m_reservePile", reserve);
            Set(entity, "m_reservePortIndex", reserve.IsEmpty ? -1 : 0);
            Set(entity, "m_reserveStep", reserveStep);
            Set(entity, "m_quantityInInputBuffer", input.Quantity + reserve.Quantity);
            Set(entity, "m_connectedInputPortsCache", ImmutableArray<IoPortData>.Empty);
            object?[] args = { entity, null };
            Check((bool)adapterType.GetMethod("TryCreateBuiltIn")!.Invoke(null, args)!, "reserve adapter resolves");
            return (entity, args[1]!, (StoreProxy)(object)store);
        }
        Quantity Total(object adapter) => (Quantity)adapterType.GetMethod("GetTotalQuantity")!.Invoke(adapter, null)!;
        Quantity Remove(object adapter, ProductProto product, int limit) => (Quantity)adapterType.GetMethod("RemoveProduct")!
            .Invoke(adapter, new object[] { product, limit.Quantity(), false })!;
        MixedPile Reserve(MiniZipper entity) => (MixedPile)reserveField.GetValue(entity)!;
        int Port(MiniZipper entity) => (int)typeof(MiniZipper).GetField("m_reservePortIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(entity)!;
        MixedPile Input(MiniZipper entity) => ((MixedPile[])typeof(MiniZipper).GetField("m_inputBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(entity)!)[0];
        void CheckAccounting(MiniZipper entity, object adapter, int expected) =>
            Check(Total(adapter) == expected.Quantity() && entity.TotalQuantityInBuffers == expected.Quantity(),
                $"reserve and cached totals agree: expected={expected}, adapter={Total(adapter)}, cached={entity.TotalQuantityInBuffers}, input={Input(entity).Quantity}, reserve={Reserve(entity).Quantity}");

        var fixture = Fixture(pile, pile);
        CheckAccounting(fixture.Entity, fixture.Adapter, 6);
        var products = new Dict<ProductProto, Quantity>();
        adapterType.GetMethod("AddBufferedProducts")!.Invoke(fixture.Adapter, new object[] { products });
        Check(products[a] == 4.Quantity() && products[b] == 2.Quantity(), "content display includes reserve members");
        var list = new List<ProductQuantity>();
        adapterType.GetMethod("GetBufferedProducts")!.Invoke(fixture.Adapter, new object[] { list });
        Check(list.Sum(p => p.Quantity.Value) == 6, "order snapshots include reserve");
        Check((Quantity)adapterType.GetMethod("GetProductQuantity")!.Invoke(fixture.Adapter, new object[] { a })! == 4.Quantity(), "member query includes reserve");
        int hash = (int)adapterType.GetMethod("GetBufferStateHash")!.Invoke(fixture.Adapter, null)!;
        Set(fixture.Entity, "m_reservePile", MixedPile.FromPlain(b, 3.Quantity()));
        Check(hash != (int)adapterType.GetMethod("GetBufferStateHash")!.Invoke(fixture.Adapter, null)!, "reserve changes invalidate displayed inventory");

        fixture = Fixture(pile, pile);
        Check(Remove(fixture.Adapter, a, 3) == 3.Quantity(), "selective removal crosses into reserve and respects limit");
        CheckAccounting(fixture.Entity, fixture.Adapter, 3);
        Check(Reserve(fixture.Entity).Quantity == 2.Quantity() && Reserve(fixture.Entity).GetSlot(0) == 2 && Reserve(fixture.Entity).GetSlot(1) == 1,
            "reserve retains other members in unit order");
        Check(Remove(fixture.Adapter, a, 10) == Quantity.One, "remaining requested reserve member is removed once");
        CheckAccounting(fixture.Entity, fixture.Adapter, 2);
        adapterType.GetMethod("ClearAllProductsForQuickRemove")!.Invoke(fixture.Adapter, null);
        CheckAccounting(fixture.Entity, fixture.Adapter, 0);
        Check(Reserve(fixture.Entity).IsEmpty && Port(fixture.Entity) == -1 && fixture.Store.Stored == 2.Quantity(), "quick removal returns all retained units and resets reserve");
        adapterType.GetMethod("ClearAllProductsForQuickRemove")!.Invoke(fixture.Adapter, null);
        Check(fixture.Store.Stored == 2.Quantity(), "repeated quick removal cannot return products twice");

        fixture = Fixture(MixedPile.FromPlain(a, Quantity.One), MixedPile.FromPlain(b, 2.Quantity()));
        Check(Remove(fixture.Adapter, a, 1) == Quantity.One, "remove the complete input while keeping reserve");
        CheckAccounting(fixture.Entity, fixture.Adapter, 2);
        Check(Input(fixture.Entity).Product == b && Input(fixture.Entity).Quantity == 2.Quantity() && Reserve(fixture.Entity).IsEmpty && Port(fixture.Entity) == -1,
            "remaining reserve moves to its empty input so native simulation can send it");
        var steps = (SimStep[])typeof(MiniZipper).GetField("m_inputBufferSteps", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Entity)!;
        Check(steps[0] == reserveStep, "promotion preserves reserve arrival step");

        fixture = Fixture(MixedPile.FromPlain(b, Quantity.One), MixedPile.FromPlain(a, 2.Quantity()));
        Check(Remove(fixture.Adapter, a, 0) == Quantity.Zero && Remove(fixture.Adapter, Product("Absent"), 10) == Quantity.Zero, "zero and unrelated removal leave reserve intact");
        CheckAccounting(fixture.Entity, fixture.Adapter, 3);
        Check(Remove(fixture.Adapter, a, 10) == 2.Quantity() && Reserve(fixture.Entity).IsEmpty && Port(fixture.Entity) == -1, "empty reserve releases its port");
        CheckAccounting(fixture.Entity, fixture.Adapter, 1);
        adapterType.GetMethod("ClearAllProductsForQuickRemove")!.Invoke(fixture.Adapter, null);
        Check(fixture.Store.Stored == Quantity.One, "quick removal returns retained plain products too");

        fixture = Fixture(pile, pile);
        adapterType.GetMethod("ClearAllProductsForQuickRemove")!.Invoke(fixture.Adapter, null);
        CheckAccounting(fixture.Entity, fixture.Adapter, 0);
        Check(Reserve(fixture.Entity).IsEmpty && fixture.Store.Stored == 6.Quantity(), "quick removal directly returns input and reserve exactly once");
        Console.WriteLine("PASS: mini-zipper reserve display, snapshots, selective/quick removal, cached totals, unit order, promotion and idempotence.");
    }
}
