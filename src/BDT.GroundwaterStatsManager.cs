// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.IO;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Environment;
using Mafi.Core.Factory.WellPumps;
using Mafi.Core.Game;
using Mafi.Core.Map;
using Mafi.Core.Products;
using Mafi.Core.PropertiesDb;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Generation;
using Mafi.Serialization;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;

namespace CoIDesignerToolkit;

public sealed class GroundwaterStatsManager
{
    private const int SCHEMA_VERSION = 1;
    public const string CONFIG_KEY = "bdtGroundwaterStateJson";

    private static readonly ModLogger s_log = new ModLogger("BDT.GroundwaterStatsManager");
    private static GroundwaterStatsManager? s_instance;

    public const int MONTHLY_HISTORY_COUNT = 13; // Index 0 is "Now", 1..12 are -1..-12 months
    public const int YEARLY_HISTORY_COUNT = 11;  // Index 0 is "Now", 1..10 are -1..-10 years (Jan 1)
    public const int MONTHLY_DRAWS_COUNT = 12;   // Past 12 recorded monthly draws (index 0 is most recent completed month)

    public sealed class ResourceRecord
    {
        public readonly IVirtualTerrainResource Resource;
        public readonly string Key;

        public readonly long[] MonthlyHistory = new long[MONTHLY_HISTORY_COUNT];
        public readonly long[] YearlyHistory = new long[YEARLY_HISTORY_COUNT];
        public readonly long[] MonthlyDraws = new long[MONTHLY_DRAWS_COUNT];

        public int RecordedMonthsCount { get; internal set; }
        public int RecordedYearsCount { get; internal set; }
        public Quantity CurrentMonthDraw { get; internal set; }

        public ResourceRecord(IVirtualTerrainResource resource, string key)
        {
            Resource = resource;
            Key = key;
            MonthlyHistory[0] = resource.Quantity.Value;
            YearlyHistory[0] = resource.Quantity.Value;
        }

        public void RecordNewMonth(bool isNewYear)
        {
            // Shift monthly history: index 12 <- 11 ... index 1 <- index 0 snapshot
            for (int i = MONTHLY_HISTORY_COUNT - 1; i >= 2; i--)
            {
                MonthlyHistory[i] = MonthlyHistory[i - 1];
            }
            MonthlyHistory[1] = MonthlyHistory[0]; // Snapshot at start of month
            MonthlyHistory[0] = Resource.Quantity.Value;

            // Shift monthly draws: index 11 <- 10 ... index 0 <- CurrentMonthDraw
            for (int i = MONTHLY_DRAWS_COUNT - 1; i >= 1; i--)
            {
                MonthlyDraws[i] = MonthlyDraws[i - 1];
            }
            MonthlyDraws[0] = CurrentMonthDraw.Value;
            CurrentMonthDraw = Quantity.Zero;

            if (RecordedMonthsCount < MONTHLY_DRAWS_COUNT)
            {
                RecordedMonthsCount++;
            }

            if (isNewYear)
            {
                RecordNewYear();
            }
        }

        public void RecordNewYear()
        {
            // Shift yearly history: index 10 <- 9 ... index 1 <- index 0 snapshot (Jan 1)
            for (int i = YEARLY_HISTORY_COUNT - 1; i >= 2; i--)
            {
                YearlyHistory[i] = YearlyHistory[i - 1];
            }
            YearlyHistory[1] = YearlyHistory[0];
            YearlyHistory[0] = Resource.Quantity.Value;

            if (RecordedYearsCount < YEARLY_HISTORY_COUNT - 1)
            {
                RecordedYearsCount++;
            }
        }

        public void UpdateCurrentLevel()
        {
            MonthlyHistory[0] = Resource.Quantity.Value;
            YearlyHistory[0] = Resource.Quantity.Value;
        }

        public Quantity CalculateAverageMonthlyDraw()
        {
            int count = Math.Min(RecordedMonthsCount, MONTHLY_DRAWS_COUNT);
            if (count <= 0)
            {
                return CurrentMonthDraw;
            }

            long total = 0;
            for (int i = 0; i < count; i++)
            {
                total += MonthlyDraws[i];
            }
            return new Quantity((int)Math.Round((double)total / count));
        }
    }

    private readonly Dict<IVirtualTerrainResource, ResourceRecord> m_records = new Dict<IVirtualTerrainResource, ResourceRecord>();
    private readonly Dict<string, ResourceRecord> m_recordsByKey = new Dict<string, ResourceRecord>();
    private IModStateJsonStore? m_store;
    private ICalendar? m_calendar;
    private GameDifficultyConfig? m_difficultyConfig;
    private IPropertiesDb? m_propertiesDb;
    private VirtualResourceManager? m_virtualResourceManager;

