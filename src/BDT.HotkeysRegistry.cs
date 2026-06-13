using Mafi.Unity.InputControl;
using UnityEngine;
using Mafi.Collections.ImmutableCollections;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
    [Kb((KbCategory)100, "Bdt_LayoutBoxMode", "Layout Box Mode", "Toggles layout box mode", false, false, null)]
    public static KeyBindings LayoutBoxModeToggle { get; set; } = new KeyBindings(
        ShortcutMode.Game, 
        new KeyBinding((KbCategory)100, new[] { KeyCode.LeftAlt, KeyCode.B }.ToImmutableArray()), 
        KeyBinding.Empty((KbCategory)100));
}
