using Mafi.Collections.ImmutableCollections;
using Mafi.Unity.Audio;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class HotkeysRegistry
{
    private const KbCategory BDT_CATEGORY = (KbCategory)100;
    private static AudioSource? s_clickSound;

    [Kb(BDT_CATEGORY, "Bdt_TransportCleanup", "Transport cleanup tool", "Activates the transport cleanup selection tool", false, false, null)]
    public static KeyBindings TransportCleanup { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.Delete);

    [Kb(BDT_CATEGORY, "Bdt_HeightFilterShowLayer", "Height filter show layer", "Increases the maximum visible height filter layer", false, false, null)]
    public static KeyBindings HeightFilterShowLayer { get; set; } = FromPrimaryKeys(KeyCode.PageUp);

    [Kb(BDT_CATEGORY, "Bdt_HeightFilterHideLayer", "Height filter hide layer", "Decreases the maximum visible height filter layer", false, false, null)]
    public static KeyBindings HeightFilterHideLayer { get; set; } = FromPrimaryKeys(KeyCode.PageDown);

    [Kb(BDT_CATEGORY, "Bdt_ThroughputOverlayToggle", "Toggle throughput overlay", "Toggles the throughput overlay", false, false, null)]
    public static KeyBindings ThroughputOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.T);

    [Kb(BDT_CATEGORY, "Bdt_ThroughputAoETool", "Throughput tool", "Activates the throughput area selection tool", false, false, null)]
    public static KeyBindings ThroughputAoETool { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.T);

    [Kb(BDT_CATEGORY, "Bdt_PollutionOverlayToggle", "Toggle pollution overlay", "Toggles the pollution overlay", false, false, null)]
    public static KeyBindings PollutionOverlayToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.P);

    [Kb(BDT_CATEGORY, "Bdt_LayoutBoxMode", "Layout box mode", "Toggles layout box mode", false, false, null)]
    public static KeyBindings LayoutBoxModeToggle { get; set; } = FromPrimaryKeys(KeyCode.LeftAlt, KeyCode.B);

    [Kb(BDT_CATEGORY, "Bdt_UndoPlacement", "Undo placement", "Undoes the last BDT placement action", false, false, null)]
    public static KeyBindings UndoPlacement { get; set; } = FromPrimaryKeys(KeyCode.LeftControl, KeyCode.Z);

    public static void Initialize(AudioDb audioDb)
    {
        try
        {
            s_clickSound = audioDb.GetSharedAudioUi("Assets/Unity/UserInterface/Audio/ButtonClick.prefab");
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
            s_clickSound.Play();
        }
    }

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
