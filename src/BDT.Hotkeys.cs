// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.GameMenu.Settings;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoIDesignerToolkit;

internal readonly struct BdtHotkey
{
    private const KbCategory CATEGORY = KbCategory.Tools;
    private const ShortcutMode MODE = ShortcutMode.Game;

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

    public BdtHotkey WithPrimary(KeyBinding binding)
    {
        return new BdtHotkey(new KeyBindings(MODE, binding, Secondary));
    }

    public BdtHotkey WithSecondary(KeyBinding binding)
    {
        return new BdtHotkey(new KeyBindings(MODE, Primary, binding));
    }

    public string PrimaryConfigString()
    {
        return Primary.ToString();
    }

    public string SecondaryConfigString()
    {
        return Secondary.ToString();
    }

    public override string ToString()
    {
        return Bindings.ToNiceStringLong();
    }

    public static BdtHotkey FromPrimaryKeys(params KeyCode[] keys)
    {
        return new BdtHotkey(new KeyBindings(
            MODE,
            new KeyBinding(CATEGORY, keys.ToImmutableArray()),
            KeyBinding.Empty(CATEGORY)));
    }

    public static BdtHotkey FromConfigStrings(string primary, string secondary, BdtHotkey fallback)
    {
        return new BdtHotkey(new KeyBindings(
            MODE,
            ParseBinding(primary, fallback.Primary),
            ParseBinding(secondary, fallback.Secondary)));
    }

    public static BdtHotkey FromLegacy(KeyCode key, bool ctrl, bool alt, bool shift)
    {
        if (key == KeyCode.None)
            return new BdtHotkey(KeyBindings.Empty(CATEGORY, MODE));

        var keys = new List<KeyCode>();
        if (ctrl)
            keys.Add(KeyCode.LeftControl);
        if (shift)
            keys.Add(KeyCode.LeftShift);
        if (alt)
            keys.Add(KeyCode.LeftAlt);
        keys.Add(key);
        return FromPrimaryKeys(keys.ToArray());
    }

    private static KeyBinding ParseBinding(string value, KeyBinding fallback)
    {
        if (value == null)
            return fallback;

        if (value.Length == 0)
            return KeyBinding.Empty(CATEGORY);

        KeyBinding parsed = KeyBinding.Empty(CATEGORY).UpdateSelfFrom(value);
        return parsed.IsEmpty && value.Length > 0 ? fallback : parsed;
    }

    private static bool IsPressed(KeyBinding binding)
    {
        if (binding.IsEmpty)
            return false;

        ImmutableArray<KeyCode> keys = binding.Keys;
        KeyCode trigger = keys.Last;
        if (!Input.GetKeyDown(trigger))
            return false;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (!Input.GetKey(keys[i]))
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

        if (!hasCtrl && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            return false;
        if (!hasAlt && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            return false;
        if (!hasShift && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            return false;

        return true;
    }
}

internal sealed class BdtKeyBindingField : KeyBindingField
{
    private readonly Func<BdtHotkey> m_getHotkey;
    private readonly Action<BdtHotkey> m_setHotkey;
    private readonly bool m_isPrimary;
    private readonly List<KeyCode> m_keys = new List<KeyCode>();

    private bool m_isEditing;

    public BdtKeyBindingField(Func<BdtHotkey> getHotkey, Action<BdtHotkey> setHotkey, bool isPrimary)
    {
        m_getHotkey = getHotkey;
        m_setHotkey = setHotkey;
        m_isPrimary = isPrimary;

        this.Width(23.Percent()).Height(27.px());
        RegisterCallback<MouseUpEvent>(OnMouseUp);
        RegisterCallback<MouseDownEvent>(OnMouseDown);
        RegisterCallback<DetachFromPanelEvent>(_ => Cancel());
        Refresh();
    }

    public void Refresh()
    {
        KeyBinding binding = CurrentBinding();
        this.Value(ToShortNiceString(binding).AsLoc());
    }

    public void InputUpdate()
    {
        if (!m_isEditing)
            return;

        if (Input.GetKey(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        int pressedCount = 0;
        int previousCount = m_keys.Count;
        foreach (KeyCode key in ShortcutsMap.AllKeys)
        {
            if (!Input.GetKey(key))
                continue;

            pressedCount++;
            if (!m_keys.Contains(key))
                m_keys.Add(key);
        }

        if (previousCount != m_keys.Count)
        {
            string text = string.Join(" + ", m_keys.Select(ToShortNiceString));
            this.Value((text + " ...").AsLoc());
        }

        if (m_keys.Count > 0 && pressedCount < m_keys.Count)
            Commit(clear: false);
    }

    private void OnMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0 || m_isEditing)
            return;

        m_isEditing = true;
        m_keys.Clear();
        this.Value("Waiting for key press ...".AsLoc()).Color(Theme.PrimaryColor);
        BdtKeyBindingUpdateHost.Add(this);
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button == 1 && !m_isEditing)
            Commit(clear: true);
    }

    private void Cancel()
    {
        if (!m_isEditing)
            return;

        m_isEditing = false;
        m_keys.Clear();
        BdtKeyBindingUpdateHost.Remove(this);
        Refresh();
    }

    private void Commit(bool clear)
    {
        if (!m_isEditing && !clear)
            return;

        m_isEditing = false;
        KeyBinding binding = clear || m_keys.Count == 0
            ? KeyBinding.Empty(KbCategory.Tools)
            : new KeyBinding(KbCategory.Tools, m_keys.ToImmutableArray());

        BdtHotkey current = m_getHotkey();
        m_setHotkey(m_isPrimary ? current.WithPrimary(binding) : current.WithSecondary(binding));
        m_keys.Clear();
        BdtKeyBindingUpdateHost.Remove(this);
        this.Color(Theme.DefaultColor);
        Refresh();
    }

    private KeyBinding CurrentBinding()
    {
        BdtHotkey hotkey = m_getHotkey();
        return m_isPrimary ? hotkey.Primary : hotkey.Secondary;
    }

    private static string ToShortNiceString(KeyBinding binding)
    {
        return string.Join(" + ", binding.Keys.Select(ToShortNiceString));
    }

    private static string ToShortNiceString(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftControl:
            case KeyCode.RightControl:
                return "CTRL";
            case KeyCode.LeftShift:
            case KeyCode.RightShift:
                return "SHIFT";
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt:
                return "ALT";
            case KeyCode.LeftMeta:
            case KeyCode.RightMeta:
            case KeyCode.LeftWindows:
            case KeyCode.RightWindows:
                return "META";
            case KeyCode.PageUp:
                return "PgUp";
            case KeyCode.PageDown:
                return "PgDn";
            case KeyCode.Delete:
                return "Del";
            case KeyCode.Backspace:
                return "Bksp";
            case KeyCode.Return:
                return "Enter";
            default:
                return key.ToNiceString();
        }
    }
}

internal sealed class BdtKeyBindingUpdateHost : MonoBehaviour
{
    private static readonly List<BdtKeyBindingField> s_fields = new List<BdtKeyBindingField>();
    private static BdtKeyBindingUpdateHost? s_instance;

    public static void Add(BdtKeyBindingField field)
    {
        EnsureInstance();
        if (!s_fields.Contains(field))
            s_fields.Add(field);
    }

    public static void Remove(BdtKeyBindingField field)
    {
        s_fields.Remove(field);
    }

    private static void EnsureInstance()
    {
        if (s_instance != null)
            return;

        var hostObject = new GameObject("BDT Key Binding Update Host");
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        s_instance = hostObject.AddComponent<BdtKeyBindingUpdateHost>();
    }

    private void Update()
    {
        if (s_fields.Count == 0)
            return;

        BdtKeyBindingField[] fields = s_fields.ToArray();
        foreach (BdtKeyBindingField field in fields)
            field.InputUpdate();
    }
}
