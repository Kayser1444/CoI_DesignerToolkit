using System.Reflection;
using System.Runtime.CompilerServices;
using CoI.AutoHelpers.Compatibility;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Economy;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Sorters;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

public static class Program
{
    private static int Main(string[] args)
    {
        string managed = args[0], mod = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            foreach (string directory in new[] { managed, Path.GetDirectoryName(mod)! })
            {
                string path = Path.Combine(directory, new AssemblyName(request.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(mod); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string mod)
    {
        ProductProto a = Product("A"), b = Product("B"), placeholder = Product("Mixed");
        var category = (MixedMaterialCategory)RuntimeHelpers.GetUninitializedObject(typeof(MixedMaterialCategory));
        var members = new[] { Member(category, a), Member(category, b) };
        Set(category, "m_mixedByIndex", members);
        Set(placeholder, "<MixedCategory>k__BackingField", (Option<MixedMaterialCategory>)category);
        MixedPile pile = MixedPile.CreateHomogeneous(placeholder, 1, 1);
        MixedPile chunk = MixedPile.CreateHomogeneous(placeholder, 2, 1);
        MixedPile.Stack(ref pile, ref chunk);
        chunk = MixedPile.CreateHomogeneous(placeholder, 1, 1);
        MixedPile.Stack(ref pile, ref chunk);
        MethodInfo remove = Assembly.LoadFrom(mod).GetType("CoIDesignerToolkit.TransportProductRemovalAdapter", true)!
            .GetMethod("RemoveFromPile", BindingFlags.NonPublic | BindingFlags.Static)!;

        MixedPile Remove(MixedPile source, ProductProto product, int maximum, int expectedRemoved)
        {
            object[] parameters = { source, product, maximum.Quantity(), Quantity.Zero };
            var kept = (MixedPile)remove.Invoke(null, parameters)!;
            Check((Quantity)parameters[3] == expectedRemoved.Quantity(), "removed quantity");
            Check((Quantity)parameters[2] == (maximum - expectedRemoved).Quantity(), "remaining removal scope");
            return kept;
        }
        MixedPile result = Remove(pile, a, 1, 1);
        Check(result.Quantity == 2.Quantity() && result.GetSlot(0) == 2 && result.GetSlot(1) == 1,
            "partial removal preserves the other member and unit order");
        result = Remove(pile, a, 10, 2);
        Check(result.Quantity == Quantity.One && result.GetSlot(0) == 2, "removing A leaves B intact");
        result = Remove(pile, b, 1, 1);
        Check(result.Quantity == 2.Quantity() && result.GetSlot(0) == 1 && result.GetSlot(1) == 1,
            "removing the middle unit preserves both A units");
        result = Remove(MixedPile.FromPlain(a, 5.Quantity()), a, 2, 2);
        Check(!result.HasMembers && result.Quantity == 3.Quantity(), "plain products preserve remaining quantity");
        result = Remove(pile, Product("Other"), 10, 0);
        Check(result.Quantity == pile.Quantity, "unrequested product is untouched");
        result = Remove(pile, a, 0, 0);
        Check(result.Quantity == pile.Quantity, "zero removal scope is harmless");

        var source = new Buffer(a, 5, 5);
        var target = new Buffer(a, 0, 2);
        Check(source.TransferTo(target, 4.Quantity(), false) == 2.Quantity(), "transfer observes receiver capacity");
        Check(source.Quantity == 3.Quantity() && target.Quantity == 2.Quantity(), "transfer conserves quantity");
        Check(source.TransferTo(target, Quantity.One, false) == Quantity.Zero && source.Quantity == 3.Quantity(),
            "full receiver does not drain the source");
        Check(source.TransferTo(new Buffer(b, 0, 10), Quantity.One, false) == Quantity.Zero,
            "cross-product transfer is refused");
        AdapterContracts(mod, pile, a);
        Console.WriteLine("PASS: mixed-pile selective removal, scope limits, unit order, native zipper/lift/mini-zipper/sorter quick removal, and transient-buffer quantity conservation.");
    }

    private static void AdapterContracts(string mod, MixedPile pile, ProductProto product)
    {
        Type adapterType = Assembly.LoadFrom(mod).GetType("CoIDesignerToolkit.TransportProductRemovalAdapter", true)!;
        foreach (Type entityType in new[] { typeof(Zipper), typeof(Lift), typeof(MiniZipper), typeof(Sorter) })
        {
            var entity = (Entity)RuntimeHelpers.GetUninitializedObject(entityType);
            var context = (EntityContext)RuntimeHelpers.GetUninitializedObject(typeof(EntityContext));
            var store = DispatchProxy.Create<IAssetTransactionManager, StoreProxy>();
            Set(context, "AssetTransactionManager", store);
            Set(entity, "<Context>k__BackingField", context);
            var output = new Queueue<ZipBuffProduct>();
            bool mini = entityType == typeof(MiniZipper);
            bool sorter = entityType == typeof(Sorter);
            if (!mini)
            {
                output.Enqueue(new ZipBuffProduct(pile, default));
                Set(entity, "m_outputBuffer", output);
                Set(entity, "<QuantityInOutputBuffer>k__BackingField", pile.Quantity);
            }
            MixedPile[]? input = null;
            if (entityType == typeof(Zipper) || mini)
            {
                input = new[] { pile };
                Set(entity, "m_inputBuffer", input);
                Set(entity, mini ? "m_quantityInInputBuffer" : "<QuantityInInputBuffer>k__BackingField", pile.Quantity);
            }
            else if (!sorter) Set(entity, "m_pendingInput", pile);
            object?[] args = { entity, null };
            Check((bool)adapterType.GetMethod("TryCreateBuiltIn")!.Invoke(null, args)!, "native adapter fields resolve");
            object adapter = args[1]!;
            Quantity total = (Quantity)adapterType.GetMethod("GetTotalQuantity")!.Invoke(adapter, null)!;
            Check(total == (sorter || mini ? 3 : 6).Quantity(), "native input and output piles are counted");
            Quantity removed = (Quantity)adapterType.GetMethod("RemoveProduct")!
                .Invoke(adapter, new object[] { product, 3.Quantity(), false })!;
            Check(removed == (sorter || mini ? 2 : 3).Quantity(), "scope removes matching members across input and output");
            total = (Quantity)adapterType.GetMethod("GetTotalQuantity")!.Invoke(adapter, null)!;
            Check(total == (sorter || mini ? 1 : 3).Quantity(), "unrequested members remain after regular removal");
            adapterType.GetMethod("ClearAllProductsForQuickRemove")!.Invoke(adapter, null);
            Check(output.Count == 0 && (input == null || input.All(p => p.IsEmpty)), "quick removal empties native buffers");
            total = (Quantity)adapterType.GetMethod("GetTotalQuantity")!.Invoke(adapter, null)!;
            Check(total == Quantity.Zero, "quick removal leaves no pending input to duplicate");
            Check(((StoreProxy)(object)store).Stored == (sorter || mini ? 1 : 3).Quantity(), "quick removal returns exactly the remaining products");
        }
    }

    public class StoreProxy : DispatchProxy
    {
        public Quantity Stored;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name != "StoreClearedProduct") throw new InvalidOperationException(targetMethod.Name);
            Stored += ((ProductQuantity)args![0]!).Quantity;
            return null;
        }
    }

    private static ProductProto Product(string id)
    {
        var product = (ProductProto)RuntimeHelpers.GetUninitializedObject(typeof(ProductProto));
        Set(product, "<Id>k__BackingField", new Proto.ID(id));
        Set(product, "MaxQuantityPerTransportedProduct", 3.Quantity());
        return product;
    }
    private static MixedMemberProductProto Member(MixedMaterialCategory category, ProductProto product)
    {
        var member = (MixedMemberProductProto)RuntimeHelpers.GetUninitializedObject(typeof(MixedMemberProductProto));
        Set(member, "Category", category); Set(member, "Member", product);
        return member;
    }
    private static void Set(object target, string name, object value)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Buffer(ProductProto product, int quantity, int capacity) : PlainProductBufferAdapter
    {
        private Quantity m_quantity = quantity.Quantity();
        public override ProductProto Product => product;
        public override Quantity Capacity => capacity.Quantity();
        public override Quantity UsableCapacity => Capacity - Quantity;
        public override Quantity Quantity => m_quantity;
        public override Quantity StoreAsMuchAs(Quantity incoming)
        { Quantity accepted = incoming.Min(UsableCapacity); m_quantity += accepted; return incoming - accepted; }
        public override Quantity RemoveAsMuchAs(Quantity maximum)
        { Quantity removed = maximum.Min(m_quantity); m_quantity -= removed; return removed; }
    }
}
