using System.Reflection;
using System.Runtime.CompilerServices;
using CoIDesignerToolkit;
using CoI.AutoHelpers.Persistence;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Products;
using Mafi.Core.PropertiesDb;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Generation;

internal static class Program
{
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            string path = Path.Combine(args[0], new AssemblyName(request.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        Scenario(loaded: false, populated: true);
        Scenario(loaded: true, populated: true);
        Scenario(loaded: false, populated: false);
        Console.WriteLine("PASS: new and loaded worlds discover resources without warnings, restore reservoir history, and support empty resource maps.");
    }

    static void Scenario(bool loaded, bool populated)
    {
        CoI.AutoHelpers.Logging.ModLogger.Warnings.Clear();
        var builder = new DependencyResolverBuilder();
        var calendar = DispatchProxy.Create<ICalendar, Probe>();
        builder.RegisterInstance(calendar).As<ICalendar>();
        builder.RegisterInstance((GameDifficultyConfig)RuntimeHelpers.GetUninitializedObject(typeof(GameDifficultyConfig))).AsSelf();
        builder.RegisterInstance(DispatchProxy.Create<IPropertiesDb, Probe>()).As<IPropertiesDb>();
        var events = DispatchProxy.Create<IGameLoopEvents, Probe>();
        builder.RegisterInstance(events).As<IGameLoopEvents>();
        // Matches a newly constructed manager, before its NewGameCreated callback.
        var resources = (VirtualResourceManager)RuntimeHelpers.GetUninitializedObject(typeof(VirtualResourceManager));
        builder.RegisterInstance(resources).AsSelf();
        var protos = new ProtosDb();
        var product = (VirtualResourceProductProto)RuntimeHelpers.GetUninitializedObject(typeof(VirtualResourceProductProto));
        Set(product, "<Id>k__BackingField", new Proto.ID("GroundwaterContract"));
        protos.Add(product);
        builder.RegisterInstance(protos).AsSelf();
        var resource = DispatchProxy.Create<IVirtualTerrainResource, Probe>();
        ((Probe)(object)resource).Product = product;
        var map = new Dict<VirtualResourceProductProto, ImmutableArray<IVirtualTerrainResource>>();
        if (populated) map[product] = ImmutableArray.Create(resource);
        if (loaded) Set(resources, "m_virtualResourcesMap", map);
        string key = GroundwaterStatsManager.GetResourceKey(resource);
        var store = new Store { Json = "{\"schemaVersion\":1,\"reservoirs\":{\"" + key + "\":{\"recordedMonths\":3,\"monthlyDraws\":[17],\"monthlyHistory\":[100,90],\"yearlyHistory\":[100,80]}}}" };
        GroundwaterStatsManager.Initialize(builder.BuildAndClear(), store);
        Check(CoI.AutoHelpers.Logging.ModLogger.Warnings.Count == 0,
            "Init emitted: " + string.Join("; ", CoI.AutoHelpers.Logging.ModLogger.Warnings));
        Check(store.Loads == 0, "history must wait for resource discovery");
        if (!loaded) Set(resources, "m_virtualResourcesMap", map);
        var callbacks = ((Probe)(object)events).Callbacks;
        Check(callbacks.Count > 0, "resource discovery must run at a world-ready lifecycle event");
        foreach (var callback in callbacks) callback();
        Check(store.Loads == 1, "world-ready discovery must restore cached history");
        Check(CoI.AutoHelpers.Logging.ModLogger.Warnings.Count == 0, "world-ready discovery emitted a warning");
        if (populated)
        {
            var record = GroundwaterStatsManager.Instance!.GetOrCreateRecord(resource)!;
            Check(record.RecordedMonthsCount == 3 && record.MonthlyDraws[0] == 17,
                $"history must be applied to the discovered reservoir: months={record.RecordedMonthsCount}, draw={record.MonthlyDraws[0]}");
            Check(record.MonthlyHistory[1] == 90 && record.YearlyHistory[1] == 80,
                "monthly and yearly level arrays must restore through the native JSON parser");
        }
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object target, string name, object value)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }

    public class Probe : DispatchProxy
    {
        public List<Action> Callbacks = new();
        public VirtualResourceProductProto? Product;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "RegisterInitState" && args?.LastOrDefault() is Action callback) Callbacks.Add(callback);
            if (method.Name == "get_Product") return Product;
            if (method.ReturnType == typeof(IEvent)) return DispatchProxy.Create<IEvent, Probe>();
            if (method.ReturnType == typeof(void)) return null;
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    sealed class Store : IModStateJsonStore
    {
        public int Loads;
        public string Json = "";
        public string StorageKind => "test";
        public string StateKey => "test";
        public string LoadJson(string defaultJson = "") { Loads++; return Json; }
        public ModStateJsonSaveResult SaveJson(string json) => ModStateJsonSaveResult.Success(StorageKind, StateKey);
    }
}

namespace CoI.AutoHelpers.Logging
{
    public sealed class ModLogger
    {
        public ModLogger(string name) { }
        public static List<string> Warnings = new();
        public void Info(string message) { }
        public void Warning(string message) => Warnings.Add(message);
    }
}
