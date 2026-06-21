// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Simulation;
using Mafi.Core.Entities.Ships;
using Mafi.Core.Entities.Dynamic;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal sealed class PollutionManager : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.PollutionManager");
    private static PollutionManager? s_instance;

    private readonly Dict<int, EntityPollutionState> m_states = new Dict<int, EntityPollutionState>();
    private IEntitiesManager? m_entitiesManager;

    public IEntitiesManager? EntitiesManager => m_entitiesManager;

    public static PollutionManager? Instance => s_instance;

    public enum PollutionType
    {
        Air,
        Ground,
        Vehicle,
        Ship
    }

    public sealed class EntityPollutionState
    {
        public readonly float[] DailyHistory = new float[360];
        public int HistoryCount;
        public int HistoryHead;
        
        public float CurrentDaySum;
        public float CachedAveragePollution;
        public PollutionType Type;

        public void RecalculateAverage(int daysToAverage)
        {
            if (daysToAverage <= 0)
            {
                CachedAveragePollution = 0f;
                return;
            }
            if (HistoryCount == 0)
            {
                CachedAveragePollution = 0f;
                return;
            }

            int take = Math.Min(daysToAverage, HistoryCount);
            float sum = 0f;
            int idx = HistoryHead;
            for (int i = 0; i < take; i++)
            {
                idx = (idx - 1 + 360) % 360;
                sum += DailyHistory[idx];
            }

            // sum is total pollution in last `take` days.
            // 30 days = 1 minute real-time.
            // Pollution per minute = (sum / take) * 30.
            CachedAveragePollution = (sum / take) * 30f;
        }
    }

    public PollutionManager()
    {
        s_instance = this;
    }

    public void Initialize(DependencyResolver resolver)
    {
        m_entitiesManager = resolver.Resolve<IEntitiesManager>();

        try
        {
            var calendar = resolver.Resolve<Calendar>();
            calendar.NewDay.AddNonSaveable(this, OnNewDay);
            
            m_entitiesManager.EntityRemoved.AddNonSaveable(this, OnEntityRemoved);

            s_log.Info("PollutionManager initialized.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to subscribe to calendar/entity events in PollutionManager: {ex.Message}");
        }
    }

    public void Dispose()
    {
        s_instance = null;
        m_states.Clear();
        m_entitiesManager = null;
    }

    public EntityPollutionState GetOrCreateState(int entityId, PollutionType type)
    {
        if (!m_states.TryGetValue(entityId, out var state))
        {
            state = new EntityPollutionState { Type = type };
            m_states[entityId] = state;
        }
        return state;
    }

    public void RecordPollution(int entityId, float amount, PollutionType type)
    {
        if (DesignerToolkitSettings.PollutionDaysToAverage == 0)
            return;

        var state = GetOrCreateState(entityId, type);
        state.CurrentDaySum += amount;
    }

    public Dict<int, EntityPollutionState> GetAllStates()
    {
        return m_states;
    }

    public void OnNewDay()
    {
        int days = DesignerToolkitSettings.PollutionDaysToAverage;
        if (days == 0)
        {
            m_states.Clear();
            return;
        }

        // 1. Nominal Ship Pollution Polling
        PollShipsNominalPollution();

        // 2. Roll history for all states
        foreach (var state in m_states.Values)
        {
            state.DailyHistory[state.HistoryHead] = state.CurrentDaySum;
            state.HistoryHead = (state.HistoryHead + 1) % 360;
            if (state.HistoryCount < 360)
            {
                state.HistoryCount++;
            }
            state.CurrentDaySum = 0f;
            state.RecalculateAverage(days);
        }
    }

    private void PollShipsNominalPollution()
    {
        if (m_entitiesManager == null) return;

        try
        {
            foreach (Ship ship in m_entitiesManager.GetAllEntitiesOfType<Ship>())
            {
                if (ship.IsDestroyed || !ship.IsEnabled) continue;

                // Ship nominal active rate: if IsEngineOn or IsMoving is true
                if (ship.IsEngineOn || ship.IsMoving)
                {
                    var proto = ship.Prototype;
                    if (proto.FuelTankProto.HasValue)
                    {
                        var ftp = proto.FuelTankProto.Value;
                        float capacity = ftp.Capacity.Value;
                        float ticks = ftp.Duration.Ticks;
                        float pollutionPercent = ftp.PollutionPercent.ToFloat();
                        float dailySum = (capacity * 600f) / ticks * pollutionPercent / 30f;

                        RecordPollution(ship.Id.Value, dailySum, PollutionType.Ship);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in PollShipsNominalPollution: {ex.Message}");
        }
    }

    public void OnEntityRemoved(IEntity entity)
    {
        m_states.Remove(entity.Id.Value);
    }
}
