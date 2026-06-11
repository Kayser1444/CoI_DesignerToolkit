// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Base.Prototypes.Sandbox;
using Mafi.Core.Entities;
using Mafi.Core.Ports;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Ports.Io;
using Mafi.Core.Products;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class RateLimitPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.RateLimitPatches");
    private static bool s_patched = false;

    private static MethodInfo? s_productsManagerProductCreated;
    private static MethodInfo? s_singleCounterReportValue;
    private static FieldInfo? s_tpCounterSource;

    public static void Apply(Harmony harmony)
    {
        if (s_patched) return;
        s_patched = true;

        try
        {
            s_productsManagerProductCreated = typeof(IProductsManager).GetMethod("ProductCreated", 
                BindingFlags.Instance | BindingFlags.Public, 
                null, 
                new Type[] { typeof(ProductProto), typeof(Quantity), typeof(CreateReason) }, 
                null);
            var singleCounterType = typeof(ProductsSourceEntity).Assembly.GetType("Mafi.Core.Simulation.SingleCounter");
            if (singleCounterType != null)
                s_singleCounterReportValue = singleCounterType.GetMethod("ReportValue", BindingFlags.Instance | BindingFlags.Public);
            
            s_tpCounterSource = typeof(ProductsSourceEntity).GetField("m_tpCounter", BindingFlags.Instance | BindingFlags.NonPublic);

            var simUpdateInterface = typeof(IEntityWithSimUpdate);
            var simUpdateMethod = simUpdateInterface.GetMethod("SimUpdate");

            var portsInterface = typeof(IEntityWithPorts);
            var receiveMethod = portsInterface.GetMethod("ReceiveAsMuchAsFromPort");

            var sourceMap = typeof(ProductsSourceEntity).GetInterfaceMap(simUpdateInterface);
            var methodSourceUpdate = sourceMap.TargetMethods[Array.IndexOf(sourceMap.InterfaceMethods, simUpdateMethod)];
            if (methodSourceUpdate != null)
                harmony.Patch(methodSourceUpdate, prefix: new HarmonyMethod(typeof(RateLimitPatches), nameof(SourceSimUpdatePrefix)));

            var sinkMap = typeof(ProductsSinkEntity).GetInterfaceMap(portsInterface);
            var methodSinkReceive = sinkMap.TargetMethods[Array.IndexOf(sinkMap.InterfaceMethods, receiveMethod)];
            if (methodSinkReceive != null)
                harmony.Patch(methodSinkReceive, prefix: new HarmonyMethod(typeof(RateLimitPatches), nameof(SinkReceivePrefix)));

            var transportMap = typeof(Transport).GetInterfaceMap(portsInterface);
            var methodTransportReceive = transportMap.TargetMethods[Array.IndexOf(transportMap.InterfaceMethods, receiveMethod)];
            if (methodTransportReceive != null)
                harmony.Patch(methodTransportReceive, prefix: new HarmonyMethod(typeof(RateLimitPatches), nameof(TransportReceivePrefix)), postfix: new HarmonyMethod(typeof(RateLimitPatches), nameof(TransportReceivePostfix)));
            
            s_log.Info("Rate limit patches applied.");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply rate limit patches: {ex}");
        }
    }



    public static bool SourceSimUpdatePrefix(ProductsSourceEntity __instance)
    {
        var limitOpt = RateLimitManager.GetLimit(__instance.Id);
        if (!limitOpt.HasValue) return true; // Original behavior

        try
        {
            // We duplicate the logic to avoid transpiling, applying the limit to generation.
            Quantity generated = Quantity.Zero;
            var providedLastTickProp = typeof(ProductsSourceEntity).GetProperty("ProvidedLastTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            providedLastTickProp?.SetValue(__instance, generated);

            if (!__instance.IsAllowedToCheat || __instance.IsNotEnabled || __instance.ProvidedProduct.IsNone)
            {
                return false;
            }

            int toGenerate = RateLimitManager.ConsumeTokens(__instance, IoPort.MAX_TRANSFER_PER_TICK.Value);
            if (toGenerate <= 0) return false;

            var enumerator = __instance.ConnectedOutputPorts.GetEnumerator();
            while (enumerator.MoveNext())
            {
                IoPortData current = enumerator.Current;
                if (current.AllowedProductType == __instance.ProvidedProduct.Value.Type)
                {
                    generated += new Quantity(toGenerate) - current.SendAsMuchAs(new Quantity(toGenerate).Of(__instance.ProvidedProduct.Value));
                }
            }
            providedLastTickProp?.SetValue(__instance, generated);
            
            if (s_productsManagerProductCreated != null)
                s_productsManagerProductCreated.Invoke(__instance.Context.ProductsManager, new object[] { __instance.ProvidedProduct.Value, generated, CreateReason.Cheated });

            if (s_tpCounterSource != null && s_singleCounterReportValue != null)
            {
                var tpCounter = s_tpCounterSource.GetValue(__instance);
                if (tpCounter != null)
                    s_singleCounterReportValue.Invoke(tpCounter, new object[] { (ushort)generated.Value });
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"SourceSimUpdatePrefix EXCEPTION: {ex}");
        }

        return false; // Skip original
    }

    public static bool SinkReceivePrefix(ProductsSinkEntity __instance, ref ProductQuantity pq, IoPortToken sourcePort, ref Quantity __result)
    {
        var limitOpt = RateLimitManager.GetLimit(__instance.Id);
        if (!limitOpt.HasValue) return true;

        if (!__instance.IsAllowedToCheat || __instance.IsNotEnabled)
        {
            __result = pq.Quantity;
            return false;
        }

        int allowed = RateLimitManager.ConsumeTokens(__instance, pq.Quantity.Value);
        if (allowed <= 0)
        {
            __result = pq.Quantity; // None consumed, return original amount
            return false;
        }

        var toConsume = new ProductQuantity(pq.Product, new Quantity(allowed));
        __instance.Context.ProductsManager.ProductDestroyed(toConsume, DestroyReason.Cheated);

        var consumedLastTickProp = typeof(ProductsSinkEntity).GetProperty("ConsumedLastTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (consumedLastTickProp != null)
        {
            Quantity current = (Quantity)consumedLastTickProp.GetValue(__instance);
            consumedLastTickProp.SetValue(__instance, current + toConsume.Quantity);
        }

        __result = pq.Quantity - toConsume.Quantity; // Return the remainder
        return false; // Skip original
    }

    public static void TransportReceivePrefix(Transport __instance, ref ProductQuantity pq, out Quantity __state)
    {
        var limitOpt = RateLimitManager.GetLimit(__instance.Id);
        if (!limitOpt.HasValue) 
        {
            __state = Quantity.Zero;
            return;
        }

        int allowed = RateLimitManager.ConsumeTokens(__instance, pq.Quantity.Value);
        __state = pq.Quantity - new Quantity(allowed);
        pq = new ProductQuantity(pq.Product, new Quantity(allowed));
    }

    public static void TransportReceivePostfix(ref Quantity __result, Quantity __state)
    {
        __result += __state;
    }
}
