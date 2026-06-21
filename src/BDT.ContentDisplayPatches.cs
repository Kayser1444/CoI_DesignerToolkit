// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Input;
using Mafi.Core.Products;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Inspectors;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class ContentDisplayPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.ContentDisplayPatches");

    private static readonly FieldInfo s_inputBufferField = typeof(Zipper).GetField("m_inputBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo s_outputBufferField = typeof(Zipper).GetField("m_outputBuffer", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Apply(Harmony harmony)
    {
        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var type = assembly.GetType("Mafi.Unity.Ui.Inspectors.ZipperInspector");
            if (type == null)
            {
                s_log.Warning("ZipperInspector type not found");
                return;
            }

            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length > 0)
            {
                harmony.Patch(ctors[0], postfix: new HarmonyMethod(typeof(ContentDisplayPatches), nameof(ZipperInspectorCtorPostfix)));
                s_log.Info("Patched constructor for ZipperInspector");
            }

            // Patch EntitiesCommandsProcessor to intercept QuickRemoveFromEntityCmd for Zippers
            var processorType = typeof(EntitiesCommandsProcessor);
            var invokeMethod = processorType.GetMethod("Invoke", new Type[] { typeof(QuickRemoveFromEntityCmd) });
            if (invokeMethod != null)
            {
                harmony.Patch(invokeMethod, prefix: new HarmonyMethod(typeof(ContentDisplayPatches), nameof(QuickRemovePrefix)));
                s_log.Info("Patched EntitiesCommandsProcessor.Invoke(QuickRemoveFromEntityCmd) prefix");
            }
            else
            {
                s_log.Warning("EntitiesCommandsProcessor.Invoke(QuickRemoveFromEntityCmd) not found");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply ContentDisplayPatches: {ex.Message}");
        }
    }

    private static void ZipperInspectorCtorPostfix(object __instance)
    {
        try
        {
            var zipperInspector = __instance as BaseInspector<Zipper>;
            if (zipperInspector == null) return;

            var inspectorType = __instance.GetType();
            FieldInfo? mainBodyField = null;
            var searchType = inspectorType;
            while (searchType != null && mainBodyField == null)
            {
                mainBodyField = searchType.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                searchType = searchType.BaseType;
            }

            if (mainBodyField == null) return;

            var mainBody = mainBodyField.GetValue(__instance) as Column;
            if (mainBody != null)
            {
                var bufferUi = new BufferWithMultipleProductsUi();
                var panel = new PanelWithHeader();
                panel.Title(Tr.TransportedProducts);
                panel.BodyAdd(bufferUi);

                mainBody.Add(panel);

                // Add the custom Quick Remove button
                AddQuickRemoveButtonForZipper(bufferUi, zipperInspector.Context.InputScheduler, () => zipperInspector.Entity);

                var productsCache = new Lyst<ProductQuantity>();
                var dict = new Dict<ProductProto, Quantity>();

                zipperInspector.Observe(() => GetBufferStateHash(zipperInspector.Entity))
                    .Do(delegate
                    {
                        var zipper = zipperInspector.Entity;
                        if (zipper == null || zipper.IsDestroyed) return;

                        productsCache.Clear();
                        dict.Clear();

                        if (s_inputBufferField != null)
                        {
                            var inputBuf = s_inputBufferField.GetValue(zipper) as ProductQuantity[];
                            if (inputBuf != null)
                            {
                                for (int i = 0; i < inputBuf.Length; i++)
                                {
                                    var pq = inputBuf[i];
                                    if (pq.IsNotEmpty)
                                    {
                                        if (dict.TryGetValue(pq.Product, out var existingQty))
                                        {
                                            dict[pq.Product] = existingQty + pq.Quantity;
                                        }
                                        else
                                        {
                                            dict.Add(pq.Product, pq.Quantity);
                                        }
                                    }
                                }
                            }
                        }

                        if (s_outputBufferField != null)
                        {
                            var outputBuf = s_outputBufferField.GetValue(zipper) as Queueue<ZipBuffProduct>;
                            if (outputBuf != null)
                            {
                                var enumerator = outputBuf.GetEnumerator();
                                while (enumerator.MoveNext())
                                {
                                    var item = enumerator.Current;
                                    if (item.ProductQuantity.IsNotEmpty)
                                    {
                                        if (dict.TryGetValue(item.ProductQuantity.Product, out var existingQty))
                                        {
                                            dict[item.ProductQuantity.Product] = existingQty + item.ProductQuantity.Quantity;
                                        }
                                        else
                                        {
                                            dict.Add(item.ProductQuantity.Product, item.ProductQuantity.Quantity);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var kvp in dict)
                        {
                            productsCache.Add(kvp.Key.WithQuantity(kvp.Value));
                        }

                        bufferUi.SetProducts(productsCache, zipper.MaxBufferSize);
                    });
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in ZipperInspectorCtorPostfix: {ex}");
        }
    }

    private static void AddQuickRemoveButtonForZipper(BufferWithMultipleProductsUi buffer, IInputScheduler scheduler, Func<Zipper> zipperProvider)
    {
        ButtonIcon quickRemoveBtn;
        UpointsButtonFloater quickRemoveBtnFloater;

        buffer.Add(quickRemoveBtn = new ButtonIcon(Button.Unity, "Assets/Unity/UserInterface/General/Trash128.png")
            .AbsolutePosition(right: 4, top: 4)
            .OnClick((Action)delegate
            {
                var zipper = zipperProvider();
                if (zipper != null)
                {
                    scheduler.ScheduleInputCmd(new QuickRemoveFromEntityCmd(zipper.Id));
                }
            }, false));

        quickRemoveBtnFloater = quickRemoveBtn.AttachUpointsFloater().Title(Tr.QuickRemove__Action);

        buffer.Observe(delegate
        {
            var zipper = zipperProvider();
            if (zipper == null || zipper.IsDestroyed) return Make.Kvp(Upoints.Zero, false);
            bool canAfford;
            Upoints quickRemoveCost = GetZipperQuickRemoveCost(zipper, out canAfford);
            return Make.Kvp(quickRemoveCost, canAfford);
        }).Do(delegate(KeyValuePair<Upoints, bool> result)
        {
            Upoints key = result.Key;
            if (key.IsPositive)
            {
                quickRemoveBtnFloater.Cost(key);
            }
            quickRemoveBtn.Visible(key.IsPositive);
            quickRemoveBtn.Enabled(result.Value);
        });
    }

    private static Upoints GetZipperQuickRemoveCost(Zipper zipper, out bool canAfford)
    {
        canAfford = false;
        Quantity totalQty = Quantity.Zero;

        if (s_inputBufferField != null)
        {
            var inputBuf = s_inputBufferField.GetValue(zipper) as ProductQuantity[];
            if (inputBuf != null)
            {
                for (int i = 0; i < inputBuf.Length; i++)
                {
                    totalQty += inputBuf[i].Quantity;
                }
            }
        }

        if (s_outputBufferField != null)
        {
            var outputBuf = s_outputBufferField.GetValue(zipper) as Queueue<ZipBuffProduct>;
            if (outputBuf != null)
            {
                var enumerator = outputBuf.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    totalQty += enumerator.Current.ProductQuantity.Quantity;
                }
            }
        }

        var upointsManager = zipper.Context.UpointsManager;
        Upoints cost = QuickDeliverCostHelper.QuantityToUnityCost(totalQty.Value, upointsManager.QuickActionCostMultiplier, applyDiscount: true) ?? Upoints.Zero;
        canAfford = upointsManager.CanConsume(cost);
        return cost;
    }

    private static void ClearZipperProducts(Zipper zipper)
    {
        if (zipper == null || zipper.IsDestroyed) return;

        var assetTransactionManager = zipper.Context.AssetTransactionManager;

        // Clear input buffer
        if (s_inputBufferField != null)
        {
            var inputBuf = s_inputBufferField.GetValue(zipper) as ProductQuantity[];
            if (inputBuf != null)
            {
                for (int i = 0; i < inputBuf.Length; i++)
                {
                    var pq = inputBuf[i];
                    if (pq.IsNotEmpty)
                    {
                        assetTransactionManager.StoreClearedProduct(pq);
                        inputBuf[i] = ProductQuantity.None;
                    }
                }
            }
        }

        SetBackingField(zipper, "QuantityInInputBuffer", Quantity.Zero);

        // Clear output buffer
        if (s_outputBufferField != null)
        {
            var outputBuf = s_outputBufferField.GetValue(zipper) as Queueue<ZipBuffProduct>;
            if (outputBuf != null)
            {
                var enumerator = outputBuf.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    if (item.ProductQuantity.IsNotEmpty)
                    {
                        assetTransactionManager.StoreClearedProduct(item.ProductQuantity);
                    }
                }
                outputBuf.Clear();
            }
        }

        SetBackingField(zipper, "QuantityInOutputBuffer", Quantity.Zero);
    }

    private static void SetBackingField(object obj, string propertyName, object value)
    {
        var field = obj.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            s_log.Warning($"Backing field for property {propertyName} not found");
        }
    }

    private static int GetBufferStateHash(Zipper? zipper)
    {
        if (zipper == null || zipper.IsDestroyed) return 0;

        int hash = 17;
        hash = hash * 31 + zipper.TotalQuantityInBuffers.Value.GetHashCode();
        hash = hash * 31 + zipper.MaxBufferSize.Value.GetHashCode();

        if (s_inputBufferField != null)
        {
            var inputBuf = s_inputBufferField.GetValue(zipper) as ProductQuantity[];
            if (inputBuf != null)
            {
                for (int i = 0; i < inputBuf.Length; i++)
                {
                    var pq = inputBuf[i];
                    if (pq.IsNotEmpty)
                    {
                        hash = hash * 31 + pq.Product.Id.Value.GetHashCode();
                        hash = hash * 31 + pq.Quantity.Value.GetHashCode();
                    }
                }
            }
        }

        if (s_outputBufferField != null)
        {
            var outputBuf = s_outputBufferField.GetValue(zipper) as Queueue<ZipBuffProduct>;
            if (outputBuf != null)
            {
                var enumerator = outputBuf.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    if (item.ProductQuantity.IsNotEmpty)
                    {
                        hash = hash * 31 + item.ProductQuantity.Product.Id.Value.GetHashCode();
                        hash = hash * 31 + item.ProductQuantity.Quantity.Value.GetHashCode();
                    }
                }
            }
        }

        return hash;
    }

    public static bool QuickRemovePrefix(EntitiesCommandsProcessor __instance, QuickRemoveFromEntityCmd cmd, EntitiesManager ___m_entitiesManager)
    {
        try
        {
            if (___m_entitiesManager.TryGetEntity<Zipper>(cmd.EntityId, out var zipper))
            {
                bool canAfford;
                Upoints cost = GetZipperQuickRemoveCost(zipper, out canAfford);
                if (!cost.IsNotPositive && canAfford)
                {
                    zipper.Context.UpointsManager.ConsumeExactly(IdsCore.UpointsCategories.QuickRemove, cost);
                    ClearZipperProducts(zipper);
                }
                cmd.SetResultSuccess(zipper.Id);
                return false; // skip original method
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error in QuickRemovePrefix: {ex}");
        }
        return true;
    }
}
