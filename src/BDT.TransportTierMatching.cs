// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Input;
using Mafi.Core.Ports.Io;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.UiState;
using Mafi.Unity.Ui.Controllers;
using Mafi.Unity.Ui.Library;
using PreviewRequest = Mafi.Unity.Ui.Controllers.PathFindingTransportPreview.PreviewRequest;

namespace CoIDesignerToolkit;

internal static class TransportTierMatching
{
    private static TerrainOccupancyManager? s_occupancy;
    private static IIoPortsManager? s_ports;
    private static TransportsManager? s_transports;
    private static LastSelectedTierTracker? s_tiers;

    private sealed class MatchedEndpoints
    {
        public MatchedEndpoints(Transport start, Transport end)
        {
            Start = start;
            End = end;
        }

        public readonly Transport Start;
        public readonly Transport End;
    }

    private static readonly ConditionalWeakTable<BuildTransportCmd, MatchedEndpoints> s_toolCommands =
        new ConditionalWeakTable<BuildTransportCmd, MatchedEndpoints>();
    private static (Transport Start, Transport End)? s_previewEndpoints;

    [ThreadStatic]
    private static bool s_previewing;

    private static readonly Action<TransportBuildController, TransportProto> SetToolTier =
        AccessTools.MethodDelegate<Action<TransportBuildController, TransportProto>>(
            AccessTools.DeclaredMethod(typeof(TransportBuildController), "setTransportProto"));
    private static readonly Func<PathFindingTransportPreview, IProtoWithTiers, string> GetTierLabel =
        AccessTools.MethodDelegate<Func<PathFindingTransportPreview, IProtoWithTiers, string>>(
            AccessTools.DeclaredMethod(typeof(PathFindingTransportPreview), "resolveTierLabel"));

    public static void Initialize(DependencyResolver resolver)
    {
        s_occupancy = resolver.Resolve<TerrainOccupancyManager>();
        s_ports = resolver.Resolve<IIoPortsManager>();
        s_transports = resolver.Resolve<TransportsManager>();
        s_tiers = resolver.Resolve<LastSelectedTierTracker>();
    }

    public static void Dispose()
    {
        s_occupancy = null;
        s_ports = null;
        s_transports = null;
        s_tiers = null;
        s_previewEndpoints = null;
    }

