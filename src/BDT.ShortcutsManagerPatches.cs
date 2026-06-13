// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using HarmonyLib;
using Mafi.Unity.InputControl;

namespace CoIDesignerToolkit;

/// <summary>
/// Suppresses vanilla keybindings from triggering when a BDT hotkey that is a
/// superset of the vanilla keybind is currently being held down.
/// For example, if a vanilla tool uses Alt-E, and a BDT tool uses Ctrl-Alt-E,
/// this prevents Alt-E from firing when Ctrl-Alt-E is pressed.
/// </summary>
internal static class ShortcutsManagerPatches
{
    public static void Apply(Harmony harmony)
    {
        Type targetType = typeof(ShortcutsManager);

        var isDownMethod = targetType.GetMethod(nameof(ShortcutsManager.IsDown), new Type[] { typeof(KeyBindings) });
        if (isDownMethod != null)
        {
            harmony.Patch(isDownMethod, prefix: new HarmonyMethod(typeof(ShortcutsManagerPatches), nameof(IsDown_Prefix)));
        }

        var isUpMethod = targetType.GetMethod(nameof(ShortcutsManager.IsUp), new Type[] { typeof(KeyBindings) });
        if (isUpMethod != null)
        {
            harmony.Patch(isUpMethod, prefix: new HarmonyMethod(typeof(ShortcutsManagerPatches), nameof(IsUp_Prefix)));
        }

        var isOnMethod = targetType.GetMethod(nameof(ShortcutsManager.IsOn), new Type[] { typeof(KeyBindings) });
        if (isOnMethod != null)
        {
            harmony.Patch(isOnMethod, prefix: new HarmonyMethod(typeof(ShortcutsManagerPatches), nameof(IsOn_Prefix)));
        }
    }

    private static bool IsDown_Prefix(KeyBindings bindings, ref bool __result)
    {
        if (BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Primary) ||
            BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Secondary))
        {
            __result = false;
            return false;
        }
        return true;
    }

    private static bool IsUp_Prefix(KeyBindings bindings, ref bool __result)
    {
        if (BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Primary) ||
            BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Secondary))
        {
            __result = false;
            return false;
        }
        return true;
    }

    private static bool IsOn_Prefix(KeyBindings bindings, ref bool __result)
    {
        if (BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Primary) ||
            BdtKeyBindingUpdateHost.IsAnySupersetHeld(bindings.Secondary))
        {
            __result = false;
            return false;
        }
        return true;
    }
}
