using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.Audio;
using Mafi.Unity.Ui;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.InputControl;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
    private static UiContext? s_context;
    private static System.Action? s_clickSound;
    private static System.Action? s_transportModifierSound;

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportCleanup", "Transport cleanup tool", "Activates the transport cleanup selection tool", false)]
    public static KeyBindings TransportCleanup { get; set; } = FromPrimary(KeyCode.Delete, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterShowLayer", "Height filter show layer", "Increases the maximum visible height filter layer", false)]
    public static KeyBindings HeightFilterShowLayer { get; set; } = FromPrimary(KeyCode.PageUp);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterHideLayer", "Height filter hide layer", "Decreases the maximum visible height filter layer", false)]
    public static KeyBindings HeightFilterHideLayer { get; set; } = FromPrimary(KeyCode.PageDown);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityHigh", "Height filter transport visibility (high)", "Steps transport visibility towards High", false)]
    public static KeyBindings HeightFilterTransportVisibilityHigh { get; set; } = FromPrimary(KeyCode.PageUp, KbModifiers.Shift);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_HeightFilterTransportVisibilityLow", "Height filter transport visibility (low)", "Steps transport visibility towards Low", false)]
    public static KeyBindings HeightFilterTransportVisibilityLow { get; set; } = FromPrimary(KeyCode.PageDown, KbModifiers.Shift);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputOverlayToggle", "Toggle throughput overlay", "Toggles the throughput overlay", false)]
    public static KeyBindings ThroughputOverlayToggle { get; set; } = FromPrimary(KeyCode.T, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_ThroughputAoETool", "Throughput tool", "Activates the throughput area selection tool", false)]
    public static KeyBindings ThroughputAoETool { get; set; } = FromPrimary(KeyCode.T, KbModifiers.Alt | KbModifiers.Shift);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportProductRemovalAoETool", "Remove products in area", "Activates the area product-removal tool", false)]
    public static KeyBindings TransportProductRemovalAoETool { get; set; } = FromPrimary(KeyCode.Backspace, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_PollutionOverlayToggle", "Toggle pollution overlay", "Toggles the pollution overlay", false)]
    public static KeyBindings PollutionOverlayToggle { get; set; } = FromPrimary(KeyCode.P, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_RadiationOverlayToggle", "Toggle radiation overlay", "Toggles the radiation overlay", false)]
    public static KeyBindings RadiationOverlayToggle { get; set; } = FromPrimary(KeyCode.R, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_LayoutBoxMode", "Toggle layout box overlay", "Toggles layout box overlay", false)]
    public static KeyBindings LayoutBoxModeToggle { get; set; } = FromPrimary(KeyCode.B, KbModifiers.Alt);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_UndoPlacement", "Undo placement", "Undoes the last BDT placement action", false)]
    public static KeyBindings UndoPlacement { get; set; } = FromPrimary(KeyCode.Z, KbModifiers.Ctrl);

    [CoI.AutoHelpers.InputControl.ModKeybind("Bdt_TransportLazyHeight", "Transport lazy height routing", "Holds lazy height matching mode while placing transports (Alt)", true)]
    public static KeyBindings TransportLazyHeight { get; set; } = FromPrimary(modifiers: KbModifiers.Alt);

    public static void Initialize(UiContext context)
    {
        s_context = context;
        UiRoot audioOwner = context.UiRoot;
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

    internal static void Shutdown()
    {
        s_context = null;
        s_clickSound = null;
        s_transportModifierSound = null;
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
        UiContext? context = s_context;
        if (context == null ||
            !CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsPressed(bindings) ||
            context.CameraController.IsInFreeLookMode || EventSystem.current == null ||
            UnityInputManager.IsInputFieldFocused())
        {
            return false;
        }

        foreach (IUnityInputController controller in context.InputMgr.ActiveControllers)
        {
            if (controller.Config.BlockShortcuts)
                return false;
        }

        return true;
    }

    public static bool IsHeld(KeyBindings bindings)
    {
        return CoI.AutoHelpers.InputControl.CustomKeybindsInjector.IsHeld(bindings);
    }

    private static KeyBindings FromPrimary(KeyCode key = KeyCode.None, KbModifiers modifiers = KbModifiers.None)
    {
        return new KeyBindings(KbChord.Keys(modifiers, key), KbChord.Empty);
    }

}
