// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Trains;
using Mafi.Core.Vehicles;
using Mafi.Core.Buildings.Cargo.Ships;
using Mafi.Core.Products;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class PollutionPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.PollutionPatches");

    [ThreadStatic]
    public static Machine? CurrentExecutingMachine;

    [ThreadStatic]
    private static bool s_isRecordingFuelConsumption;

    private static readonly Dictionary<FuelTank, IEntity> s_fuelTankToEntity = new Dictionary<FuelTank, IEntity>();

    public static void Apply(Harmony harmony)
    {
        try
        {
            var tryPushFinishedRecipeToBuffers = typeof(Machine).GetMethod(
                "tryPushFinishedRecipeToBuffers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (tryPushFinishedRecipeToBuffers != null)
            {
                harmony.Patch(tryPushFinishedRecipeToBuffers,
                    prefix: new HarmonyMethod(typeof(Machine_tryPushFinishedRecipeToBuffers_Patch), nameof(Machine_tryPushFinishedRecipeToBuffers_Patch.Prefix)),
                    finalizer: new HarmonyMethod(typeof(Machine_tryPushFinishedRecipeToBuffers_Patch), nameof(Machine_tryPushFinishedRecipeToBuffers_Patch.Finalizer)));
                s_log.Info("Patched Machine.tryPushFinishedRecipeToBuffers.");
            }
            else
            {
                s_log.Warning("Machine.tryPushFinishedRecipeToBuffers not found!");
            }

            var storeAsMuchAs = typeof(ProductBuffer).GetMethod(
                nameof(ProductBuffer.StoreAsMuchAs),
                new[] { typeof(Quantity) });
            if (storeAsMuchAs != null)
            {
                harmony.Patch(storeAsMuchAs,
                    postfix: new HarmonyMethod(typeof(ProductBuffer_StoreAsMuchAs_Patch), nameof(ProductBuffer_StoreAsMuchAs_Patch.Postfix)));
                s_log.Info("Patched ProductBuffer.StoreAsMuchAs.");
            }
            else
            {
                s_log.Warning("ProductBuffer.StoreAsMuchAs not found!");
            }

            var consumeFuelPerUpdatePercent = typeof(FuelTank).GetMethod(
                nameof(FuelTank.ConsumeFuelPerUpdate),
                new[] { typeof(Percent) });
            if (consumeFuelPerUpdatePercent != null)
            {
                harmony.Patch(consumeFuelPerUpdatePercent,
                    prefix: new HarmonyMethod(typeof(FuelTank_ConsumeFuelPerUpdatePercent_Patch), nameof(FuelTank_ConsumeFuelPerUpdatePercent_Patch.Prefix)));
                s_log.Info("Patched FuelTank.ConsumeFuelPerUpdate(Percent).");
            }
            else
            {
                s_log.Warning("FuelTank.ConsumeFuelPerUpdate(Percent) not found!");
            }

            var consumeFuelPerUpdateEnum = typeof(FuelTank).GetMethod(
                nameof(FuelTank.ConsumeFuelPerUpdate),
                new[] { typeof(VehicleFuelConsumption) });
            if (consumeFuelPerUpdateEnum != null)
            {
                harmony.Patch(consumeFuelPerUpdateEnum,
                    prefix: new HarmonyMethod(typeof(FuelTank_ConsumeFuelPerUpdateEnum_Patch), nameof(FuelTank_ConsumeFuelPerUpdateEnum_Patch.Prefix)));
                s_log.Info("Patched FuelTank.ConsumeFuelPerUpdate(VehicleFuelConsumption).");
            }
            else
            {
                s_log.Warning("FuelTank.ConsumeFuelPerUpdate(VehicleFuelConsumption) not found!");
            }

            var consumeFuelShip = typeof(CargoShipV2).GetMethod(
                "ConsumeFuel",
                BindingFlags.Instance | BindingFlags.Public);
            if (consumeFuelShip != null)
            {
                harmony.Patch(consumeFuelShip,
                    prefix: new HarmonyMethod(typeof(CargoShipV2_ConsumeFuel_Patch), nameof(CargoShipV2_ConsumeFuel_Patch.Prefix)));
                s_log.Info("Patched CargoShipV2.ConsumeFuel.");
            }
            else
            {
                s_log.Warning("CargoShipV2.ConsumeFuel not found!");
            }

            s_log.Info("Pollution patches applied successfully.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply pollution patches: {ex}");
        }
    }

    public static IEntity? GetEntityForFuelTank(FuelTank tank, IEntitiesManager em)
    {
        if (s_fuelTankToEntity.TryGetValue(tank, out var entity) && !entity.IsDestroyed)
        {
            return entity;
        }

        // Refresh mapping
        s_fuelTankToEntity.Clear();
        foreach (var v in em.GetAllEntitiesOfType<Vehicle>())
        {
            var t = v.m_fuelTank.ValueOrNull;
            if (t != null)
            {
                s_fuelTankToEntity[t] = v;
            }
        }
        foreach (var l in em.GetAllEntitiesOfType<Locomotive>())
        {
            var t = l.m_fuelTank.ValueOrNull;
            if (t != null)
            {
                s_fuelTankToEntity[t] = l;
            }
        }

        if (s_fuelTankToEntity.TryGetValue(tank, out entity) && !entity.IsDestroyed)
        {
            return entity;
        }
        return null;
    }

    public static class Machine_tryPushFinishedRecipeToBuffers_Patch
    {
        public static void Prefix(Machine __instance)
        {
            if (DesignerToolkitSettings.PollutionDaysToAverage > 0)
            {
                CurrentExecutingMachine = __instance;
            }
        }

        public static void Finalizer()
        {
            CurrentExecutingMachine = null;
        }
    }

    public static class ProductBuffer_StoreAsMuchAs_Patch
    {
        public static void Postfix(ProductBuffer __instance, Quantity quantity, Quantity __result)
        {
            if (CurrentExecutingMachine != null && DesignerToolkitSettings.PollutionDaysToAverage > 0)
            {
                var product = __instance.Product;
                if (product.Id == IdsCore.Products.PollutedAir)
                {
                    Quantity accepted = quantity - __result;
                    if (accepted.IsPositive)
                    {
                        float mult = PollutionManager.Instance?.AirPollutionMultiplier ?? 1f;
                        PollutionManager.Instance?.RecordPollution(CurrentExecutingMachine.Id.Value, accepted.Value * mult, PollutionManager.PollutionType.Air);
                    }
                }
                else if (product.Id == IdsCore.Products.PollutedWater)
                {
                    Quantity accepted = quantity - __result;
                    if (accepted.IsPositive)
                    {
                        float mult = PollutionManager.Instance?.WaterPollutionMultiplier ?? 1f;
                        PollutionManager.Instance?.RecordPollution(CurrentExecutingMachine.Id.Value, accepted.Value * mult, PollutionManager.PollutionType.Ground);
                    }
                }
            }
        }
    }

    private static void RecordFuelPollution(FuelTank tank, Percent consumptionPercent)
    {
        if (s_isRecordingFuelConsumption) return;
        s_isRecordingFuelConsumption = true;
        try
        {
            if (DesignerToolkitSettings.PollutionDaysToAverage > 0 && PollutionManager.Instance != null)
            {
                if (tank.m_fuelConsumptionDisabled.Value) return;

                var em = PollutionManager.Instance.EntitiesManager;
                if (em == null) return;

                var entity = GetEntityForFuelTank(tank, em);
                if (entity != null)
                {
                    var proto = tank.Proto;
                    float capacity = proto.Capacity.Value;
                    float duration = proto.Duration.Ticks;
                    float pollutionPercent = proto.PollutionPercent.ToFloat();

                    float multiplier = tank.m_fuelConsumptionMultiplier.Value.ToFloat();
                    float consumedTicks = consumptionPercent.ToFloat() * multiplier;

                    float mult = 1f;
                    if (entity is Vehicle)
                    {
                        mult = PollutionManager.Instance.VehiclesPollutionMultiplier * PollutionManager.Instance.AirPollutionMultiplier;
                    }
                    else if (entity is Locomotive)
                    {
                        mult = PollutionManager.Instance.TrainsPollutionMultiplier * PollutionManager.Instance.AirPollutionMultiplier;
                    }

                    // pollution = (consumedTicks / duration) * capacity * pollutionPercent * mult
                    float pollution = (consumedTicks / duration) * capacity * pollutionPercent * mult;

                    PollutionManager.Instance.RecordPollution(entity.Id.Value, pollution, PollutionManager.PollutionType.Vehicle);
                }
            }
        }
        finally
        {
            s_isRecordingFuelConsumption = false;
        }
    }

    public static class FuelTank_ConsumeFuelPerUpdatePercent_Patch
    {
        public static void Prefix(FuelTank __instance, Percent consumptionPercent)
        {
            RecordFuelPollution(__instance, consumptionPercent);
        }
    }

    public static class FuelTank_ConsumeFuelPerUpdateEnum_Patch
    {
        public static void Prefix(FuelTank __instance, VehicleFuelConsumption consumption)
        {
            Percent consumptionPercent = (consumption != VehicleFuelConsumption.Idle) ? Percent.Hundred : __instance.Proto.IdleFuelConsumption;
            RecordFuelPollution(__instance, consumptionPercent);
        }
    }

    public static class CargoShipV2_ConsumeFuel_Patch
    {
        public static void Prefix(CargoShipV2 __instance, Quantity toConsume)
        {
            if (toConsume.IsPositive && PollutionManager.Instance != null)
            {
                PollutionManager.Instance.RecordShipFuel(__instance.Id.Value, toConsume.Value);
            }
        }
    }
}
