using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;

public static partial class Program
{
    private static void BuildingCancellationContracts(string mod)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        Assembly bdt = Assembly.LoadFrom(mod);
        Type ordersType = bdt.GetType("CoIDesignerToolkit.BuildingMoveOrders", true)!;
        Type orderType = ordersType.GetNestedType("Order", BindingFlags.NonPublic)!;
        MethodInfo resolve = ordersType.GetMethod("ResolveCanceledDemolition", hidden)!;
        MethodInfo observe = ordersType.GetMethod("OnConstructionStateChanged", hidden)!;
        MethodInfo report = bdt.GetType("CoIDesignerToolkit.BuildingMoveApi", true)!.GetMethod("SetUniqueForMove")!;
        var unique = (TransportProto)RuntimeHelpers.GetUninitializedObject(typeof(TransportProto));
        Set(unique, "<Id>k__BackingField", new Proto.ID("Contracts.CanceledUnique"));
        var ordinary = (TransportProto)RuntimeHelpers.GetUninitializedObject(typeof(TransportProto));
        Set(ordinary, "<Id>k__BackingField", new Proto.ID("Contracts.CanceledOrdinary"));

        var byId = new Dict<EntityId, IEntity>();
        var manager = (EntitiesManager)RuntimeHelpers.GetUninitializedObject(typeof(EntitiesManager));
        Set(manager, "m_entitiesById", byId);
        var construction = DispatchProxy.Create<IConstructionManager, DemolitionCancellationProbe>();
        var context = (EntityContext)RuntimeHelpers.GetUninitializedObject(typeof(EntityContext));
        Set(context, "ConstructionManager", construction);
        var queue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(orderType))!;
        var canceled = new HashSet<EntityId>();
        object orders = RuntimeHelpers.GetUninitializedObject(ordersType);
        Set(orders, "m_entities", manager);
        Set(orders, "m_orders", queue);
        Set(orders, "m_canceledDemolitions", canceled);

        EntityConfigData Config(int id, TransportProto proto) => new EntityConfigData(proto, null) { OriginalEntityId = new EntityId(id) };
        Transport AddOriginal(EntityConfigData config, ConstructionState state)
        {
            var entity = (Transport)RuntimeHelpers.GetUninitializedObject(typeof(Transport));
            var id = config.OriginalEntityId!.Value;
            Set(entity, "m_id", id);
            Set(entity, "<Context>k__BackingField", context);
            Set(entity, "<Prototype>k__BackingField", config.Prototype.Value);
            typeof(StaticEntity).GetField("m_proto", hidden)!.SetValue(entity, config.Prototype.Value);
            Set(entity, "m_constructionState", state);
            byId.Add(id, entity);
            return entity;
        }
        HashSet<EntityId> Ids(object order, string name) => (HashSet<EntityId>)orderType.GetField(name)!.GetValue(order)!;
        ImmutableArray<EntityConfigData> Payload(object order, string name) =>
            (ImmutableArray<EntityConfigData>)orderType.GetField(name)!.GetValue(order)!;
        object QueuedOrder(int originalUniqueCount, params EntityConfigData[] original)
        {
            object order = Activator.CreateInstance(orderType, ImmutableArray<TileSurfaceCopyPasteData>.Empty,
                ImmutableArray<TileSurfaceCopyPasteData>.Empty)!;
            Set(order, "Confirmed", true);
            Set(order, "InitialUniqueSourceCount", originalUniqueCount);
            var payload = original.ToImmutableArray();
            Set(order, "Source", payload);
            Set(order, "Target", payload);
            foreach (var config in original)
            {
                Ids(order, "CompletedSources").Add(config.OriginalEntityId!.Value);
                Ids(order, "DemolitionOrdered").Add(config.OriginalEntityId!.Value);
            }
            queue.Add(order);
            return order;
        }
        bool Resolve(object order, params EntityId[] ids) => (bool)resolve.Invoke(orders, new object[] { order, ids })!;

        report.Invoke(null, new object[] { unique.Id.Value, true });
        try
        {
            var sole = Config(71, unique);
            var companion = Config(72, ordinary);
            Transport surviving = AddOriginal(sole, ConstructionState.Constructed);
            object single = QueuedOrder(1, sole, companion);
            observe.Invoke(orders, new object[] { surviving, ConstructionState.Constructed });
            Check(canceled.SetEquals(new[] { surviving.Id }), "native construction restart reports a canceled unique demolition");
            Check(Resolve(single, canceled.ToArray()), "canceling the only original unique allows the rest to continue");
            Check(queue.Contains(single) && Payload(single, "Target").Length == 1
                && ReferenceEquals(Payload(single, "Target")[0], companion), "remaining destination payload is preserved");
            Check(Payload(single, "Source").Length == 1 && !Ids(single, "CompletedSources").Contains(surviving.Id)
                && !Ids(single, "DemolitionOrdered").Contains(surviving.Id), "canceled original cannot be moved or restored by this order");
            canceled.Clear();

            var survivingUnique = Config(81, unique);
            var demolishedUnique = Config(82, unique);
            var pendingOrdinary = Config(83, ordinary);
            var demolishedOrdinary = Config(84, ordinary);
            Transport canceledOriginal = AddOriginal(survivingUnique, ConstructionState.InConstruction);
            Transport pending = AddOriginal(pendingOrdinary, ConstructionState.PendingDeconstruction);
            object several = QueuedOrder(2, survivingUnique, demolishedUnique, pendingOrdinary, demolishedOrdinary);
            Check(!Resolve(several, canceledOriginal.Id), "last surviving unique still cancels a batch that started with two");
            Check(!queue.Contains(several) && (bool)orderType.GetField("Terminal")!.GetValue(several)!, "canceled batch leaves the placement queue");
            Check(pending.ConstructionState == ConstructionState.Constructed, "a remaining demolition is reversed through native construction");
            Check(Ids(several, "DemolitionOrdered").SetEquals(new[] { demolishedUnique.OriginalEntityId!.Value, demolishedOrdinary.OriginalEntityId!.Value }),
                "already demolished originals remain eligible for explicit undo");

            var onlyBuilding = Config(91, unique);
            Transport restored = AddOriginal(onlyBuilding, ConstructionState.Constructed);
            object noRemainder = QueuedOrder(1, onlyBuilding);
            Check(!Resolve(noRemainder, restored.Id) && !queue.Contains(noRemainder), "canceling the whole contents ends the move without a placement");
        }
        finally
        {
            report.Invoke(null, new object[] { unique.Id.Value, false });
        }
        Console.WriteLine("PASS: canceled unique demolition trims a single-unique move and ends a multi-unique batch while preserving undo.");
    }

    public class DemolitionCancellationProbe : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != nameof(IConstructionManager.StartConstruction))
                throw new NotSupportedException(method?.Name);
            Set(args![0]!, "m_constructionState", ConstructionState.Constructed);
            return null;
        }
    }
}