    public static void ApplyPatches(Harmony harmony)
    {
        Type controller = typeof(TransportBuildController);
        harmony.Patch(AccessTools.DeclaredMethod(controller, "handleSelectingFirstPivot"),
            transpiler: Patch(nameof(MatchStart)));
        harmony.Patch(AccessTools.DeclaredMethod(controller, "handleSelectingContinuationPivot"),
            transpiler: Patch(nameof(MatchContinuation)));
        harmony.Patch(AccessTools.DeclaredMethod(controller, "handleNoProtoSelected"),
            transpiler: Patch(nameof(KeepRememberedTier)));
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(PathFindingTransportPreview), nameof(PathFindingTransportPreview.ShowContinuationPreview)),
            prefix: Patch(nameof(UpdatePreviewTierLabel)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(PathFindingTransportPreview), "processContRequest"),
            prefix: Patch(nameof(BeginPreview)), finalizer: Patch(nameof(EndPreview)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(TransportsConstructionHelper), "CanJoinTo"),
            transpiler: Patch(nameof(ShowMatchedGhostsInPreview)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(TransportsCommandsProcessor), nameof(TransportsCommandsProcessor.Invoke),
                new[] { typeof(BuildTransportCmd) }),
            prefix: Patch(nameof(MatchGhostsForBuild)), finalizer: Patch(nameof(RestoreGhostsAfterFailedBuild)));
    }

    private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(TransportTierMatching), name);
    private static MethodInfo Own(string name) => AccessTools.DeclaredMethod(typeof(TransportTierMatching), name);

    private static IEnumerable<CodeInstruction> MatchStart(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(PreviewRequest), nameof(PreviewRequest.CreateStartRequest)),
            Own(nameof(CreateMatchedStartRequest)));

    private static IEnumerable<CodeInstruction> KeepRememberedTier(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(TransportBuildController), "setTransportProto"),
            Own(nameof(SelectRememberedTier)));

    private static IEnumerable<CodeInstruction> MatchContinuation(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        Type pickResult = AccessTools.Inner(typeof(TransportBuildController), "PickResult");
        FieldInfo position = AccessTools.DeclaredField(pickResult, "Position");
        FieldInfo direction = AccessTools.DeclaredField(pickResult, "Direction");
        MethodInfo factory = AccessTools.DeclaredMethod(typeof(PreviewRequest), nameof(PreviewRequest.CreateContRequest));
        MethodInfo schedule = AccessTools.DeclaredMethod(typeof(IInputScheduler), nameof(IInputScheduler.ScheduleInputCmd))
            .MakeGenericMethod(typeof(BuildTransportCmd));

        int positionLoad = codes.FindIndex(code => code.LoadsField(position));
        int requestCall = codes.FindIndex(code => code.Calls(factory));
        if (positionLoad < 1 || requestCall <= positionLoad
            || !codes[positionLoad - 1].IsLdloc())
            throw new InvalidOperationException("Could not find the transport cursor pick and continuation request.");

        var loadPick = codes[positionLoad - 1].Clone();
        CodeInstruction oldCall = codes[requestCall];
        loadPick.MoveLabelsFrom(oldCall).MoveBlocksFrom(oldCall);
        oldCall.opcode = OpCodes.Call;
        oldCall.operand = Own(nameof(CreateMatchedContinuationRequest));
        codes.Insert(requestCall, loadPick);
        codes.Insert(requestCall + 1, new CodeInstruction(OpCodes.Ldfld, direction));
        return codes.MethodReplacer(schedule, Own(nameof(ScheduleToolBuild)));
    }

    private static void SelectRememberedTier(TransportBuildController controller, TransportProto picked)
    {
        TransportProto? remembered = s_tiers?.GetLastUsedTransportForShape(picked.PortsShape).ValueOrNull;
        SetToolTier(controller, DesignerToolkitSettings.MatchTransportTierEnabled && remembered != null
            && !remembered.IsLockedOrUnavailable && InSameFamily(remembered, picked) ? remembered : picked);
    }

    private static PreviewRequest CreateMatchedStartRequest(
        TransportProto selected,
        Tile3i position,
        TransportPathFinderOptions options,
        Direction903d? direction,
        bool disableSnapping)
    {
        Transport? start = FindEndpoint(selected, position, direction, disableSnapping);
        TransportProto proto = PickTier(selected, start, null);
        return PreviewRequest.CreateStartRequest(proto, position, options, direction, disableSnapping);
    }

    private static PreviewRequest CreateMatchedContinuationRequest(
        TransportProto selected,
        Tile3i position,
        ImmutableArray<Tile3i> pivots,
        ImmutableArray<Tile2i> pillarHints,
        Option<TransportTrajectory> existingTrajectory,
        TransportPathFinderOptions options,
        ImmutableArray<Tile3i> bannedTiles,
        Direction903d? startDirection,
        bool disableSnapping,
        Direction903d? endDirection)
    {
        Transport? start = FindEndpoint(selected, pivots.First, startDirection, disableSnapping);
        Transport? end = FindEndpoint(selected, position, endDirection, disableSnapping);
        TransportProto proto = PickTier(selected, start, end);
        s_previewEndpoints = start != null && end != null ? (start, end) : null;

        if (existingTrajectory.ValueOrNull is { } trajectory && trajectory.TransportProto != proto
            && SameGeometry(trajectory.TransportProto, proto))
            existingTrajectory = trajectory.CreateCopyWithNewProto(proto);

        return PreviewRequest.CreateContRequest(proto, position, pivots, pillarHints, existingTrajectory,
            options, bannedTiles, startDirection, disableSnapping);
    }

    private static void UpdatePreviewTierLabel(
        PathFindingTransportPreview __instance,
        PreviewRequest request,
        PreviewRequest? ___m_requestedPreview,
        DockedBuyPricePopup ___m_pricePopup,
        ref string ___m_tierLabel)
    {
        if (request.Proto != ___m_requestedPreview?.Proto)
        {
            ___m_tierLabel = GetTierLabel(__instance, request.Proto);
            ___m_pricePopup.Label(request.Proto.Strings.Name);
        }
    }

    private static Transport? FindEndpoint(TransportProto selected, Tile3i position, Direction903d? direction, bool disableSnapping)
    {
        if (!DesignerToolkitSettings.MatchTransportTierEnabled || disableSnapping || s_occupancy == null)
            return null;

        if (s_occupancy.TryGetOccupyingEntityAt<Transport>(position, out var transport, s_transports?.IgnorePillarsPredicate)
            && InSameFamily(selected, transport.Prototype))
            return transport;

        if (direction.HasValue && s_ports != null
            && s_ports.TryGetPortAt(position + direction.Value.ToTileDirection(), -direction.Value, selected.PortsShape, out var port)
            && port.OwnerEntity is Transport connected && InSameFamily(selected, connected.Prototype))
            return connected;

        return null;
    }

    private static TransportProto PickTier(TransportProto selected, Transport? start, Transport? end)
    {
        if (start == null && end == null)
            return selected;

        TransportProto proto;
        if (start == null || end == null)
        {
            proto = (start ?? end)!.Prototype;
        }
        else if (start.IsConstructed != end.IsConstructed)
        {
            proto = start.IsConstructed ? start.Prototype : end.Prototype;
        }
        else
        {
            GetTierDepth(start.Prototype, out int startTier);
            GetTierDepth(end.Prototype, out int endTier);
            proto = startTier >= endTier ? start.Prototype : end.Prototype;
        }

        return proto.IsBuildable && !proto.IsLockedOrUnavailable ? proto : selected;
    }

    private static bool InSameFamily(TransportProto first, TransportProto second) =>
        first.PortsShape == second.PortsShape && GetBaseTier(first) == GetBaseTier(second);

    private static IProtoWithTiers GetBaseTier(IProtoWithTiers proto) => GetTierDepth(proto, out _);

    private static IProtoWithTiers GetTierDepth(IProtoWithTiers proto, out int depth)
    {
        depth = 1;
        while (depth < 20 && proto.TierData.PreviousTierIndirect.ValueOrNull is { } previous)
        {
            proto = previous;
            depth++;
        }
        return proto;
    }

    private static bool SameGeometry(TransportProto first, TransportProto second) =>
        first.ZStepLength == second.ZStepLength && first.CanGoUpDown == second.CanGoUpDown;

    private static bool CanRetierGhost(Transport transport, TransportProto proto) =>
        !transport.IsDestroyed && transport.Prototype != proto
        && transport.ConstructionState == ConstructionState.InConstruction
        && transport.ConstructionProgress.ValueOrNull is { CurrentSteps: 0 } progress
        && progress.GetAssetValueOfBuffers().IsEmpty
        && !proto.IsLockedOrUnavailable && InSameFamily(transport.Prototype, proto)
        && SameGeometry(transport.Prototype, proto);

    private static void BeginPreview(PreviewRequest request) =>
        s_previewing = DesignerToolkitSettings.MatchTransportTierEnabled && !request.DisablePortSnapping;
    private static void EndPreview() => s_previewing = false;

    private static IEnumerable<CodeInstruction> ShowMatchedGhostsInPreview(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo getPrototype = AccessTools.DeclaredPropertyGetter(typeof(Transport), nameof(Transport.Prototype));
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(getPrototype))
            {
                yield return instruction;
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Call, Own(nameof(GetPreviewJoinTier)));
        }
    }

    private static TransportProto GetPreviewJoinTier(Transport transport, TransportProto target) =>
        s_previewing && CanRetierGhost(transport, target) ? target : transport.Prototype;

    private static BuildTransportCmd ScheduleToolBuild(IInputScheduler scheduler, BuildTransportCmd command)
    {
        // Only commands issued by the route tool can change an existing ghost tier.
        if (s_previewEndpoints is { } endpoints && DesignerToolkitSettings.MatchTransportTierEnabled
            && !command.DisablePortSnapping && command.PivotPositions.Length > 1)
            s_toolCommands.Add(command, new MatchedEndpoints(endpoints.Start, endpoints.End));

        return scheduler.ScheduleInputCmd(command);
    }

    private static void MatchGhostsForBuild(
        BuildTransportCmd cmd,
        ProtosDb ___m_protosDb,
        ConstructionManager ___m_constructionManager,
        out List<(Transport Entity, TransportProto Previous)>? __state)
    {
        __state = null;
        if (!s_toolCommands.TryGetValue(cmd, out var endpoints))
            return;

        s_toolCommands.Remove(cmd);
        if (!DesignerToolkitSettings.MatchTransportTierEnabled
            || !___m_protosDb.TryGetProto<TransportProto>(cmd.ProtoId, out var proto))
            return;

        Transport start = endpoints.Start;
        Transport end = endpoints.End;
        if (start.IsDestroyed || end.IsDestroyed || PickTier(proto, start, end) != proto
            || !IsStillEndpoint(start, cmd.PivotPositions.First) || !IsStillEndpoint(end, cmd.PivotPositions.Last))
            return;

        Retier(start, proto, ___m_constructionManager, ref __state);
        if (end != start)
            Retier(end, proto, ___m_constructionManager, ref __state);
    }

    private static bool IsStillEndpoint(Transport transport, Tile3i position) =>
        transport.Trajectory.TryGetLowPivotIndexFor(position, out _, out _)
        || transport.StartInputPort.ExpectedConnectedPortCoord == position
        || transport.EndOutputPort.ExpectedConnectedPortCoord == position;

    private static void Retier(Transport transport, TransportProto proto, ConstructionManager construction,
        ref List<(Transport Entity, TransportProto Previous)>? changed)
    {
        if (!CanRetierGhost(transport, proto))
            return;

        TransportProto previous = transport.Prototype;
        if (construction.ReplaceEntityUnderConstruction(transport, proto, Option<EntityConfigData>.None))
        {
            changed ??= new List<(Transport Entity, TransportProto Previous)>(1);
            changed.Add((transport, previous));
        }
    }

    private static void RestoreGhostsAfterFailedBuild(
        BuildTransportCmd cmd,
        ConstructionManager ___m_constructionManager,
        List<(Transport Entity, TransportProto Previous)>? __state,
        Exception? __exception)
    {
        if (__state == null || (__exception == null && cmd.Result.IsValid))
            return;

        foreach (var (entity, previous) in __state)
        {
            if (!entity.IsDestroyed && entity.ConstructionState == ConstructionState.InConstruction)
                ___m_constructionManager.ReplaceEntityUnderConstruction(entity, previous, Option<EntityConfigData>.None);
        }
    }
}
