// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System.Linq;
using CoI.AutoHelpers.InputControl;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoIDesignerToolkit;

internal readonly struct BdtHotkey
{
    public readonly KeyBindings Bindings;

    public BdtHotkey(KeyBindings bindings)
    {
        Bindings = bindings;
    }

    public KeyBinding Primary => Bindings.Primary;

    public KeyBinding Secondary => Bindings.Secondary;

    public bool IsPressed()
    {
        return IsPressed(Primary) || IsPressed(Secondary);
    }

    public bool IsHeld()
    {
        return IsHeld(Primary) || IsHeld(Secondary);
    }

    private static bool IsPressed(KeyBinding binding)
    {
        if (binding.IsEmpty)
            return false;

        ImmutableArray<KeyCode> keys = binding.Keys;
        KeyCode trigger = keys.Last;
        if (!CustomKeybindsInjector.IsLogicalKeyDownThisFrame(trigger))
            return false;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (!CustomKeybindsInjector.IsLogicalKeyDown(keys[i]))
                return false;
        }

        // Check if any standard modifier is pressed that is NOT in the hotkey keys
        bool hasCtrl = false;
        bool hasAlt = false;
        bool hasShift = false;

        for (int i = 0; i < keys.Length; i++)
        {
            KeyCode k = keys[i];
            if (k == KeyCode.LeftControl || k == KeyCode.RightControl)
                hasCtrl = true;
            else if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt)
                hasAlt = true;
            else if (k == KeyCode.LeftShift || k == KeyCode.RightShift)
                hasShift = true;
        }

        if (!hasCtrl && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftControl) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightControl)))
            return false;
        if (!hasAlt && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftAlt) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightAlt)))
            return false;
        if (!hasShift && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftShift) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightShift)))
            return false;

        return true;
    }

    public static bool IsHeld(KeyBinding binding)
    {
        if (binding.IsEmpty)
            return false;

        ImmutableArray<KeyCode> keys = binding.Keys;
        KeyCode trigger = keys.Last;
        if (!CustomKeybindsInjector.IsLogicalKeyDown(trigger))
            return false;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (!CustomKeybindsInjector.IsLogicalKeyDown(keys[i]))
                return false;
        }

        // Check if any standard modifier is pressed that is NOT in the hotkey keys
        bool hasCtrl = false;
        bool hasAlt = false;
        bool hasShift = false;

        for (int i = 0; i < keys.Length; i++)
        {
            KeyCode k = keys[i];
            if (k == KeyCode.LeftControl || k == KeyCode.RightControl)
                hasCtrl = true;
            else if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt)
                hasAlt = true;
            else if (k == KeyCode.LeftShift || k == KeyCode.RightShift)
                hasShift = true;
        }

        if (!hasCtrl && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftControl) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightControl)))
            return false;
        if (!hasAlt && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftAlt) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightAlt)))
            return false;
        if (!hasShift && (CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.LeftShift) || CustomKeybindsInjector.IsLogicalKeyDown(KeyCode.RightShift)))
            return false;

        return true;
    }
}
