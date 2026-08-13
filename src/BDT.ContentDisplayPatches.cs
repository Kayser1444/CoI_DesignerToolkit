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
using Mafi.Core.Factory.Lifts;
using Mafi.Core.Factory.Sorters;
using Mafi.Core.Factory.Zippers;
using Mafi.Core.Input;
using Mafi.Core.Ports;
using Mafi.Core.Products;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Inspectors;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.UiToolkit.Library.FloatingPanel;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class ContentDisplayPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.ContentDisplayPatches");

    public static void Apply(Harmony harmony)
    {
        try
        {
            Assembly assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            PatchSimulationMethods(harmony);

            MethodInfo? quickRemoveInvoke = typeof(EntitiesCommandsProcessor).GetMethod(
                "Invoke",
                new[] { typeof(QuickRemoveFromEntityCmd) });
            if (quickRemoveInvoke != null)
                harmony.Patch(
                    quickRemoveInvoke,
                    prefix: new HarmonyMethod(typeof(ContentDisplayPatches), nameof(QuickRemovePrefix)),
                    postfix: new HarmonyMethod(typeof(ContentDisplayPatches), nameof(QuickRemovePostfix)));
            else
                s_log.Warning("EntitiesCommandsProcessor.Invoke(QuickRemoveFromEntityCmd) not found");

            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.ZipperInspector", nameof(ZipperInspectorCtorPostfix));
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.LiftInspector", nameof(LiftInspectorCtorPostfix));
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.MiniZipperInspector", nameof(MiniZipperInspectorCtorPostfix));
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.SorterInspector", nameof(SorterInspectorCtorPostfix));
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply ContentDisplayPatches: {ex}");
        }
    }

    private static void PatchInspectorConstructor(Harmony harmony, Assembly assembly, string typeName, string postfixName)
    {
        Type? type = assembly.GetType(typeName);
        ConstructorInfo[] constructors = type?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? Array.Empty<ConstructorInfo>();
        if (constructors.Length == 0)
        {
            s_log.Warning($"{typeName} constructor not found");
            return;
        }
        harmony.Patch(constructors[0], postfix: new HarmonyMethod(typeof(ContentDisplayPatches), postfixName));
    }

    private static void PatchSimulationMethods(Harmony harmony)
    {
        Type[] supportedTypes = { typeof(Zipper), typeof(Lift), typeof(MiniZipper), typeof(Sorter) };
        foreach (Type supportedType in supportedTypes)
        {
            PatchInterfaceMethod(harmony, supportedType, typeof(IEntityWithSimUpdate), "SimUpdate", nameof(SimUpdatePrefix));
            PatchInterfaceMethod(harmony, supportedType, typeof(IEntityWithPorts), "ReceiveAsMuchAsFromPort", nameof(ReceivePrefix));
            PatchInterfaceMethod(harmony, supportedType, typeof(IEntityWithPortsEarlyExit), "CouldReceiveFromPortEarlyExit", nameof(CouldReceivePrefix));
        }
    }

    private static void PatchInterfaceMethod(
        Harmony harmony,
        Type entityType,
        Type interfaceType,
        string methodName,
        string prefixName)
    {
        if (!interfaceType.IsAssignableFrom(entityType))
            return;
        MethodInfo? interfaceMethod = interfaceType.GetMethod(methodName);
        if (interfaceMethod == null)
            return;
        InterfaceMapping map = entityType.GetInterfaceMap(interfaceType);
        int index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);
        if (index >= 0)
            harmony.Patch(map.TargetMethods[index], prefix: new HarmonyMethod(typeof(ContentDisplayPatches), prefixName));
    }

    private static void ZipperInspectorCtorPostfix(object __instance)
    {
        try
        {
            if (__instance is not BaseInspector<Zipper> inspector || GetMainBody(__instance) is not Column mainBody)
                return;

            BufferWithMultipleProductsUi bufferUi = new BufferWithMultipleProductsUi();
            PanelWithHeader panel = new PanelWithHeader();
            panel.Title(Tr.TransportedProducts);
            panel.BodyAdd(bufferUi);
            mainBody.Add(panel);
            AddRemovalControl(bufferUi, inspector.Context.InputScheduler, () => inspector.Entity, absolute: true);

            Lyst<ProductQuantity> productsCache = new Lyst<ProductQuantity>();
            Dict<ProductProto, Quantity> products = new Dict<ProductProto, Quantity>();
            inspector.Observe(() => TransportProductRemovalManager.GetBufferStateHash(inspector.Entity)).Do(delegate
            {
                Zipper zipper = inspector.Entity;
                if (zipper == null || zipper.IsDestroyed)
                    return;
                productsCache.Clear();
                TransportProductRemovalManager.AddBufferedProducts(zipper, products);
                foreach (KeyValuePair<ProductProto, Quantity> product in products)
                    productsCache.Add(product.Key.WithQuantity(product.Value));
                bufferUi.SetProducts(productsCache, TransportProductRemovalManager.GetMaxBufferSize(zipper));
            });
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error extending ZipperInspector: {ex}");
        }
    }

    private static void MiniZipperInspectorCtorPostfix(object __instance)
    {
        if (__instance is BaseInspector<MiniZipper> inspector)
            ReplaceBufferQuickControl(__instance, inspector.Context.InputScheduler, () => inspector.Entity, nameof(MiniZipper));
    }

    private static void SorterInspectorCtorPostfix(object __instance)
    {
        if (__instance is BaseInspector<Sorter> inspector)
            ReplaceBufferQuickControl(__instance, inspector.Context.InputScheduler, () => inspector.Entity, nameof(Sorter));
    }

    private static void LiftInspectorCtorPostfix(object __instance)
    {
        try
        {
            if (__instance is not BaseInspector<Lift> inspector || GetMainBody(__instance) is not UiComponent mainBody)
                return;

            Row? actionRow = null;
            foreach (Row row in FindDescendants<Row>(mainBody))
            {
                List<ButtonIcon> buttons = DirectChildren<ButtonIcon>(row);
                if (buttons.Count < 2)
                    continue;
                buttons[buttons.Count - 1].RemoveFromHierarchy();
                actionRow = row;
                break;
            }
            if (actionRow == null)
            {
                s_log.Warning("Lift inspector action row was not found; combined removal control was not installed.");
                return;
            }
            AddRemovalControl(actionRow, inspector.Context.InputScheduler, () => inspector.Entity, absolute: false);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error extending LiftInspector: {ex}");
        }
    }

    private static void ReplaceBufferQuickControl<T>(
        object inspector,
        IInputScheduler scheduler,
        Func<T> entityProvider,
        string adapterName)
        where T : class, IEntity
    {
        try
        {
            if (GetMainBody(inspector) is not UiComponent mainBody)
                return;
            BufferWithMultipleProductsUi? buffer = null;
            foreach (BufferWithMultipleProductsUi candidate in FindDescendants<BufferWithMultipleProductsUi>(mainBody))
            {
                buffer = candidate;
                break;
            }
            if (buffer == null)
            {
                s_log.Warning($"{adapterName} product buffer UI was not found; combined removal control was not installed.");
                return;
            }
            foreach (ButtonIcon button in DirectChildren<ButtonIcon>(buffer))
                button.RemoveFromHierarchy();
            AddRemovalControl(buffer, scheduler, () => entityProvider(), absolute: true);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error extending {adapterName} inspector: {ex}");
        }
    }

    public static void AddRemovalControl(
        UiComponent parent,
        IInputScheduler scheduler,
        Func<IEntity?> entityProvider,
        bool absolute)
    {
        Label removalLabel = new Label();
        ButtonIcon removalButton = new ButtonIcon(Button.Danger, "Assets/Unity/UserInterface/General/Trash128.png");
        if (absolute)
            removalButton.AbsolutePosition(right: 4, top: 4);
        else
            removalButton.Medium();
        removalButton.Toggleable().OnClick((Action)delegate
        {
            IEntity? entity = entityProvider();
            if (entity != null)
                scheduler.ScheduleInputCmd(new TransportProductRemovalCmd(entity.Id));
        }, false);
        parent.Add(removalButton);

        ButtonTextUpoints quickRemoveButton = new ButtonTextUpoints(
            Tr.QuickRemove__Action,
            "Assets/Unity/UserInterface/General/Trash128.png")
            .Compact()
            .Tooltip(Tr.QuickRemove__Action)
            .OnClick((Action)delegate
            {
                IEntity? entity = entityProvider();
                if (entity != null)
                    scheduler.ScheduleInputCmd(new QuickRemoveFromEntityCmd(entity.Id));
            }, false);
        removalButton.FloaterInteractive(new Column(2.pt()) { removalLabel, quickRemoveButton });

        parent.Observe(delegate
        {
            IEntity? entity = entityProvider();
            return entity != null && TransportProductRemovalManager.IsRegularRemovalActive(entity);
        }).Observe(delegate
        {
            IEntity? entity = entityProvider();
            return entity != null && TransportProductRemovalManager.HasBufferedProducts(entity);
        }).Do(delegate(bool isRemoving, bool hasProducts)
        {
            removalLabel.Value(isRemoving ? Tr.RemoveProducts__Stop : Tr.RemoveProducts__Tooltip);
            removalButton.Selected(isRemoving);
            removalButton.Enabled(isRemoving || hasProducts);
        });

        parent.Observe(delegate
        {
            IEntity? entity = entityProvider();
            if (entity == null || entity.IsDestroyed)
                return Make.Kvp(Upoints.Zero, false);
            Upoints cost = TransportProductRemovalManager.GetQuickRemoveCost(entity, out bool canAfford);
            return Make.Kvp(cost, canAfford);
        }).Do(delegate(KeyValuePair<Upoints, bool> result)
        {
            quickRemoveButton.SetCost(result.Key);
            quickRemoveButton.Visible(result.Key.IsPositive);
            quickRemoveButton.Enabled(result.Value);
        });
    }

    private static UiComponent? GetMainBody(object inspector)
    {
        Type? type = inspector.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(inspector) is UiComponent component)
                return component;
            type = type.BaseType;
        }
        return null;
    }

    private static List<T> DirectChildren<T>(UiComponent parent) where T : UiComponent
    {
        List<T> result = new List<T>();
        foreach (UiComponent child in parent)
        {
            if (child is T match)
                result.Add(match);
        }
        return result;
    }

    private static IEnumerable<T> FindDescendants<T>(UiComponent parent) where T : UiComponent
    {
        foreach (UiComponent child in parent)
        {
            if (child is T match)
                yield return match;
            foreach (T descendant in FindDescendants<T>(child))
                yield return descendant;
        }
    }

    public static bool QuickRemovePrefix(QuickRemoveFromEntityCmd cmd, EntitiesManager ___m_entitiesManager, out bool __state)
    {
        __state = false;
        try
        {
            if (___m_entitiesManager.TryGetEntity<IEntity>(cmd.EntityId, out IEntity entity) &&
                TransportProductRemovalManager.SupportsEntity(entity))
            {
                if (entity is Zipper || TransportProductRemovalManager.IsExternalAdapter(entity))
                {
                    TransportProductRemovalManager.QuickRemove(entity);
                    cmd.SetResultSuccess(entity.Id);
                    return false;
                }

                if (TransportProductRemovalManager.IsRegularRemovalActive(entity))
                {
                    Upoints cost = TransportProductRemovalManager.GetQuickRemoveCost(entity, out bool canAfford);
                    __state = cost.IsPositive && canAfford;
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Error handling quick product removal: {ex}");
        }
        return true;
    }

    public static void QuickRemovePostfix(
        QuickRemoveFromEntityCmd cmd,
        EntitiesManager ___m_entitiesManager,
        bool __state)
    {
        if (!__state)
            return;

        try
        {
            if (___m_entitiesManager.TryGetEntity<IEntity>(cmd.EntityId, out IEntity entity))
                TransportProductRemovalManager.Cancel(entity.Id);
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to clean up regular removal after successful quick removal: {ex}");
        }
    }

    public static bool SimUpdatePrefix(IEntity __instance)
    {
        bool isActive = TransportProductRemovalManager.IsRegularRemovalActive(__instance);
        if (isActive && __instance is Lift lift)
            lift.AnimationStatesProvider.Pause();
        return !isActive;
    }

    public static bool ReceivePrefix(IEntity __instance, ProductQuantity pq, ref Quantity __result)
    {
        if (!TransportProductRemovalManager.IsRegularRemovalActive(__instance))
            return true;
        __result = pq.Quantity;
        return false;
    }

    public static bool CouldReceivePrefix(IEntity __instance, ref bool __result)
    {
        if (!TransportProductRemovalManager.IsRegularRemovalActive(__instance))
            return true;
        __result = false;
        return false;
    }
}
