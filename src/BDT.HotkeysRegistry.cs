using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.Audio;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
    private static System.Action? s_clickSound;
    private static System.Action? s_transportModifierSound;

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportCleanup", "Transport cleanup tool", "Activates the transport cleanup selection tool", false)]
    public static KeyBindings TransportCleanup { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterShowLayer", "Height filter show layer", "Increases the maximum visible height filter layer", false)]
    public static KeyBindings HeightFilterShowLayer { get; set; } = FromPrimaryKeys(KeyCode.PageUp);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterHideLayer", "Height filter hide layer", "Decreases the maximum visible height filter layer", false)]
    public static KeyBindings HeightFilterHideLayer { get; set; } = FromPrimaryKeys(KeyCode.PageDown);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityHigh", "Height filter transport visibility (high)", "Steps transport visibility towards High", false)]
    public static KeyBindings HeightFilterTransportVisibilityHigh { get; set; } = FromPrimaryKeys(KeyCode.LeftShift, KeyCode.PageUp);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityLow", "Height filter transport visibility (low)", "Steps transport visibility towards Low", false)]
    public static KeyBindings HeightFilterTransportVisibilityLow { get; set; } = FromPrimaryKeys(KeyCode.LeftShift, KeyCode.PageDown);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputOverlayToggle", "Toggle throughput overlay", "Toggles the throughput overlay", false)]
    public static KeyBindings ThroughputOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.T);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputAoETool", "Throughput tool", "Activates the throughput area selection tool", false)]
    public static KeyBindings ThroughputAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.T);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportProductRemovalAoETool", "Remove products in area", "Activates the area product-removal tool", false)]
    public static KeyBindings TransportProductRemovalAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Backspace);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_PollutionOverlayToggle", "Toggle pollution overlay", "Toggles the pollution overlay", false)]
    public static KeyBindings PollutionOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.P);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_RadiationOverlayToggle", "Toggle radiation overlay", "Toggles the radiation overlay", false)]
    public static KeyBindings RadiationOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.R);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_LayoutBoxMode", "Toggle layout box overlay", "Toggles layout box overlay", false)]
    public static KeyBindings LayoutBoxModeToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.B);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_UndoPlacement", "Undo placement", "Undoes the last BDT placement action", false)]
    public static KeyBindings UndoPlacement { get; set; } = FromPrimaryKeys(KeyCode.LeftControl, KeyCode.Z);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportLazyHeight", "Transport lazy height routing", "Holds lazy height matching mode while placing transports (Alt)", true)]
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
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsPressed(bindings);
    }

    public static bool IsHeld(KeyBindings bindings)
    {
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsHeld(bindings);
    }

    private static KeyBindings FromPrimaryKeys(params KeyCode[] keys)
    {
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.FromPrimaryKeys(keys);
    }

}
