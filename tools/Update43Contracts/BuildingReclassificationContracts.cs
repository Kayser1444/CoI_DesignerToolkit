using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain.Designation;

public static partial class Program
{
    private static void BuildingReclassificationContracts(string mod)
    {
        Assembly assembly = Assembly.LoadFrom(mod);
        Type ordersType = assembly.GetType("CoIDesignerToolkit.BuildingMoveOrders", true)!;
        Type orderType = ordersType.GetNestedType("Order", BindingFlags.NonPublic)!;
        Type api = assembly.GetType("CoIDesignerToolkit.BuildingMoveApi", true)!;
        MethodInfo report = api.GetMethod("SetUniqueForMove")!;
        void Report(string id, bool unique) => report.Invoke(null, new object[] { id, unique });

        const string id = "Contracts.RuntimeUnique";
        const string unrelatedId = "Contracts.OtherUnique";
        var proto = (TransportProto)RuntimeHelpers.GetUninitializedObject(typeof(TransportProto));
        Set(proto, "<Id>k__BackingField", new Proto.ID(id));
        Set(proto, "<IsUnlocked>k__BackingField", true);
        var originId = new EntityId(61);
        var origin = (Transport)RuntimeHelpers.GetUninitializedObject(typeof(Transport));
        Set(origin, "m_id", originId);
        Set(origin, "m_constructionState", Mafi.Core.Entities.Static.ConstructionState.InDeconstruction);
        var byId = new Dict<EntityId, IEntity>();
        byId.Add(originId, origin);
        var entities = (EntitiesManager)RuntimeHelpers.GetUninitializedObject(typeof(EntitiesManager));
        Set(entities, "m_entitiesById", byId);

        var protos = new ProtosDb();
        var surfaceDesignator = (SurfaceDesignationProto)RuntimeHelpers.GetUninitializedObject(typeof(SurfaceDesignationProto));
        var registeredProtos = new Dict<Proto.ID, Proto>();
        registeredProtos.Add(IdsCore.TerrainDesignators.PlaceSurfaceDesignator, surfaceDesignator);
        Set(protos, "m_protoById", registeredProtos);

        var target = new EntityConfigData(proto, null) { OriginalEntityId = originId };
        object order = Activator.CreateInstance(orderType, ImmutableArray<TileSurfaceCopyPasteData>.Empty,
            ImmutableArray<TileSurfaceCopyPasteData>.Empty)!;
        Set(order, "Target", ImmutableArray.Create(target));
        Set(order, "Confirmed", true);
        ((HashSet<EntityId>)orderType.GetField("CompletedSources")!.GetValue(order)!).Add(originId);
        ((HashSet<EntityId>)orderType.GetField("DemolitionOrdered")!.GetValue(order)!).Add(originId);

        var queue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(orderType))!;
        queue.Add(order);
        var probe = new PlacementProbe();
        object orders = RuntimeHelpers.GetUninitializedObject(ordersType);
        Set(orders, "m_orders", queue);
        Set(orders, "m_input", new ConcurrentQueue<Action>());
        Set(orders, "m_canceledDemolitions", new HashSet<EntityId>());
        Set(orders, "m_entities", entities);
        Set(orders, "m_protos", protos);
        Set(orders, "m_construct", probe);
        MethodInfo update = ordersType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Step()
        {
            try { update.Invoke(orders, null); }
            catch (TargetInvocationException ex) when (ex.InnerException is PlacementReached) { }
        }

        try
        {
            Report(id, true);
            Set(orders, "m_retryTick", 29);
            Step();
            Check(probe.Command == null, "custom uniqueness blocks placement while the original survives");
            Report(unrelatedId, true);
            Report(unrelatedId, false);
            Step();
            Check(probe.Command == null, "relaxing an unrelated prototype leaves this move waiting");

            Report(id, false);
            Report(id, true);
            Step();
            Check(probe.Command == null, "retry checks the current rule after rapid reclassification");

            Set(order, "Blocked", "Previous validation failure");
            Report(id, false);
            Step();
            Check(probe.Command?.ConfigData.Length == 1 && ReferenceEquals(probe.Command.ConfigData[0], target),
                "withdrawing uniqueness retries even a blocked destination next cycle while its original survives");
            Check(probe.Command is { AllowValidationSuppression: false }, "retry uses ordinary placement validation");
        }
        finally
        {
            Report(id, false);
            Report(unrelatedId, false);
        }
        Console.WriteLine("PASS: reclassifying a queued prototype retries ordinary placement on the next simulation pass.");
    }

    private sealed class PlacementProbe : IAction<BatchCreateStaticEntitiesCmd>
    {
        public BatchCreateStaticEntitiesCmd? Command { get; private set; }

        public void Invoke(BatchCreateStaticEntitiesCmd command)
        {
            Command = command;
            // Stop at native construction dispatch. This fixture owns no game world or HUD.
            throw new PlacementReached();
        }
    }

    private sealed class PlacementReached : Exception { }
}
