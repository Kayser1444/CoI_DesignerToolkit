using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
    private const KbCategory BDT_CATEGORY = (KbCategory)100;

    [Kb(BDT_CATEGORY, "Bdt_TransportCleanup", "Transport Cleanup", "Activates the transport cleanup selection tool", false, false, null)]
    public static KeyBindings TransportCleanup { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);

    [Kb(BDT_CATEGORY, "Bdt_HeightFilterShowLayer", "Height Filter Show Layer", "Increases the maximum visible height filter layer", false, false, null)]
    public static KeyBindings HeightFilterShowLayer { get; set; } = FromPrimaryKeys(KeyCode.PageUp);

    [Kb(BDT_CATEGORY, "Bdt_HeightFilterHideLayer", "Height Filter Hide Layer", "Decreases the maximum visible height filter layer", false, false, null)]
    public static KeyBindings HeightFilterHideLayer { get; set; } = FromPrimaryKeys(KeyCode.PageDown);

    [Kb(BDT_CATEGORY, "Bdt_ThroughputOverlayToggle", "Throughput Overlay", "Toggles the throughput overlay", false, false, null)]
    public static KeyBindings ThroughputOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.T);

    [Kb(BDT_CATEGORY, "Bdt_ThroughputAoETool", "Throughput Tool", "Toggles the throughput area selection tool", false, false, null)]
    public static KeyBindings ThroughputAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.T);

    [Kb(BDT_CATEGORY, "Bdt_PollutionOverlayToggle", "Pollution Overlay", "Toggles the pollution overlay", false, false, null)]
    public static KeyBindings PollutionOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.P);

    [Kb(BDT_CATEGORY, "Bdt_LayoutBoxMode", "Layout Box Mode", "Toggles layout box mode", false, false, null)]
    public static KeyBindings LayoutBoxModeToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.B);

    [Kb(BDT_CATEGORY, "Bdt_UndoPlacement", "Undo Placement", "Undoes the last BDT placement action", false, false, null)]
    public static KeyBindings UndoPlacement { get; set; } = FromPrimaryKeys(KeyCode.LeftControl, KeyCode.Z);

    public static bool IsPressed(KeyBindings bindings)
    {
        return new BdtHotkey(bindings).IsPressed();
    }

    private static KeyBindings FromPrimaryKeys(params KeyCode[] keys)
    {
        return new KeyBindings(
            ShortcutMode.Game,
            new KeyBinding(BDT_CATEGORY, keys.ToImmutableArray()),
            KeyBinding.Empty(BDT_CATEGORY));
    }
}
