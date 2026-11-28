using System.Reflection;
using System.Runtime.CompilerServices;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Game;
using Mafi.Core.Simulation;

public static partial class Program
{
    private static void SurfaceInstantBuildContracts(string mod)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        Assembly bdt = Assembly.LoadFrom(mod);
        Type settings = bdt.GetType("CoIDesignerToolkit.DesignerToolkitSettings", true)!;
        Type modeType = bdt.GetType("CoIDesignerToolkit.InstantBuildMode", true)!;
        FieldInfo setting = settings.GetField("<InstantBuildModeEnabled>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        object mode = RuntimeHelpers.GetUninitializedObject(modeType);

        var difficulty = (GameDifficultyConfig)RuntimeHelpers.GetUninitializedObject(typeof(GameDifficultyConfig));
        Set(difficulty, "<Sandbox>k__BackingField", GameDifficultyConfig.SandboxSetting.Enabled);
        var sim = new SimLoopEvents();
        var entities = (EntitiesManager)RuntimeHelpers.GetUninitializedObject(typeof(EntitiesManager));
        Set(entities, "m_entitiesLinear", new LystMutableDuringIter<IEntity>());
        Set(mode, "m_difficultyConfig", difficulty);
        Set(mode, "m_simLoopEvents", sim);
        Set(mode, "m_entitiesManager", entities);
        Set(mode, "m_snapshot", new List<IStaticEntity>());
        int surfaceUpdates = 0;
        Set(mode, "m_updateSurfaces", (Action)(() => surfaceUpdates++));
        // The native paused game step invokes UpdateAfterCmdProc, then returns before Update.
        var afterCommands = (Event)sim.UpdateAfterCmdProc;
        afterCommands.AddNonSaveable(mode, (Action)modeType.GetMethod("OnUpdateAfterCmdProc", privateInstance)!.CreateDelegate(typeof(Action), mode));

        object? originalSetting = setting.GetValue(null);
        try
        {
            setting.SetValue(null, true);
            Set(sim, "<IsSimPaused>k__BackingField", true);
            afterCommands.Invoke();
            Check(surfaceUpdates == 1, "a paused Sandbox command cycle must advance pending surface work with BDT Instant build enabled");

            setting.SetValue(null, false);
            afterCommands.Invoke();
            Check(surfaceUpdates == 1, "paused surface work stays pending with BDT Instant build disabled");

            setting.SetValue(null, true);
            Set(difficulty, "<Sandbox>k__BackingField", GameDifficultyConfig.SandboxSetting.Disabled);
            afterCommands.Invoke();
            Check(surfaceUpdates == 1, "BDT never advances paused surface work outside Sandbox");

            Set(difficulty, "<Sandbox>k__BackingField", GameDifficultyConfig.SandboxSetting.Enabled);
            Set(sim, "<IsSimPaused>k__BackingField", false);
            afterCommands.Invoke();
            Check(surfaceUpdates == 1, "the unpaused native surface update remains the only surface pass");
        }
        finally
        {
            setting.SetValue(null, originalSetting);
        }
        Console.WriteLine("PASS: paused surface work advances only with BDT Instant build enabled in Sandbox.");
    }
}
