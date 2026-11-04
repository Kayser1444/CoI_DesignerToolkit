using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Environment;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

public static partial class Program
{
    private static void RadiationContracts(string mod)
    {
        Type type = Assembly.LoadFrom(mod).GetType("CoIDesignerToolkit.RadiationManager", true)!;
        object sampler = RuntimeHelpers.GetUninitializedObject(type);
        MethodInfo sample = type.GetMethod("GetLocalRadiation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var proto = (MachineProto)RuntimeHelpers.GetUninitializedObject(typeof(MachineProto));
        Set(proto, "<Tags>k__BackingField", HybridSet<Tag>.Empty);
        var product = Product("RadioactiveFixture");
        Set(product, "Radioactivity", 2);
        var holder = DispatchProxy.Create<IRadiationHolderFixture, RadiationHolderProxy>();
        var proxy = (RadiationHolderProxy)(object)holder;
        proxy.Proto = proto;
        proxy.Product = product;
        float Read() => (float)sample.Invoke(sampler, new object[] { holder })!;
        Check(Read() == 10f, "unshielded radioactive inventory remains visible");
        Tag? tag = typeof(CoreProtoTags).GetField("RadiationShielded")?.GetValue(null) as Tag;
        if (tag == null)
        {
            Console.WriteLine("PASS: radiation sampler runs without the optional b626 tag on older 4.3 builds.");
            return;
        }
        Set(proto, "<Tags>k__BackingField", HybridSet<Tag>.Empty.Add(new[] { tag }));
        MethodInfo native = typeof(Mafi.Core.Environment.RadiationManager)
            .GetMethod("isShieldedHolder", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)native.Invoke(null, new object[] { holder })!, "fixture is protected by native b626 shielding");
        Check(Read() == 0f, "BDT must not count native tag-protected radioactive inventory");
        var dynamicHolder = DispatchProxy.Create<IDynamicRadiationHolderFixture, RadiationHolderProxy>();
        var dynamicProxy = (RadiationHolderProxy)(object)dynamicHolder;
        dynamicProxy.Proto = proto;
        dynamicProxy.Product = product;
        dynamicProxy.HasShielding = true;
        Check((float)sample.Invoke(sampler, new object[] { dynamicHolder })! == 10f,
            "prototype tags must not override a dynamic holder's own shielding contract");
        dynamicProxy.HasShielding = false;
        Check((float)sample.Invoke(sampler, new object[] { dynamicHolder })! == 0f,
            "tag protection applies when the holder has no dynamic shielding");
        // A different prototype must not inherit protection from the preceding sample.
        proxy.Proto = (MachineProto)RuntimeHelpers.GetUninitializedObject(typeof(MachineProto));
        Set(proxy.Proto, "<Tags>k__BackingField", HybridSet<Tag>.Empty);
        Check(Read() == 10f, "shielding remains prototype-specific");
        Console.WriteLine("PASS: b626 radiation shielding agrees with native holder protection; unprotected inventory stays visible.");
    }

    public interface IRadiationHolderFixture : IEntityWithStoredProductForUi, IEntityWithProducts { }
    public interface IDynamicRadiationHolderFixture : IRadiationHolderFixture, IShieldedEntity { }
    public class RadiationHolderProxy : DispatchProxy
    {
        public EntityProto Proto = null!;
        public ProductProto Product = null!;
        public bool HasShielding;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Prototype" => Proto,
            "get_StoredProduct" => (Option<ProductProto>)Product,
            "get_CurrentQuantity" => 5.Quantity(),
            "get_HasShielding" => HasShielding,
            _ => method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null
        };
    }
}
