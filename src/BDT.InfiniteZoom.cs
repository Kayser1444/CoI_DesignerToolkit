// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using HarmonyLib;
using Mafi;
using Mafi.Unity;
using Mafi.Unity.Camera;

namespace CoIDesignerToolkit;

internal static class InfiniteZoom
{
    private static readonly RelTile1f MinimumRadius = new RelTile1f(Fix32.FromRaw(1));

    // Leave room for the next scroll multiplication and fixed-point position conversions.
    private static readonly RelTile1f MaximumRadius = RelTile1f.MaxValue / 4;

    public static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(OrbitalCameraModel), "processZoom"),
            prefix: new HarmonyMethod(typeof(InfiniteZoom), nameof(ExtendWheelZoom)),
            transpiler: new HarmonyMethod(typeof(InfiniteZoom), nameof(SmoothZoomOutBelowOneTile)),
            finalizer: new HarmonyMethod(typeof(InfiniteZoom), nameof(RestoreLimits)));
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(OrbitalCameraModel), nameof(OrbitalCameraModel.GetEyeTransform)),
            postfix: new HarmonyMethod(typeof(InfiniteZoom), nameof(UpdateClipPlanes)));
    }

    private static bool IsWheelZoomEnabled =>
        DesignerToolkitSettings.InfiniteZoomEnabled && Mouse.GetScrollWheel() != 0f;

    private static IEnumerable<CodeInstruction> SmoothZoomOutBelowOneTile(IEnumerable<CodeInstruction> instructions) =>
        instructions.MethodReplacer(
            AccessTools.DeclaredMethod(typeof(RelTile1f), "op_LessThan", new[] { typeof(RelTile1f), typeof(RelTile1f) }),
            AccessTools.DeclaredMethod(typeof(InfiniteZoom), nameof(ShouldSnapToZoomOutFloor)));

    private static bool ShouldSnapToZoomOutFloor(RelTile1f radius, RelTile1f floor) =>
        !IsWheelZoomEnabled && radius < floor;

    private static void ExtendWheelZoom(
        ref RelTile1f ___m_minPivotDistance,
        ref RelTile1f ___m_maxPivotDistance,
        out (RelTile1f Min, RelTile1f Max)? __state)
    {
        __state = null;
        if (!IsWheelZoomEnabled)
            return;

        __state = (___m_minPivotDistance, ___m_maxPivotDistance);
        ___m_minPivotDistance = MinimumRadius;
        ___m_maxPivotDistance = MaximumRadius;
    }

    private static void RestoreLimits(
        ref RelTile1f ___m_minPivotDistance,
        ref RelTile1f ___m_maxPivotDistance,
        (RelTile1f Min, RelTile1f Max)? __state)
    {
        if (__state is not { } limits)
            return;

        ___m_minPivotDistance = limits.Min;
        ___m_maxPivotDistance = limits.Max;
    }

    private static void UpdateClipPlanes(
        OrbitalCameraModel __instance,
        RelTile1f ___m_minPivotDistance,
        ref float nearClip,
        ref float farClip)
    {
        if (!DesignerToolkitSettings.InfiniteZoomEnabled)
            return;

        float radius = __instance.OrbitRadius;
        if (radius < ___m_minPivotDistance.ToUnityUnits())
            nearClip = Math.Min(nearClip, Math.Max(0.02f, radius * 0.05f));

        farClip = Math.Max(farClip, radius + OrbitalCameraModel.FAR_PLANE_MAX_ZOOM);
    }
}
