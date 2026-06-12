// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Ports;
using Mafi.Core.Products;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Factory.Sorters;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class ThroughputPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.ThroughputPatches");
    private static bool s_patched = false;

    public static void Apply(Harmony harmony)
    {
        if (s_patched) return;
        s_patched = true;

        try
        {
            var portsInterface = typeof(IEntityWithPorts);
            var receiveMethod = portsInterface.GetMethod("ReceiveAsMuchAsFromPort");
            if (receiveMethod == null)
            {
                s_log.Warning("Failed to find IEntityWithPorts.ReceiveAsMuchAsFromPort method.");
                return;
            }

            PatchReceiveMethod(harmony, typeof(Transport), receiveMethod);
            PatchReceiveMethod(harmony, typeof(Lift), receiveMethod);
            PatchReceiveMethod(harmony, typeof(Zipper), receiveMethod);
            PatchReceiveMethod(harmony, typeof(Sorter), receiveMethod);
            PatchReceiveMethod(harmony, typeof(MiniZipper), receiveMethod);
            PatchReceiveMethod(harmony, typeof(Mafi.Base.Prototypes.Sandbox.ProductsSinkEntity), receiveMethod);
            PatchReceiveMethod(harmony, typeof(Mafi.Base.Prototypes.Buildings.UniversalProductsSink), receiveMethod);

            var simUpdateInterface = typeof(IEntityWithSimUpdate);
            var simUpdateMethod = simUpdateInterface.GetMethod("SimUpdate");
            if (simUpdateMethod != null)
            {
                PatchSimUpdateMethod(harmony, typeof(Mafi.Base.Prototypes.Sandbox.ProductsSourceEntity), simUpdateMethod);
                PatchSimUpdateMethod(harmony, typeof(Mafi.Base.Prototypes.Buildings.UniversalProductsSource), simUpdateMethod);
            }

            s_log.Info("Throughput patches applied successfully.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply throughput patches: {ex}");
        }
    }

    private static void PatchSimUpdateMethod(Harmony harmony, Type type, MethodInfo interfaceMethod)
    {
        try
        {
            var map = type.GetInterfaceMap(typeof(IEntityWithSimUpdate));
            var targetMethod = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
            if (targetMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(ThroughputPatches), nameof(SourceSimUpdatePostfix));
                harmony.Patch(targetMethod, postfix: postfix);
                s_log.Info($"Patched SimUpdate on {type.Name}");
            }
            else
            {
                s_log.Warning($"Failed to find target method for {type.Name} implementing SimUpdate.");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Exception patching {type.Name} SimUpdate: {ex.Message}");
        }
    }

    public static void SourceSimUpdatePostfix(IEntity __instance)
    {
        if (__instance is Mafi.Base.Prototypes.Sandbox.ProductsSourceEntity source)
        {
            if (source.ProvidedLastTick.IsPositive)
            {
                ThroughputManager.Instance?.RecordTransfer(source.Id.Value, source.ProvidedLastTick.Value);
            }
        }
        else if (__instance is Mafi.Base.Prototypes.Buildings.UniversalProductsSource uniSource)
        {
            if (uniSource.ProvidedLastTick.IsPositive)
            {
                ThroughputManager.Instance?.RecordTransfer(uniSource.Id.Value, uniSource.ProvidedLastTick.Value);
            }
        }
    }

    private static void PatchReceiveMethod(Harmony harmony, Type type, MethodInfo interfaceMethod)
    {
        try
        {
            var map = type.GetInterfaceMap(typeof(IEntityWithPorts));
            var targetMethod = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
            if (targetMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(ThroughputPatches), nameof(ReceiveAsMuchAsFromPortPostfix));
                harmony.Patch(targetMethod, postfix: postfix);
                s_log.Info($"Patched ReceiveAsMuchAsFromPort on {type.Name}");
            }
            else
            {
                s_log.Warning($"Failed to find target method for {type.Name} implementing ReceiveAsMuchAsFromPort.");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Exception patching {type.Name}: {ex.Message}");
        }
    }

    public static void ReceiveAsMuchAsFromPortPostfix(IEntity __instance, ProductQuantity pq, Quantity __result)
    {
        // Accepted quantity = pq.Quantity - remainder (__result)
        Quantity accepted = pq.Quantity - __result;
        if (accepted.IsPositive)
        {
            ThroughputManager.Instance?.RecordTransfer(__instance.Id.Value, accepted.Value);
        }
    }
}
