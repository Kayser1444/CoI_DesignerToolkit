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
using Mafi.Core.PropertiesDb;
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

    private readonly Dict<int, float> m_shipAccumulatedFuel = new Dict<int, float>();
    private readonly Dict<int, Dict<string, ShipTransitionState>> m_shipTransitions = new Dict<int, Dict<string, ShipTransitionState>>();
    private readonly Dict<int, Action> m_undockedHandlers = new Dict<int, Action>();
    private readonly Dict<int, Action> m_arrivedHandlers = new Dict<int, Action>();

    public IEntitiesManager? EntitiesManager => m_entitiesManager;

    public static PollutionManager? Instance => s_instance;

    public float VehiclesPollutionMultiplier => m_vehiclesPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float TrainsPollutionMultiplier => m_trainsPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float ShipsPollutionMultiplier => m_shipsPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float AirPollutionMultiplier => m_airPollutionMultiplier?.Value.ToFloat() ?? 1f;
    public float WaterPollutionMultiplier => m_waterPollutionMultiplier?.Value.ToFloat() ?? 1f;

    public enum PollutionType
    {
        Air,
        Ground,
        Vehicle,
        Ship
    }

    public sealed class ShipTransitionState
    {
        public int LastTicks;
        public float LastFuel;
        public bool HasPrevious;
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
            m_entitiesManager.EntityAdded.AddNonSaveable(this, OnEntityAdded);

            foreach (Ship ship in m_entitiesManager.GetAllEntitiesOfType<Ship>())
            {
                SubscribeToShip(ship);
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
                m_entitiesManager.EntityAdded.Remove(this, OnEntityAdded);
            }
            catch {}
            try
            {
                m_entitiesManager.EntityRemoved.Remove(this, OnEntityRemoved);
            }
            catch {}
            try
            {
                foreach (Ship ship in m_entitiesManager.GetAllEntitiesOfType<Ship>())
                {
                    UnsubscribeFromShip(ship.Id.Value);
                }
            }
            catch {}
        }
        m_states.Clear();
        m_shipTransitions.Clear();
        m_shipAccumulatedFuel.Clear();
        m_undockedHandlers.Clear();
        m_arrivedHandlers.Clear();
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
            if (state.Type == PollutionType.Ship) continue;

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
        if (entity is Ship ship)
        {
            SubscribeToShip(ship);
        }
    }

    private void SubscribeToShip(Ship ship)
    {
        int shipId = ship.Id.Value;
        UnsubscribeFromShip(shipId);

        Action undocked = () => HandleShipTransition(ship, "Undocked");
        Action arrived = () => HandleShipTransition(ship, "ArrivedFromWorld");

        m_undockedHandlers[shipId] = undocked;
        m_arrivedHandlers[shipId] = arrived;

        ship.OnUndocked += undocked;
        ship.OnArrivedFromWorld += arrived;
    }

    private void UnsubscribeFromShip(int shipId)
    {
        if (m_entitiesManager != null && m_entitiesManager.TryGetEntity(new EntityId(shipId), out IEntity entity) && entity is Ship ship)
        {
            if (m_undockedHandlers.TryGetValue(shipId, out var undocked))
            {
                ship.OnUndocked -= undocked;
                m_undockedHandlers.Remove(shipId);
            }
            if (m_arrivedHandlers.TryGetValue(shipId, out var arrived))
            {
                ship.OnArrivedFromWorld -= arrived;
                m_arrivedHandlers.Remove(shipId);
            }
        }
    }

    public void RecordShipFuel(int shipId, float amount)
    {
        if (!m_shipAccumulatedFuel.TryGetValue(shipId, out float total))
        {
            total = 0f;
        }
        m_shipAccumulatedFuel[shipId] = total + amount;
    }

    private void HandleShipTransition(Ship ship, string transitionType)
    {
        if (m_simLoopEvents == null) return;

        int currentTicks = m_simLoopEvents.CurrentStep.Value;
        int shipId = ship.Id.Value;

        if (!m_shipAccumulatedFuel.TryGetValue(shipId, out float currentFuel))
        {
            currentFuel = 0f;
        }

        if (!m_shipTransitions.TryGetValue(shipId, out var transitions))
        {
            transitions = new Dict<string, ShipTransitionState>();
            m_shipTransitions[shipId] = transitions;
        }

        if (!transitions.TryGetValue(transitionType, out var transState))
        {
            transState = new ShipTransitionState();
            transitions[transitionType] = transState;
        }

        if (transState.HasPrevious)
        {
            int deltaTicks = currentTicks - transState.LastTicks;
            float deltaFuel = currentFuel - transState.LastFuel;

            if (deltaTicks > 0 && deltaFuel >= 0f)
            {
                var proto = ship.Prototype;
                if (proto.FuelTankProto.HasValue)
                {
                    var ftp = proto.FuelTankProto.Value;
                    float pollutionPercent = ftp.PollutionPercent.ToFloat();
                    float mult = ShipsPollutionMultiplier * AirPollutionMultiplier;
                    
                    float pollution = deltaFuel * pollutionPercent * mult;
                    // Monthly rate: (pollution / deltaTicks) * 600f (where 600 ticks = 30 game days / 1 game month)
                    float monthlyRate = (pollution / deltaTicks) * 600f;

                    var state = GetOrCreateState(shipId, PollutionType.Ship);
                    state.CachedAveragePollution = monthlyRate;
                }
            }
        }

        transState.LastTicks = currentTicks;
        transState.LastFuel = currentFuel;
        transState.HasPrevious = true;
    }

    public void OnEntityRemoved(IEntity entity)
    {
        int id = entity.Id.Value;
        UnsubscribeFromShip(id);
        m_states.Remove(id);
        m_shipTransitions.Remove(id);
        m_shipAccumulatedFuel.Remove(id);
    }
}
