using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CoI.AutoHelpers.Logging;
using HarmonyLib;
using Mafi;
using Mafi.Core.Factory.Transports;

namespace CoIDesignerToolkit;

/// <summary>
/// TEMPORARY TEST FIX: keeps the serialized trajectory when a transport is
/// constructed from an entity config (for example by blueprint paste).
///
/// Vanilla previews config-based transports with port snapping disabled, but
/// commits them through TryBuildTransportFromConfig with snapping enabled. An
/// overlapping connector can then shorten the pasted trajectory by one tile
/// and recreate the connector at that overlap position.
/// </summary>
internal static class BlueprintTransportExactPasteFix
{
    private static readonly ModLogger Log = new("BDT.BlueprintTransportExactPasteFix");

    [ThreadStatic]
    private static int s_configBuildDepth;

    public static void ApplyPatches(Harmony harmony)
    {
        MethodInfo? target = AccessTools.Method(
            typeof(TransportsCommandsProcessor),
            nameof(TransportsCommandsProcessor.TryBuildTransportFromConfig));

        if (target == null)
        {
            Log.Warning(
                "Temporary blueprint transport paste fix was not applied: " +
                "TryBuildTransportFromConfig was not found.");
            return;
        }

        harmony.Patch(
            target,
            prefix: new HarmonyMethod(typeof(BlueprintTransportExactPasteFix), nameof(ExecutionPrefix)),
            transpiler: new HarmonyMethod(typeof(BlueprintTransportExactPasteFix), nameof(Transpiler)),
            finalizer: new HarmonyMethod(typeof(BlueprintTransportExactPasteFix), nameof(ExecutionFinalizer)));

        MethodInfo? helper = AccessTools.Method(
            typeof(TransportsConstructionHelper),
            nameof(TransportsConstructionHelper.CanBuildOrJoinTransport));

        if (helper == null)
        {
            Log.Warning(
                "[DEBUG-BDT-PASTE] trajectory trace was not applied: " +
                "CanBuildOrJoinTransport was not found.");
            return;
        }

        harmony.Patch(
            helper,
            prefix: new HarmonyMethod(typeof(BlueprintTransportExactPasteFix), nameof(BuildRequestPrefix)),
            postfix: new HarmonyMethod(typeof(BlueprintTransportExactPasteFix), nameof(BuildResultPostfix)));
    }

    private static void ExecutionPrefix(bool applyConfiguration, bool isFree)
    {
        s_configBuildDepth++;
        Log.Info(
            "[DEBUG-BDT-PASTE] executed TryBuildTransportFromConfig " +
            $"(applyConfiguration={applyConfiguration}, isFree={isFree}).");
    }

    private static Exception? ExecutionFinalizer(Exception? __exception)
    {
        s_configBuildDepth = Math.Max(0, s_configBuildDepth - 1);
        return __exception;
    }

    private static void BuildRequestPrefix(
        ref bool disallowMiniZipAtStart,
        ref bool disallowMiniZipAtEnd)
    {
        if (s_configBuildDepth <= 0)
        {
            return;
        }

        disallowMiniZipAtStart = true;
        disallowMiniZipAtEnd = true;
        Log.Info(
            "[DEBUG-BDT-PASTE] protected both copied-transport endpoints " +
            "from automatic connector creation.");
    }

    private static void BuildResultPostfix(
        bool disablePortSnapping,
        CanBuildTransportResult result,
        bool __result)
    {
        if (s_configBuildDepth <= 0)
        {
            return;
        }

        string requested = FormatTrajectory(result.RequestPivots);
        string built = result.NewTrajectory.HasValue
            ? FormatTrajectory(result.NewTrajectory.Value.Pivots)
            : "<none>";
        string joinStart = result.MiniZipJoinResultAtStart.HasValue
            ? result.MiniZipJoinResultAtStart.Value.CutOutResult.CutOutPosition.ToString()
            : "<none>";
        string joinEnd = result.MiniZipJoinResultAtEnd.HasValue
            ? result.MiniZipJoinResultAtEnd.Value.CutOutResult.CutOutPosition.ToString()
            : "<none>";
        string zipperStart = result.MiniZipperAtStart.HasValue
            ? result.MiniZipperAtStart.Value.Position.ToString()
            : "<none>";
        string zipperEnd = result.MiniZipperAtEnd.HasValue
            ? result.MiniZipperAtEnd.Value.Position.ToString()
            : "<none>";

        Log.Info(
            "[DEBUG-BDT-PASTE] CanBuildOrJoinTransport result: " +
            $"success={__result}, disablePortSnapping={disablePortSnapping}, " +
            $"requested={requested}, built={built}, " +
            $"joinStart={joinStart}, joinEnd={joinEnd}, " +
            $"zipperStart={zipperStart}, zipperEnd={zipperEnd}.");
    }

    private static string FormatTrajectory(Mafi.Collections.ImmutableCollections.ImmutableArray<Tile3i> pivots)
    {
        if (pivots.IsEmpty)
        {
            return "count=0";
        }

        return $"count={pivots.Length},first={pivots.First},last={pivots.Last}";
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        List<int> buildCalls = code
            .Select((instruction, index) => (instruction, index))
            .Where(x =>
                (x.instruction.opcode == OpCodes.Call || x.instruction.opcode == OpCodes.Callvirt) &&
                x.instruction.operand is MethodInfo method &&
                method.DeclaringType == typeof(TransportsCommandsProcessor) &&
                method.Name == "tryBuildTransport")
            .Select(x => x.index)
            .ToList();

        if (buildCalls.Count != 1)
        {
            Log.Warning(
                "Temporary blueprint transport paste fix was not applied: " +
                $"expected one tryBuildTransport call, found {buildCalls.Count}.");
            return code;
        }

        int callIndex = buildCalls[0];
        int searchStart = System.Math.Max(0, callIndex - 32);
        List<int> snappingFlags = new();

        for (int i = searchStart; i < callIndex; i++)
        {
            if (code[i].opcode != OpCodes.Ldc_I4_0)
            {
                continue;
            }

            int next = i + 1;
            while (next < callIndex && code[next].opcode == OpCodes.Nop)
            {
                next++;
            }

            // tryBuildTransport receives disablePortSnapping immediately before
            // the public method's isFree argument (instance argument 3).
            if (next < callIndex && code[next].opcode == OpCodes.Ldarg_3)
            {
                snappingFlags.Add(i);
            }
        }

        if (snappingFlags.Count != 1)
        {
            Log.Warning(
                "Temporary blueprint transport paste fix was not applied: " +
                $"expected one snapping flag, found {snappingFlags.Count}.");
            return code;
        }

        CodeInstruction flag = code[snappingFlags[0]];
        flag.opcode = OpCodes.Ldc_I4_1;
        flag.operand = null;

        Log.Info(
            "[DEBUG-BDT-PASTE] installed temporary blueprint transport paste fix: " +
            "port snapping is disabled for config-based transport placement.");

        return code;
    }
}
