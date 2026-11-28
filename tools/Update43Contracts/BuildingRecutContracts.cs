using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Validators;
using Mafi.Core.Factory.Lifts;

public static partial class Program
{
    private static void BuildingRecutContracts(string mod)
    {
        const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assembly bdt = Assembly.LoadFrom(mod);
        Type ordersType = bdt.GetType("CoIDesignerToolkit.BuildingMoveOrders", throwOnError: true)!;
        Type orderType = ordersType.GetNestedType("Order", BindingFlags.NonPublic)!;
        object CreateOrder() => Activator.CreateInstance(orderType, ImmutableArray<TileSurfaceCopyPasteData>.Empty,
            ImmutableArray<TileSurfaceCopyPasteData>.Empty)!;

        var manager = (EntitiesManager)RuntimeHelpers.GetUninitializedObject(typeof(EntitiesManager));
        var validators = typeof(EntityValidators).GetMethod("CreateEmpty", hidden)!.Invoke(null, null);
        Set(manager, "m_validators", validators!);
        var entity = (Lift)RuntimeHelpers.GetUninitializedObject(typeof(Lift));
        var id = new EntityId(51);
        Set(entity, "m_id", id);
        Set(entity, "m_constructionState", ConstructionState.Constructed);
        var existingEntities = new Set<IEntity>();
        existingEntities.Add(entity);
        Set(manager, "m_entities", existingEntities);

        const string protoId = "Contracts.BuildingRecut";
        var proto = (LiftProto)RuntimeHelpers.GetUninitializedObject(typeof(LiftProto));
        Set(proto, "<Id>k__BackingField", new Mafi.Core.Prototypes.Proto.ID(protoId));
        Set(entity, "m_proto", proto);
        typeof(StaticEntity).GetField("m_proto", hidden)!.SetValue(entity, proto);
        typeof(Mafi.Core.Entities.Static.Layout.LayoutEntityBase).GetField("m_proto", hidden)!.SetValue(entity, proto);
        Set(entity, "<Prototype>k__BackingField", proto);
        Check(((IStaticEntity)entity).Prototype?.Id.Value == protoId, "fixture exposes the native prototype");
        // Use the real cut validator without starting a demolition that would require a full simulation.
        MethodInfo reportUnique = bdt.GetType("CoIDesignerToolkit.BuildingMoveApi", true)!.GetMethod("SetUniqueForMove")!;
        reportUnique.Invoke(null, new object[] { protoId, true });
        try
        {
            object orders = RuntimeHelpers.GetUninitializedObject(ordersType);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(orderType))!;
            Set(orders, "m_orders", list);
            Set(orders, "m_entities", manager);
            MethodInfo cut = ordersType.GetMethod("CutBuilding")!;
            EntityValidationResult TryCut() => (EntityValidationResult)cut.Invoke(orders,
                new object[] { CreateOrder(), entity, EntityRemoveReason.Remove })!;

            Check(TryCut().IsSuccess, "an untouched completed building can be cut");
            object previous = CreateOrder();
            list.Add(previous);
            var sources = (HashSet<EntityId>)orderType.GetField("CompletedSources")!.GetValue(previous)!;
            var demolition = (HashSet<EntityId>)orderType.GetField("DemolitionOrdered")!.GetValue(previous)!;
            sources.Add(id);
            Check(TryCut().IsError, "an active preview still owns its source");

            Set(previous, "PreviewDropped", true);
            demolition.Add(id);
            Check(TryCut().IsSuccess, "an abandoned cut must release a source whose demolition was canceled");
            Check(!demolition.Contains(id), "undo of the abandoned cut must not restore a source owned by the new cut");

            Check(!list.Contains(previous), "a cut with no remaining changes leaves the move queue");

            object queued = CreateOrder();
            list.Add(queued);
            ((HashSet<EntityId>)orderType.GetField("CompletedSources")!.GetValue(queued)!).Add(id);
            Set(queued, "PreviewDropped", true);
            Set(queued, "Confirmed", true);
            Check(TryCut().IsError, "a confirmed move retains ownership of the source until it is undone or finished");
        }
        finally
        {
            reportUnique.Invoke(null, new object[] { protoId, false });
        }
        Console.WriteLine("PASS: recut releases a restored source from an abandoned cut and keeps active and queued moves protected.");
    }
}
