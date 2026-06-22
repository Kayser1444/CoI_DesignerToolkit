using System;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Simulation;
using Mafi.Core.Entities.Ships;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.PropertiesDb;
using Mafi.Core.Prototypes;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal sealed class PollutionManager : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.PollutionManager");
    private static PollutionManager? s_instance;

    private readonly Dict<int, EntityPollutionState> m_states = new Dict<int, EntityPollutionState>();
    private IEntitiesManager? m_entitiesManager;

    private IProperty<Percent>? m_vehiclesPollutionMultiplier;
    private IProperty<Percent>? m_trainsPollutionMultiplier;
    private IProperty<Percent>? m_shipsPollutionMultiplier;
    private IProperty<Percent>? m_airPollutionMultiplier;
    private IProperty<Percent>? m_waterPollutionMultiplier;
    private ISimLoopEvents? m_simLoopEvents;
    private float m_dieselPollutionPercent = 1f;

    public IEntitiesManager? EntitiesManager => m_entitiesManager;

    public static PollutionManager? Instance => s_instance;

    public float VehiclesPollutionMultiplier => m_vehiclesPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float TrainsPollutionMultiplier => m_trainsPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float ShipsPollutionMultiplier => m_shipsPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float AirPollutionMultiplier => m_airPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float WaterPollutionMultiplier => m_waterPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float DieselPollutionPercent => m_dieselPollutionPercent;

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
            
            var propsDb = resolver.Resolve<IPropertiesDb>();
            m_vehiclesPollutionMultiplier = propsDb.GetProperty(IdsCore.PropertyIds.VehiclesPollutionMultiplier);
            m_trainsPollutionMultiplier = propsDb.GetProperty(IdsCore.PropertyIds.TrainsPollutionMultiplier);
            m_shipsPollutionMultiplier = propsDb.GetProperty(IdsCore.PropertyIds.ShipsPollutionMultiplier);
            m_airPollutionMultiplier = propsDb.GetProperty(IdsCore.PropertyIds.AirPollutionMultiplier);
            m_waterPollutionMultiplier = propsDb.GetProperty(IdsCore.PropertyIds.WaterPollutionMultiplier);
            m_simLoopEvents = resolver.Resolve<ISimLoopEvents>();

            m_entitiesManager.EntityRemoved.AddNonSaveable(this, OnEntityRemoved);

            var protosDb = resolver.Resolve<ProtosDb>();
            foreach (var proto in protosDb.All<FuelTankProto>())
            {
                if (proto.Product.Id == IdsCore.Products.Diesel)
                {
                    m_dieselPollutionPercent = proto.PollutionPercent.ToFloat();
                    s_log.Info($"Found Diesel pollution percent: {m_dieselPollutionPercent}");
                    break;
                }
            }

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
        if (m_entitiesManager != null)
        {
            try
            {
                m_entitiesManager.EntityRemoved.Remove(this, OnEntityRemoved);
            }
            catch {}
        }
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

    private void OnEntityAdded(IEntity entity)
    {
    }

    public void OnEntityRemoved(IEntity entity)
    {
        int id = entity.Id.Value;
        m_states.Remove(id);
    }
}
