// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.

using System;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Research;
using Mafi.Unity.Ui.Blueprints;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui.Controllers.LayoutEntityPlacing;
using Mafi.Unity.Ui.Controllers.Tools;
using Mafi.Unity.UiToolkit.Component;

namespace CoIDesignerToolkit;

/// <summary>
/// Resolves game API shapes that differ between supported CoI 0.8.5 and 0.8.6.
/// All calls stay reflection-based so loading the mod does not require either
/// version-specific member set to exist.
///
/// Compatibility horizon: the old member shape is used by the minimum supported
/// Update 4.1 release, 0.8.5, so raising the floor from pre-4.1 versions does not
/// make this adapter removable.
/// TODO(COI-DROP-4.1): Replace this adapter with direct 0.8.6+ calls only when
/// Update 4.1 itself is no longer supported.
/// </summary>
internal static class GameApiCompat
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static object GetBlueprintLibraryHost(BlueprintsWindow window)
        => GetMemberValue(window, "m_libraryTab") ?? window;

    internal static BlueprintsLibrary GetBlueprintsLibrary(object host)
    {
        object? value = GetMemberValue(host, "BlueprintsLibrary");
        if (value is BlueprintsLibrary library)
            return library;

        object? window = GetMemberValue(host, "m_window");
        value = window == null ? null : GetMemberValue(window, "BlueprintsLibrary");
        return value as BlueprintsLibrary
            ?? throw new MissingMemberException(host.GetType().FullName, "BlueprintsLibrary");
    }

    internal static IBlueprintsFolder GetCurrentFolder(object host)
        => GetMemberValue(host, "CurrentFolder") as IBlueprintsFolder
            ?? throw new MissingMemberException(host.GetType().FullName, "CurrentFolder");

    internal static UiComponent GetPlacementPanelFirstChild(BlueprintsWindow window)
    {
        object host = GetBlueprintLibraryHost(window);
        UiComponent panel = GetMemberValue(host, "m_placementPanel") as UiComponent
            ?? throw new MissingMemberException(host.GetType().FullName, "m_placementPanel");
        return panel[0];
    }

    internal static bool TryGetSelectedBlueprint(BlueprintsWindow window, out IBlueprint blueprint)
    {
        object host = GetBlueprintLibraryHost(window);
        object? option = GetMemberValue(host, "m_selectedItem");
        object? tile = GetOptionValue(option);
        IBlueprint? selected = GetMemberValue(tile, "Blueprint") as IBlueprint;
        blueprint = selected!;
        return selected != null;
    }

    internal static object? GetSelectedBlueprintTile(object host)
        => GetOptionValue(GetMemberValue(host, "m_selectedItem"));

    internal static void SetNewBlueprintItem(BlueprintsWindow window, Option<IBlueprintItem> item)
    {
        object host = GetBlueprintLibraryHost(window);
        SetMemberValue(host, "m_newItem", item);
    }

    internal static void ActivateBlueprintController(BlueprintsWindow window)
    {
        object host = GetBlueprintLibraryHost(window);
        object controller = GetMemberValue(host, "m_controller")
            ?? throw new MissingMemberException(host.GetType().FullName, "m_controller");
        MethodInfo method = controller.GetType().GetMethod("ActivateSelf", InstanceFlags)
            ?? throw new MissingMethodException(controller.GetType().FullName, "ActivateSelf");
        method.Invoke(controller, null);
    }

    internal static BlueprintsWindow GetBlueprintsWindow(object detail)
    {
        if (GetMemberValue(detail, "Window") is BlueprintsWindow oldWindow)
            return oldWindow;

        object tab = GetMemberValue(detail, "Tab")
            ?? throw new MissingMemberException(detail.GetType().FullName, "Tab");
        return GetMemberValue(tab, "m_window") as BlueprintsWindow
            ?? throw new MissingMemberException(tab.GetType().FullName, "m_window");
    }

    internal static BlueprintsWindow.Controller GetBlueprintController(BlueprintsWindow window)
    {
        object host = GetBlueprintLibraryHost(window);
        return GetMemberValue(host, "m_controller") as BlueprintsWindow.Controller
            ?? throw new MissingMemberException(host.GetType().FullName, "m_controller");
    }

    internal static UnlockedProtosDbForUi GetUnlockedProtosDb(BlueprintsWindow window)
    {
        object? value = GetMemberValue(window, "m_unlockedProtosDb");
        if (value is UnlockedProtosDbForUi oldDb)
            return oldDb;

        object controller = GetBlueprintController(window);
        return GetMemberValue(controller, "m_unlockedProtosDb") as UnlockedProtosDbForUi
            ?? throw new MissingMemberException(controller.GetType().FullName, "m_unlockedProtosDb");
    }

    internal static ToolFilterGroupRow GetBlueprintFilterBox(BlueprintsWindow window)
    {
        object host = GetBlueprintLibraryHost(window);
        return (GetMemberValue(host, "FilterBox") ?? GetMemberValue(host, "m_filterBox")) as ToolFilterGroupRow
            ?? throw new MissingMemberException(host.GetType().FullName, "FilterBox/m_filterBox");
    }

    internal static StaticEntityMassPlacer GetBlueprintEntityPlacer(BlueprintsWindow.Controller controller)
        => GetMemberValue(controller, "m_entityPlacer") as StaticEntityMassPlacer
            ?? throw new MissingMemberException(controller.GetType().FullName, "m_entityPlacer");

    internal static MethodInfo? FindBlueprintSetFolderMethod(Type windowType, out Type patchHostType)
    {
        MethodInfo? method = windowType.GetMethod(
            "setFolder", InstanceFlags, null, new[] { typeof(IBlueprintsFolder) }, null);
        if (method != null)
        {
            patchHostType = windowType;
            return method;
        }

        patchHostType = windowType.Assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintsLibraryTab")
            ?? windowType;
        return patchHostType.GetMethod(
            "setFolder", InstanceFlags, null, new[] { typeof(IBlueprintsFolder) }, null);
    }

    internal static object GetSetFolderTarget(BlueprintsWindow window, MethodInfo method)
        => method.DeclaringType!.IsInstanceOfType(window)
            ? window
            : GetBlueprintLibraryHost(window);

    internal static void ProductCreated(
        IProductsManager manager,
        ProductProto product,
        Quantity quantity,
        CreateReason reason)
    {
        MethodInfo method = FindProductsMethod(manager, "ProductCreated", 4)
            ?? FindProductsMethod(manager, "ProductCreated", 3)
            ?? throw new MissingMethodException(manager.GetType().FullName, "ProductCreated");
        object[] args = method.GetParameters().Length == 4
            ? new object[] { Option<Proto>.None, product, quantity, reason }
            : new object[] { product, quantity, reason };
        method.Invoke(manager, args);
    }

    internal static void ProductDestroyed(
        IProductsManager manager,
        ProductProto product,
        Quantity quantity,
        DestroyReason reason)
    {
        MethodInfo? method = FindProductsMethod(manager, "ProductDestroyed", 4)
            ?? FindProductsMethod(manager, "ProductDestroyed", 3)
            ?? FindProductsMethod(manager, "ProductDestroyed", 2);
        if (method == null)
            throw new MissingMethodException(manager.GetType().FullName, "ProductDestroyed");

        ParameterInfo[] parameters = method.GetParameters();
        object[] args;
        if (parameters.Length == 4)
            args = new object[] { Option<Proto>.None, product, quantity, reason };
        else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(ProductProto))
            args = new object[] { product, quantity, reason };
        else if (parameters.Length == 3)
            args = new object[] { Option<Proto>.None, new ProductQuantity(product, quantity), reason };
        else
            args = new object[] { new ProductQuantity(product, quantity), reason };
        method.Invoke(manager, args);
    }

    internal static object? GetMemberValue(object? instance, string name)
    {
        if (instance == null)
            return null;
        Type? type = instance.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (field != null)
                return field.GetValue(instance);
            PropertyInfo? property = type.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (property != null)
                return property.GetValue(instance);
            type = type.BaseType;
        }
        return null;
    }

    private static void SetMemberValue(object instance, string name, object value)
    {
        Type? type = instance.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }
            PropertyInfo? property = type.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingMemberException(instance.GetType().FullName, name);
    }

    private static object? GetOptionValue(object? option)
    {
        if (option == null)
            return null;
        object? hasValue = GetMemberValue(option, "HasValue");
        if (hasValue is bool has && !has)
            return null;
        return GetMemberValue(option, "ValueOrNull") ?? GetMemberValue(option, "Value");
    }

    private static MethodInfo? FindProductsMethod(object manager, string name, int parameterCount)
    {
        foreach (MethodInfo method in manager.GetType().GetMethods(InstanceFlags))
        {
            if (method.Name == name && method.GetParameters().Length == parameterCount)
                return method;
        }
        return null;
    }
}
