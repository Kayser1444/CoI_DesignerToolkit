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
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mafi;
using Mafi.Core.Factory.Transports;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal static class LegacyBeltConfigurations
{
    private static readonly ModLogger s_log = new ModLogger("BDT.LegacyBeltConfigurations");

    private static readonly FieldInfo? s_startMustNotHavePerpendicularRampField =
        AccessTools.Field(typeof(TransportPathFinder), "m_startMustNotHavePerpendicularRamp");
    private static readonly FieldInfo? s_goalMustNotHavePerpendicularRampField =
        AccessTools.Field(typeof(TransportPathFinder), "m_goalMustNotHavePerpendicularRamp");

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            // Patch tryGetStepCost: bypass the "no turn while on a ramp" IL checks.
            var stepCostMethod = typeof(TransportPathFinder).GetMethod(
                "tryGetStepCost",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (stepCostMethod != null)
            {
                harmony.Patch(stepCostMethod,
                    transpiler: new HarmonyMethod(typeof(LegacyBeltConfigurations), nameof(TryGetStepCostTranspiler)));
                s_log.Info("Patched TransportPathFinder.tryGetStepCost.");
            }
            else
            {
                s_log.Warning("TransportPathFinder.tryGetStepCost not found!");
            }

            // Patch InitPathFinding: clear perpendicular-ramp flags and flat-start/end flags at port nodes.
            var initPathFindingMethod = typeof(TransportPathFinder).GetMethod(
                "InitPathFinding",
                BindingFlags.Instance | BindingFlags.Public);
            if (initPathFindingMethod != null)
            {
                harmony.Patch(initPathFindingMethod,
                    prefix: new HarmonyMethod(typeof(LegacyBeltConfigurations), nameof(InitPathFindingPrefix)),
                    postfix: new HarmonyMethod(typeof(LegacyBeltConfigurations), nameof(InitPathFindingPostfix)));
                s_log.Info("Patched TransportPathFinder.InitPathFinding.");
            }
            else
            {
                s_log.Warning("TransportPathFinder.InitPathFinding not found!");
            }

            // Patch ChangeGoal: keep the goal perpendicular-ramp flag cleared as the cursor moves.
            var changeGoalMethod = typeof(TransportPathFinder).GetMethod(
                "ChangeGoal",
                BindingFlags.Instance | BindingFlags.Public);
            if (changeGoalMethod != null)
            {
                harmony.Patch(changeGoalMethod,
                    postfix: new HarmonyMethod(typeof(LegacyBeltConfigurations), nameof(ChangeGoalPostfix)));
                s_log.Info("Patched TransportPathFinder.ChangeGoal.");
            }
            else
            {
                s_log.Warning("TransportPathFinder.ChangeGoal not found!");
            }

            // Patch CanChangeDirectionOf: allow turning perpendicularly when extending a ramp segment.
            var canChangeDirMethod = typeof(TransportsConstructionHelper).GetMethod(
                "CanChangeDirectionOf",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (canChangeDirMethod != null)
            {
                harmony.Patch(canChangeDirMethod,
                    transpiler: new HarmonyMethod(typeof(LegacyBeltConfigurations), nameof(CanChangeDirectionOfTranspiler)));
                s_log.Info("Patched TransportsConstructionHelper.CanChangeDirectionOf.");
            }
            else
            {
                s_log.Warning("TransportsConstructionHelper.CanChangeDirectionOf not found!");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "LegacyBeltConfigurations.ApplyPatches");
        }
    }

    // ---------------------------------------------------------------------------
    // tryGetStepCost transpiler — bypass "no turn while ramping" checks.
    // ---------------------------------------------------------------------------

    private static bool IsParallelToOnRelTile2i(object operand) =>
        operand is MethodInfo mi &&
        mi.Name == "IsParallelTo" &&
        mi.DeclaringType?.Name == "RelTile2i";

    private static IEnumerable<CodeInstruction> TryGetStepCostTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var parallelHook = AccessTools.Method(typeof(LegacyBeltConfigurations), nameof(IsParallelToHook));
        var list = new List<CodeInstruction>(instructions);
        int replaced = 0;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].opcode == OpCodes.Call && IsParallelToOnRelTile2i(list[i].operand))
            {
                list[i].operand = parallelHook;
                replaced++;
            }
        }

        s_log.Info($"tryGetStepCost transpiler: replaced {replaced} IsParallelTo calls.");
        return list;
    }

    /// <summary>
    /// Hook replacing <c>RelTile2i.IsParallelTo</c> inside <c>tryGetStepCost</c>.
    /// When legacy belt configurations are enabled, always returns <c>true</c> so that
    /// the two ramp-turn rejection guards are suppressed.
    /// </summary>
    /// <remarks>
    /// <c>RelTile2i</c> is a struct; its instance method IL passes <c>this</c> by
    /// managed reference, so the hook signature uses <c>ref</c>.
    /// </remarks>
    public static bool IsParallelToHook(ref RelTile2i instance, RelTile2i other)
    {
        if (DesignerToolkitSettings.LegacyBeltConfigurationsEnabled)
            return true;
        return instance.IsParallelTo(other);
    }

    private static readonly FieldInfo? s_startMustBeFlatField =
        AccessTools.Field(typeof(TransportPathFinder), "m_startMustBeFlat");
    private static readonly FieldInfo? s_goalMustBeFlatField =
        AccessTools.Field(typeof(TransportPathFinder), "m_goalMustBeFlat");

    // ---------------------------------------------------------------------------
    // InitPathFinding patches — clear port-adjacent perpendicular-ramp constraints.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Prefix: strips flat-start/goal and ban-start-ramps-in-X/Y flags from the options
    /// before <c>InitPathFinding</c> uses them, so the pathfinder allows ramp steps in
    /// any direction at the start and goal nodes.
    /// </summary>
    public static void InitPathFindingPrefix(ref TransportPathFinderOptions options)
    {
        if (DesignerToolkitSettings.LegacyBeltConfigurationsEnabled)
        {
            const TransportPathFinderFlags rampFlags =
                TransportPathFinderFlags.StartMustBeFlat |
                TransportPathFinderFlags.GoalMustBeFlat |
                TransportPathFinderFlags.BanStartRampsInX |
                TransportPathFinderFlags.BanStartRampsInY;
            options = new TransportPathFinderOptions(
                options.PreferredHeight,
                options.ForcedStartDirection,
                // Clear banned-start-directions: the perpendicular ramp bans added when
                // the previous segment had a height change are what block legacy belt configs.
                // Removing all banned directions is safe because the pathfinder's cost model
                // strongly prefers forward paths and the placed port tiles are already banned
                // as occupied nodes.
                default,
                options.Flags & ~rampFlags
            );
        }
    }

    /// <summary>
    /// Postfix: after <c>InitPathFinding</c> has derived <c>m_startMustBeFlat</c>,
    /// <c>m_goalMustBeFlat</c>, <c>m_startMustNotHavePerpendicularRamp</c>, and
    /// <c>m_goalMustNotHavePerpendicularRamp</c> from flags and the port scan, clear them
    /// all so the pathfinder allows perpendicular ramp directions at start and goal.
    /// </summary>
    public static void InitPathFindingPostfix(TransportPathFinder __instance)
    {
        if (DesignerToolkitSettings.LegacyBeltConfigurationsEnabled)
        {
            s_startMustBeFlatField?.SetValue(__instance, false);
            s_goalMustBeFlatField?.SetValue(__instance, false);
            s_startMustNotHavePerpendicularRampField?.SetValue(__instance, false);
            s_goalMustNotHavePerpendicularRampField?.SetValue(__instance, false);
        }
    }

    /// <summary>
    /// Postfix: keep the goal perpendicular-ramp constraint cleared as the user moves
    /// the cursor and <c>ChangeGoal</c> is called repeatedly.
    /// </summary>
    public static void ChangeGoalPostfix(TransportPathFinder __instance)
    {
        if (DesignerToolkitSettings.LegacyBeltConfigurationsEnabled)
        {
            s_goalMustNotHavePerpendicularRampField?.SetValue(__instance, false);
        }
    }

    // ---------------------------------------------------------------------------
    // CanChangeDirectionOf transpiler — allow perpendicular turns on existing ramps.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns an effective Z value for the ramp-direction guards in <c>CanChangeDirectionOf</c>.
    /// When legacy belt configurations are enabled, returns 0 so that the Z-difference check
    /// evaluates to false and the perpendicular-turn guard is bypassed.
    /// </summary>
    public static int GetTile3iZHook(int actualZ) =>
        DesignerToolkitSettings.LegacyBeltConfigurationsEnabled ? 0 : actualZ;

    /// <summary>
    /// Transpiler for <c>CanChangeDirectionOf</c>: intercepts every <c>ldfld Tile3i.Z</c>
    /// instruction and inserts a <see cref="GetTile3iZHook"/> call after it, so that the
    /// Z-difference guard evaluates to false when legacy belt configurations are enabled.
    /// </summary>
    /// <remarks>
    /// <c>Tile3i.Z</c> is a public field, not a property, so the IL uses <c>ldfld</c>
    /// rather than <c>call get_Z</c>.
    /// </remarks>
    private static IEnumerable<CodeInstruction> CanChangeDirectionOfTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var zHook = AccessTools.Method(typeof(LegacyBeltConfigurations), nameof(GetTile3iZHook));
        var zField = AccessTools.Field(typeof(Tile3i), nameof(Tile3i.Z));
        var list = new List<CodeInstruction>(instructions);
        int replaced = 0;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].opcode == OpCodes.Ldfld &&
                list[i].operand is FieldInfo fi &&
                fi == zField)
            {
                // Insert the hook call immediately after ldfld Tile3i.Z.
                // The hook takes the loaded int and returns an effective int (0 when enabled).
                list.Insert(i + 1, new CodeInstruction(OpCodes.Call, zHook));
                i++; // skip the newly inserted instruction
                replaced++;
            }
        }

        s_log.Info($"CanChangeDirectionOf transpiler: wrapped {replaced} Tile3i.Z field loads.");
        return list;
    }
}