    public static GroundwaterStatsManager? Instance => s_instance;
    public VirtualResourceManager? VirtualResourceManager => m_virtualResourceManager;

    public static void Initialize(DependencyResolver resolver, IModStateJsonStore store)
    {
        s_instance = new GroundwaterStatsManager();
        s_instance.Init(resolver, store);
    }

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var mineMethod = typeof(SimpleVirtualResource).GetMethod(nameof(SimpleVirtualResource.MineResourceAt));
            if (mineMethod != null)
            {
                harmony.Patch(mineMethod, postfix: new HarmonyMethod(typeof(GroundwaterStatsManager), nameof(MineResourceAtPostfix)));
                s_log.Info("Patched SimpleVirtualResource.MineResourceAt for groundwater tracking");
            }
            else
            {
                s_log.Warning("Could not find SimpleVirtualResource.MineResourceAt to patch");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to patch SimpleVirtualResource: {ex.Message}");
        }
    }

    private void Init(DependencyResolver resolver, IModStateJsonStore store)
    {
        m_store = store;
        m_calendar = resolver.Resolve<ICalendar>();
        m_difficultyConfig = resolver.Resolve<GameDifficultyConfig>();
        m_propertiesDb = resolver.Resolve<IPropertiesDb>();
        m_virtualResourceManager = resolver.Resolve<VirtualResourceManager>();

        m_calendar.NewMonthStart.AddNonSaveable(this, onNewMonthStart);
        m_calendar.NewDay.AddNonSaveable(this, onNewDay);

        // Pre-discover existing groundwater resources
        try
        {
            var protosDb = resolver.Resolve<Mafi.Core.Prototypes.ProtosDb>();
            var groundwaterProto = protosDb.GetOrThrow<VirtualResourceProductProto>(IdsCore.Products.Groundwater);
            var allResources = m_virtualResourceManager.GetAllResourcesFor(groundwaterProto);
            foreach (var res in allResources)
            {
                GetOrCreateRecord(res);
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error discovering initial groundwater resources: {ex.Message}");
        }

        LoadFromStore();
    }

    public static string GetResourceKey(IVirtualTerrainResource resource)
    {
        if (resource is SimpleVirtualResource svr)
        {
            return $"res_{svr.Position.X}_{svr.Position.Y}_{svr.Product.Id.Value}";
        }
        return $"res_{resource.Product.Id.Value}_{resource.GetHashCode()}";
    }

    public ResourceRecord GetOrCreateRecord(IVirtualTerrainResource resource)
    {
        if (!m_records.TryGetValue(resource, out var record))
        {
            string key = GetResourceKey(resource);
            if (!m_recordsByKey.TryGetValue(key, out record))
            {
                record = new ResourceRecord(resource, key);
                m_recordsByKey[key] = record;
            }
            m_records[resource] = record;
        }
        return record;
    }

    public IVirtualTerrainResource? GetResourceAt(ProductProto product, Tile2i position)
    {
        if (m_virtualResourceManager == null) return null;
        var resources = m_virtualResourceManager.RetrieveResourcesAt(product, position);
        return resources.Length > 0 ? resources[0] : null;
    }

    public static void MineResourceAtPostfix(SimpleVirtualResource __instance, ProductQuantity __result)
    {
        if (s_instance == null || __result.Quantity.IsZero) return;

        try
        {
            var record = s_instance.GetOrCreateRecord(__instance);
            record.CurrentMonthDraw += __result.Quantity;
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in MineResourceAtPostfix: {ex.Message}");
        }
    }

    private void onNewMonthStart()
    {
        bool isNewYear = m_calendar != null && m_calendar.CurrentDate.Month == 1;
        foreach (var kvp in m_records)
        {
            kvp.Value.RecordNewMonth(isNewYear);
        }
        SaveToStore();
    }

    private void onNewDay()
    {
        foreach (var kvp in m_records)
        {
            kvp.Value.UpdateCurrentLevel();
        }
    }

    /// <summary>
    /// Calculates the maximum sustainable monthly draw for this groundwater reservoir.
    /// Based on post-year-10 steady-state annual rainfall factor for the current difficulty setting:
    /// Annual Replenish = ConfiguredCapacity * 0.00185 * 15 * AnnualRainFactor
    /// Monthly Sustainable Draw = Annual Replenish / 12 = ConfiguredCapacity * 0.00185 * 1.25 * AnnualRainFactor
    /// </summary>
    public Quantity CalculateMaxSustainableMonthlyDraw(IVirtualTerrainResource resource)
    {
        if (resource == null) return Quantity.Zero;

        float annualRainFactor = 3.5f; // default Standard post-year 10
        if (m_difficultyConfig != null)
        {
            switch (m_difficultyConfig.WeatherDifficulty)
            {
                case GameDifficultyConfig.WeatherDifficultySetting.Easy:
                    annualRainFactor = 4.0f;
                    break;
                case GameDifficultyConfig.WeatherDifficultySetting.Dry:
                    annualRainFactor = 3.25f;
                    break;
                default:
                    annualRainFactor = 3.5f;
                    break;
            }
        }

        double multiplier = 0.00185 * 1.25 * annualRainFactor;
        long sustainableMonthly = (long)Math.Round(resource.ConfiguredCapacity.Value * multiplier);
        return new Quantity((int)Math.Max(0, sustainableMonthly));
    }

    public void LoadFromStore()
    {
        if (m_store == null) return;

        string json = m_store.LoadJson();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root)) return;

            if (!root.TryGetValue("schemaVersion", out object? rawSchema)) return;

            int schemaVersion = 0;
            if (rawSchema is int sInt) schemaVersion = sInt;
            else if (rawSchema is double sDouble) schemaVersion = (int)sDouble;
            else if (rawSchema is long sLong) schemaVersion = (int)sLong;

            if (schemaVersion != SCHEMA_VERSION) return;

            if (root.TryGetValue("reservoirs", out object? rawReservoirs) && rawReservoirs is Dict<string, object> reservoirs)
            {
                foreach (var kvp in reservoirs)
                {
                    string key = kvp.Key;
                    if (kvp.Value is Dict<string, object> resData)
                    {
                        if (m_recordsByKey.TryGetValue(key, out var record))
                        {
                            LoadRecordData(record, resData);
                        }
                    }
                }
            }
            s_log.Info($"Loaded groundwater stats from cache for {m_recordsByKey.Count} reservoirs.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load groundwater state from store: {ex.Message}");
        }
    }

    private static void LoadRecordData(ResourceRecord record, Dict<string, object> resData)
    {
        if (resData.TryGetValue("recordedMonths", out object? rawMonths))
            record.RecordedMonthsCount = ToInt(rawMonths);

        if (resData.TryGetValue("recordedYears", out object? rawYears))
            record.RecordedYearsCount = ToInt(rawYears);

        if (resData.TryGetValue("monthlyHistory", out object? rawMonthly) && rawMonthly is Lyst<object> monthlyList)
        {
            for (int i = 0; i < Math.Min(MONTHLY_HISTORY_COUNT, monthlyList.Count); i++)
                record.MonthlyHistory[i] = ToLong(monthlyList[i]);
        }

        if (resData.TryGetValue("yearlyHistory", out object? rawYearly) && rawYearly is Lyst<object> yearlyList)
        {
            for (int i = 0; i < Math.Min(YEARLY_HISTORY_COUNT, yearlyList.Count); i++)
                record.YearlyHistory[i] = ToLong(yearlyList[i]);
        }

        if (resData.TryGetValue("monthlyDraws", out object? rawDraws) && rawDraws is Lyst<object> drawsList)
        {
            for (int i = 0; i < Math.Min(MONTHLY_DRAWS_COUNT, drawsList.Count); i++)
                record.MonthlyDraws[i] = ToLong(drawsList[i]);
        }
    }

    private static int ToInt(object? obj)
    {
        if (obj is int i) return i;
        if (obj is long l) return (int)l;
        if (obj is double d) return (int)d;
        return 0;
    }

    private static long ToLong(object? obj)
    {
        if (obj is long l) return l;
        if (obj is int i) return i;
        if (obj is double d) return (long)d;
        return 0L;
    }

    public void SaveToStore()
    {
        if (m_store == null) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"schemaVersion\": {SCHEMA_VERSION},\n");
            sb.Append("  \"reservoirs\": {\n");

            bool firstRes = true;
            foreach (var kvp in m_recordsByKey)
            {
                var record = kvp.Value;
                if (!firstRes) sb.Append(",\n");
                firstRes = false;

                sb.Append($"    \"{EscapeJson(kvp.Key)}\": {{\n");
                sb.Append($"      \"recordedMonths\": {record.RecordedMonthsCount},\n");
                sb.Append($"      \"recordedYears\": {record.RecordedYearsCount},\n");

                sb.Append("      \"monthlyHistory\": [");
                for (int i = 0; i < MONTHLY_HISTORY_COUNT; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(record.MonthlyHistory[i]);
                }
                sb.Append("],\n");

                sb.Append("      \"yearlyHistory\": [");
                for (int i = 0; i < YEARLY_HISTORY_COUNT; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(record.YearlyHistory[i]);
                }
                sb.Append("],\n");

                sb.Append("      \"monthlyDraws\": [");
                for (int i = 0; i < MONTHLY_DRAWS_COUNT; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(record.MonthlyDraws[i]);
                }
                sb.Append("]\n");

                sb.Append("    }");
            }

            sb.Append("\n  }\n}");
            m_store.SaveJson(sb.ToString());
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to save groundwater state to store: {ex.Message}");
        }
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
