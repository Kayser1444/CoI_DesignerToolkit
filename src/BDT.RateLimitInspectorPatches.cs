// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Entities;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public static class RateLimitInspectorPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.RateLimitInspectorPatches");
    private static bool s_patched = false;

    public static void Apply(Harmony harmony)
    {
        if (s_patched) return;
        s_patched = true;

        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.ProductsSourceEntityInspector");
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.ProductsSinkEntityInspector");
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.UniversalProductsSourceInspector");
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.UniversalProductsSinkInspector");
            PatchInspectorConstructor(harmony, assembly, "Mafi.Unity.Ui.Inspectors.TransportInspector");
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to apply inspector patches: {ex.Message}");
        }
    }

    private static void PatchInspectorConstructor(Harmony harmony, Assembly assembly, string typeName)
    {
        var type = assembly.GetType(typeName);
        if (type == null)
        {
            s_log.Warning($"Type {typeName} not found");
            return;
        }

        var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (ctors.Length > 0)
        {
            harmony.Patch(ctors[0], postfix: new HarmonyMethod(typeof(RateLimitInspectorPatches), nameof(InspectorCtorPostfix)));
            s_log.Info($"Patched constructor for {typeName}");
        }
    }

    public static void InspectorCtorPostfix(object __instance)
    {
        try
        {
            var inspectorType = __instance.GetType();
            var baseType = inspectorType;
            PropertyInfo? entityProp = null;

            while (baseType != null)
            {
                if (entityProp == null)
                    entityProp = baseType.GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                baseType = baseType.BaseType;
            }

            if (entityProp == null) return;

            // Entity is null in the constructor. We will fetch it via a lambda when needed.


            FieldInfo? mainBodyField = null;
            var searchType = inspectorType;
            while (searchType != null && mainBodyField == null)
            {
                mainBodyField = searchType.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                searchType = searchType.BaseType;
            }

            if (mainBodyField != null)
            {
                var mainBody = mainBodyField.GetValue(__instance) as Column;
                var uiComponent = __instance as Mafi.Unity.UiToolkit.Component.UiComponent;
                if (mainBody != null && uiComponent != null)
                {
                    var panel = RateLimitUI.BuildPanel(uiComponent, () => entityProp.GetValue(__instance) as IEntity);
                    mainBody.Add(panel);
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"InspectorCtorPostfix EXCEPTION: {ex}");
        }
    }
}
