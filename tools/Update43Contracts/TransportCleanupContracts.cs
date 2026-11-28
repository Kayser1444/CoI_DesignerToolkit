using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Sorters;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Ports;
using Mafi.Core.Ports.Io;
using Mafi.Core.Terrain;

public static partial class Program
{
    private static void TransportCleanupContracts(string mod)
    {
        Type toolType = Assembly.LoadFrom(mod).GetType("CoIDesignerToolkit.TransportCleanupTool", true)!;
        object tool = RuntimeHelpers.GetUninitializedObject(toolType);
        var entities = new LystMutableDuringIter<IEntity>();
        var manager = (EntitiesManager)RuntimeHelpers.GetUninitializedObject(typeof(EntitiesManager));
        Set(manager, "m_entitiesLinear", entities);
        Set(tool, "m_entitiesManager", manager);

        var inside = new Tile3i(15, 15, 0);
        var outside = new Tile3i(25, 25, 0);
        RectangleTerrainArea2i area = RectangleTerrainArea2i.FromTwoPositions(new Tile2i(10, 10), new Tile2i(20, 20));
        MethodInfo find = toolType.GetMethod("FindMatches", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cases = new List<(IEntity Entity, string Case, bool Selected)>();
        foreach (Type type in new[] { typeof(Lift), typeof(Zipper), typeof(MiniZipper), typeof(Sorter) })
        {
            AddLayout(type, inside, 0, "zero connections", selected: true);
            AddLayout(type, inside, 1, "one connection", selected: true);
            AddLayout(type, inside, 2, "two connections", selected: false);
            if (type != typeof(Lift))
                AddLayout(type, inside, 3, "three connections", selected: false);
            AddLayout(type, outside, 0, "outside selection", selected: false);
            AddLayout(type, inside, 0, "destroyed", selected: false, destroyed: true);
        }
        AddTransport(inside, connected: false, selected: true);
        AddTransport(inside, connected: true, selected: false);
        AddTransport(outside, connected: false, selected: false);

        HashSet<IEntity> matches = Find();
        string[] errors = cases.Where(test => matches.Contains(test.Entity) != test.Selected)
            .Select(test => $"{test.Entity.GetType().Name}, {test.Case}: expected {(test.Selected ? "selected" : "skipped")}")
            .ToArray();
        Check(errors.Length == 0, "Transport cleanup selection failed. " + string.Join("; ", errors));

        IEntityWithPorts lift = (IEntityWithPorts)cases.First(test => test.Entity is Lift && test.Case == "one connection").Entity;
        Set(lift.Ports[1], "<ConnectedPort>k__BackingField", ConnectedPort());
        Check(!Find().Contains(lift), "A lift that gains its second connection is skipped when cleanup checks it again.");
        Console.WriteLine("PASS: transport cleanup area selection respects type, position, and zero/one versus two-or-more port connections.");

        HashSet<IEntity> Find() => new((IEnumerable<IEntity>)find.Invoke(tool, new object[] { area })!,
            ReferenceEqualityComparer.Instance);

        void Track(IEntity entity, string scenario, bool selected)
        {
            Set(entity, "m_id", new EntityId(cases.Count + 1));
            entities.Add(entity);
            cases.Add((entity, scenario, selected));
        }

        void AddLayout(Type type, Tile3i position, int connections, string scenario, bool selected, bool destroyed = false)
        {
            var entity = (IEntityWithPorts)RuntimeHelpers.GetUninitializedObject(type);
            Set(entity, "<CenterTile>k__BackingField", position);
            Set(entity, "<IsDestroyed>k__BackingField", destroyed);
            int portCount = type == typeof(Lift) ? 2 : 4;
            var ports = new ImmutableArrayBuilder<IoPort>(portCount);
            for (int i = 0; i < portCount; ++i)
            {
                var port = (IoPort)RuntimeHelpers.GetUninitializedObject(typeof(IoPort));
                if (i < connections)
                    Set(port, "<ConnectedPort>k__BackingField", ConnectedPort());
                ports[i] = port;
            }
            Set(entity, "<Ports>k__BackingField", ports.GetImmutableArrayAndClear());
            Track(entity, scenario, selected);
        }

        void AddTransport(Tile3i position, bool connected, bool selected)
        {
            var transport = (Transport)RuntimeHelpers.GetUninitializedObject(typeof(Transport));
            var trajectory = (TransportTrajectory)RuntimeHelpers.GetUninitializedObject(typeof(TransportTrajectory));
            Set(trajectory, "Pivots", ImmutableArray.Create(position));
            Set(transport, "m_trajectory", trajectory);
            Set(transport, "<IsFullyConnected>k__BackingField", connected);
            Track(transport, connected ? "fully connected" : $"disconnected at {position}", selected);
        }

        static Option<IoPort> ConnectedPort() => (IoPort)RuntimeHelpers.GetUninitializedObject(typeof(IoPort));
    }
}
