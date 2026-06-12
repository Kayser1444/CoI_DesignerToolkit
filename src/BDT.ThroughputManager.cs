// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.IO;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Simulation;
using Mafi.Serialization;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;

namespace CoIDesignerToolkit;

internal sealed class ThroughputManager : IDisposable
{
    private const int SCHEMA_VERSION = 1;
    public const string CONFIG_KEY = "bdtThroughputConfigStateJson";

    private static readonly ModLogger s_log = new ModLogger("BDT.ThroughputManager");
    private static ThroughputManager? s_instance;

    private readonly Dict<int, EntityState> m_states = new Dict<int, EntityState>();
    private IModStateJsonStore? m_store;

    public static ThroughputManager? Instance => s_instance;

    public sealed class EntityState
    {
        public bool DisplayThroughput;
        public int DaysToAverage = 30;

        // Circular buffer storing daily sums. Size is 360 (1 game year)
        public readonly float[] DailyHistory = new float[360];
        public int HistoryCount;
        public int HistoryHead;
        
        public float CurrentDaySum;
        public float CachedAverageThroughput;

        public void RecalculateAverage()
        {
            if (HistoryCount == 0)
            {
                CachedAverageThroughput = 0f;
                return;
            }

            int take = Math.Min(DaysToAverage, HistoryCount);
            float sum = 0f;
            int idx = HistoryHead;
            for (int i = 0; i < take; i++)
            {
                idx = (idx - 1 + 360) % 360;
                sum += DailyHistory[idx];
            }

            // sum is total items in last `take` days.
            // 30 days = 1 minute real-time.
            // Throughput per minute = (sum / take) * 30.
            CachedAverageThroughput = (sum / take) * 30f;
        }
    }

    public ThroughputManager()
    {
        s_instance = this;
    }

    public void Initialize(DependencyResolver resolver, IModStateJsonStore store)
    {
        m_store = store;
        LoadFromStore();

        try
        {
            var calendar = resolver.Resolve<Calendar>();
            calendar.NewDay.AddNonSaveable(this, OnNewDay);
            
            var entitiesManager = resolver.Resolve<EntitiesManager>();
            entitiesManager.EntityRemoved.AddNonSaveable(this, OnEntityRemoved);

            s_log.Info("ThroughputManager initialized.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to subscribe to calendar/entity events in ThroughputManager: {ex.Message}");
        }
    }

    public void Dispose()
    {
        s_instance = null;
        m_states.Clear();
        m_store = null;
    }

    public EntityState GetOrCreateState(int entityId)
    {
        if (!m_states.TryGetValue(entityId, out var state))
        {
            state = new EntityState();
            m_states[entityId] = state;
        }
        return state;
    }

    public void RecordTransfer(int entityId, float amount)
    {
        if (m_states.TryGetValue(entityId, out var state))
        {
            state.CurrentDaySum += amount;
        }
    }

    public Dict<int, EntityState> GetAllStates()
    {
        return m_states;
    }

    public void OnNewDay()
    {
        foreach (var state in m_states.Values)
        {
            // Push CurrentDaySum into DailyHistory circular buffer
            state.DailyHistory[state.HistoryHead] = state.CurrentDaySum;
            state.HistoryHead = (state.HistoryHead + 1) % 360;
            if (state.HistoryCount < 360)
            {
                state.HistoryCount++;
            }
            state.CurrentDaySum = 0f;
            state.RecalculateAverage();
        }
    }

    public void OnEntityRemoved(IEntity entity)
    {
        if (m_states.Remove(entity.Id.Value))
        {
            SaveToStore();
        }
    }

    public void SaveConfigState()
    {
        SaveToStore();
    }

    private void LoadFromStore()
    {
        m_states.Clear();
        if (m_store == null) return;

        string json = m_store.LoadJson();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root)) return;

            if (!root.TryGetValue("schemaVersion", out object? rawSchema))
                return;

            int schemaVersion = 0;
            if (rawSchema is int sInt) schemaVersion = sInt;
            else if (rawSchema is double sDouble) schemaVersion = (int)sDouble;
            else if (rawSchema is long sLong) schemaVersion = (int)sLong;

            if (schemaVersion != SCHEMA_VERSION)
                return;

            if (root.TryGetValue("configs", out object? rawConfigs) && rawConfigs is Dict<string, object> configs)
            {
                foreach (var kvp in configs)
                {
                    if (int.TryParse(kvp.Key, out int entityIdValue) && kvp.Value is Dict<string, object> configData)
                    {
                        var state = new EntityState();
                        if (configData.TryGetValue("display", out object? rawDisplay) && rawDisplay is bool displayVal)
                        {
                            state.DisplayThroughput = displayVal;
                        }
                        if (configData.TryGetValue("days", out object? rawDays))
                        {
                            int daysVal = 30;
                            if (rawDays is int dInt) daysVal = dInt;
                            else if (rawDays is double dDouble) daysVal = (int)dDouble;
                            else if (rawDays is long dLong) daysVal = (int)dLong;
                            
                            state.DaysToAverage = Math.Max(1, Math.Min(360, daysVal));
                        }

                        // We only load it if display is active or days are non-default
                        if (state.DisplayThroughput || state.DaysToAverage != 30)
                        {
                            m_states[entityIdValue] = state;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load throughput configuration state: {ex.Message}");
        }
    }

    private void SaveToStore()
    {
        if (m_store == null) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"schemaVersion\": {SCHEMA_VERSION},");
            sb.AppendLine("  \"configs\": {");
            bool first = true;
            foreach (var kvp in m_states)
            {
                var state = kvp.Value;
                // Only save if it has a reason to be saved
                if (!state.DisplayThroughput && state.DaysToAverage == 30)
                    continue;

                if (!first) sb.AppendLine(",");
                sb.Append($"    \"{kvp.Key}\": {{ \"display\": {(state.DisplayThroughput ? "true" : "false")}, \"days\": {state.DaysToAverage} }}");
                first = false;
            }
            sb.AppendLine();
            sb.AppendLine("  }");
            sb.AppendLine("}");

            string json = sb.ToString();
            ModStateJsonSaveResult result = m_store.SaveJson(json);
            if (!result.Succeeded)
            {
                s_log.Warning($"Failed to save throughput configuration state: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Exception saving throughput configuration state: {ex.Message}");
        }
    }
}
