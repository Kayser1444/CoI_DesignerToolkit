using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.Audio;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
// PRE_4_3_COMPAT: legacy branches/API guards only; remove after Update 5 or when pre-4.3 support ends.
#if !COI_UPDATE_4_3
    private const KbCategory BDT_CATEGORY = (KbCategory)100;
#endif
    private static System.Action? s_clickSound;
    private static System.Action? s_transportModifierSound;

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportCleanup", "Transport cleanup tool", "Activates the transport cleanup selection tool", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_TransportCleanup", "Transport cleanup tool", "Activates the transport cleanup selection tool", false, false, null)]
#endif
    public static KeyBindings TransportCleanup { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterShowLayer", "Height filter show layer", "Increases the maximum visible height filter layer", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_HeightFilterShowLayer", "Height filter show layer", "Increases the maximum visible height filter layer", false, false, null)]
#endif
    public static KeyBindings HeightFilterShowLayer { get; set; } = FromPrimaryKeys(KeyCode.PageUp);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterHideLayer", "Height filter hide layer", "Decreases the maximum visible height filter layer", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_HeightFilterHideLayer", "Height filter hide layer", "Decreases the maximum visible height filter layer", false, false, null)]
#endif
    public static KeyBindings HeightFilterHideLayer { get; set; } = FromPrimaryKeys(KeyCode.PageDown);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityHigh", "Height filter transport visibility (high)", "Steps transport visibility towards High", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_HeightFilterTransportVisibilityHigh", "Height filter transport visibility (high)", "Steps transport visibility towards High", false, false, null)]
#endif
    public static KeyBindings HeightFilterTransportVisibilityHigh { get; set; } = FromPrimaryKeys(KeyCode.LeftShift, KeyCode.PageUp);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityLow", "Height filter transport visibility (low)", "Steps transport visibility towards Low", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_HeightFilterTransportVisibilityLow", "Height filter transport visibility (low)", "Steps transport visibility towards Low", false, false, null)]
#endif
    public static KeyBindings HeightFilterTransportVisibilityLow { get; set; } = FromPrimaryKeys(KeyCode.LeftShift, KeyCode.PageDown);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputOverlayToggle", "Toggle throughput overlay", "Toggles the throughput overlay", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_ThroughputOverlayToggle", "Toggle throughput overlay", "Toggles the throughput overlay", false, false, null)]
#endif
    public static KeyBindings ThroughputOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.T);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputAoETool", "Throughput tool", "Activates the throughput area selection tool", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_ThroughputAoETool", "Throughput tool", "Activates the throughput area selection tool", false, false, null)]
#endif
    public static KeyBindings ThroughputAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.T);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportProductRemovalAoETool", "Remove products in area", "Activates the area product-removal tool", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_TransportProductRemovalAoETool", "Remove products in area", "Activates the area product-removal tool", false, false, null)]
#endif
    public static KeyBindings TransportProductRemovalAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Backspace);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_PollutionOverlayToggle", "Toggle pollution overlay", "Toggles the pollution overlay", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_PollutionOverlayToggle", "Toggle pollution overlay", "Toggles the pollution overlay", false, false, null)]
#endif
    public static KeyBindings PollutionOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.P);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_RadiationOverlayToggle", "Toggle radiation overlay", "Toggles the radiation overlay", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_RadiationOverlayToggle", "Toggle radiation overlay", "Toggles the radiation overlay", false, false, null)]
#endif
    public static KeyBindings RadiationOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.R);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_LayoutBoxMode", "Toggle layout box overlay", "Toggles layout box overlay", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_LayoutBoxMode", "Toggle layout box overlay", "Toggles layout box overlay", false, false, null)]
#endif
    public static KeyBindings LayoutBoxModeToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.B);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_UndoPlacement", "Undo placement", "Undoes the last BDT placement action", false)]
#else
    [Kb(BDT_CATEGORY, "Bdt_UndoPlacement", "Undo placement", "Undoes the last BDT placement action", false, false, null)]
#endif
    public static KeyBindings UndoPlacement { get; set; } = FromPrimaryKeys(KeyCode.LeftControl, KeyCode.Z);

#if COI_UPDATE_4_3
    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportLazyHeight", "Transport lazy height routing", "Holds lazy height matching mode while placing transports (Alt)", true)]
#else
    [Kb(BDT_CATEGORY, "Bdt_TransportLazyHeight", "Transport lazy height routing", "Holds lazy height matching mode while placing transports (Alt)", true, false, null)]
#endif
    public static KeyBindings TransportLazyHeight { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt);

    public static void Initialize(object audioOwner)
    {
        try
        {
            s_clickSound = CoI.AutoHelpers.Compatibility.UiAudio.Bind(audioOwner, "ButtonClick");
            s_transportModifierSound = CoI.AutoHelpers.Compatibility.UiAudio.Bind(audioOwner, "Rotate");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[BDT] Failed to initialize button click sound: {ex.Message}");
        }
    }

    public static void PlayClickSound()
    {
        if (s_clickSound != null)
        {
            s_clickSound();
        }
    }

    public static void PlayTransportModifierSound()
    {
        if (s_transportModifierSound != null)
        {
            s_transportModifierSound();
        }
    }

    public static bool IsPressed(KeyBindings bindings)
    {
#if COI_UPDATE_4_3
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsPressed(bindings);
#else
        return new BdtHotkey(bindings).IsPressed();
#endif
    }

    public static bool IsHeld(KeyBindings bindings)
    {
#if COI_UPDATE_4_3
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsHeld(bindings);
#else
        return new BdtHotkey(bindings).IsHeld();
#endif
    }

    private static KeyBindings FromPrimaryKeys(params KeyCode[] keys)
    {
#if COI_UPDATE_4_3
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.FromPrimaryKeys(keys);
#else
        return new KeyBindings(
            ShortcutMode.Game,
            new KeyBinding(BDT_CATEGORY, keys.ToImmutableArray()),
            KeyBinding.Empty(BDT_CATEGORY));
#endif
    }

#if !COI_UPDATE_4_3
    private static KeyBindings FromKeys(KeyCode primaryKey, KeyCode secondaryKey)
    {
        return new KeyBindings(
            ShortcutMode.Game,
            new KeyBinding(BDT_CATEGORY, ImmutableArray.Create(primaryKey)),
            new KeyBinding(BDT_CATEGORY, ImmutableArray.Create(secondaryKey)));
    }
#endif
}
