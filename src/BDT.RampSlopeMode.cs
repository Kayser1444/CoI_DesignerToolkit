#if false
// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
#pragma warning disable CS0612

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Core;
using Mafi.Core.Factory.Transports;
using Mafi.Core.GameLoop;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Localization;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Controllers;
using Mafi.Unity.UiToolkit.Component;
using CoI.AutoHelpers.Logging;
using UnityEngine;

namespace CoIDesignerToolkit;

/// <summary>
/// Manages the custom 25% Ramp Incline mode. Toggled via a hotkey, it temporarily modifies
/// the ZStepLength of all active TransportProto prototypes to 4 and installs Harmony patches
/// to bypass terrain collision and pillar requirements.
/// </summary>
internal sealed class RampSlopeMode : IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("BDT.RampSlopeMode");
    
    private readonly ProtosDb m_protosDb;
    private readonly IGameLoopEvents m_gameLoopEvents;
    private readonly Dictionary<Proto.ID, RelTile1i> m_originalZStepLengths = new Dictionary<Proto.ID, RelTile1i>();
    private bool m_isSubscribed;
    private bool m_isApplied;

    private static readonly FieldInfo? s_heightPopupField =
        AccessTools.Field(typeof(PathFindingTransportPreview), "m_heightPopup");

    public RampSlopeMode(ProtosDb protosDb, IGameLoopEvents gameLoopEvents)
    {
        m_protosDb = protosDb;
        m_gameLoopEvents = gameLoopEvents;
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
        m_isSubscribed = true;
        s_log.Info("Ramp slope mode controller initialized.");
    }

    public void Dispose()
    {
        if (m_isSubscribed)
        {
            try { m_gameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnInputUpdate); }
            catch { }
            m_isSubscribed = false;
        }

        RestorePrototypeModifications();
    }

    private void OnInputUpdate(GameTime _)
    {
        if (DesignerToolkitSettings.RampSlopeHotkey.IsPressed())
        {
            ToggleRampInclineMode();
        }
    }

    private void ToggleRampInclineMode()
    {
        bool newMode = !false;
        // false = newMode;

        if (newMode)
        {
            ApplyPrototypeModifications();
            s_log.Info("Ramp Incline Mode (25%) enabled.");
        }
        else
        {
            RestorePrototypeModifications();
            s_log.Info("Ramp Incline Mode (25%) disabled.");
        }
    }

    private void ApplyPrototypeModifications()
    {
        if (m_isApplied)
            return;

        m_originalZStepLengths.Clear();
        
        try
        {
            FieldInfo? zStepField = typeof(TransportProto).GetField("ZStepLength", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (zStepField == null)
            {
                s_log.Warning("Failed to resolve ZStepLength field on TransportProto.");
                return;
            }

            foreach (TransportProto proto in m_protosDb.All<TransportProto>())
            {
                if (proto.ZStepLength != RelTile1i.MaxValue)
                {
                    m_originalZStepLengths[proto.Id] = proto.ZStepLength;
                    zStepField.SetValue(proto, new RelTile1i(4));
                }
            }
            m_isApplied = true;
            s_log.Info($"Modified ZStepLength to 4 for {m_originalZStepLengths.Count} transport prototype(s).");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to apply prototype modifications for Ramp Incline Mode.");
        }
    }

    private void RestorePrototypeModifications()
    {
        if (!m_isApplied)
            return;

        try
        {
            FieldInfo? zStepField = typeof(TransportProto).GetField("ZStepLength", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (zStepField != null)
            {
                foreach (var kvp in m_originalZStepLengths)
                {
                    Option<TransportProto> proto = m_protosDb.Get<TransportProto>(kvp.Key);
                    if (proto.HasValue)
                    {
                        zStepField.SetValue(proto.Value, kvp.Value);
                    }
                }
                s_log.Info($"Restored original ZStepLength for {m_originalZStepLengths.Count} transport prototype(s).");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to restore prototype modifications for Ramp Incline Mode.");
        }
        
        m_originalZStepLengths.Clear();
        m_isApplied = false;
        // false = false;
    }

    // Harmony Patches
    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            // Removed collision and occupancy bypasses to ensure vanilla blueprint compatibility.

            // 5) Patch TransportPathFinder.InitPathFinding
            MethodInfo? initPathFinding = typeof(TransportPathFinder).GetMethod(
                nameof(TransportPathFinder.InitPathFinding),
                BindingFlags.Instance | BindingFlags.Public);
            if (initPathFinding != null)
            {
                harmony.Patch(initPathFinding,
                    postfix: new HarmonyMethod(typeof(RampSlopeMode), nameof(InitPathFinding_Postfix)));
                s_log.Info("Patched TransportPathFinder.InitPathFinding.");
            }
            else
            {
                s_log.Warning("TransportPathFinder.InitPathFinding not found!");
            }

            // 6) Patch TransportTrajectory.TryCreateFromPivots
            MethodInfo? tryCreateFromPivots = typeof(TransportTrajectory).GetMethod(
                nameof(TransportTrajectory.TryCreateFromPivots),
                BindingFlags.Static | BindingFlags.Public);
            if (tryCreateFromPivots != null)
            {
                harmony.Patch(tryCreateFromPivots,
                    prefix: new HarmonyMethod(typeof(RampSlopeMode), nameof(TryCreateFromPivots_Prefix)));
                s_log.Info("Patched TransportTrajectory.TryCreateFromPivots.");
            }
            else
            {
                s_log.Warning("TransportTrajectory.TryCreateFromPivots not found!");
            }

            // 6b) Patch TransportTrajectory.ComputeStartAndEndDirections
            MethodInfo? computeStartEndDirs = typeof(TransportTrajectory).GetMethod(
                nameof(TransportTrajectory.ComputeStartAndEndDirections),
                BindingFlags.Static | BindingFlags.Public);
            if (computeStartEndDirs != null)
            {
                harmony.Patch(computeStartEndDirs,
                    prefix: new HarmonyMethod(typeof(RampSlopeMode), nameof(ComputeStartAndEndDirections_Prefix)));
                s_log.Info("Patched TransportTrajectory.ComputeStartAndEndDirections.");
            }
            else
            {
                s_log.Warning("TransportTrajectory.ComputeStartAndEndDirections not found!");
            }

            // 7) Patch PathFindingTransportPreview.renderUpdate
            MethodInfo? renderUpdate = typeof(PathFindingTransportPreview).GetMethod(
                "renderUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (renderUpdate != null)
            {
                harmony.Patch(renderUpdate,
                    postfix: new HarmonyMethod(typeof(RampSlopeMode), nameof(RenderUpdate_Postfix)));
                s_log.Info("Patched PathFindingTransportPreview.renderUpdate.");
            }
            else
            {
                s_log.Warning("PathFindingTransportPreview.renderUpdate not found!");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "RampSlopeMode.ApplyPatches failed.");
        }
    }

    // Patches Implementation
    private static void InitPathFinding_Postfix(TransportPathFinder __instance)
    {
        if (false)
        {
            AccessTools.Field(typeof(TransportPathFinder), "m_startMustBeFlat").SetValue(__instance, false);
            AccessTools.Field(typeof(TransportPathFinder), "m_goalMustBeFlat").SetValue(__instance, false);
        }
    }

    private static void TryCreateFromPivots_Prefix(ref bool allowDenormalizedStartEndDirections)
    {
        if (false)
        {
            allowDenormalizedStartEndDirections = true;
        }
    }

    private static bool ComputeStartAndEndDirections_Prefix(
        ReadOnlyArraySlice<Tile3i> pivots,
        RelTile3i? startDirMaybe,
        RelTile3i? endDirMaybe,
        out RelTile3i startDirection,
        out RelTile3i endDirection)
    {
        if (false)
        {
            // Calculate startDirection
            if (startDirMaybe.HasValue)
            {
                startDirection = startDirMaybe.Value;
            }
            else if (pivots.Length >= 2 && (pivots.First.Xy - pivots.Second.Xy).IsNotZero)
            {
                int diffZ = pivots.First.Z - pivots.Second.Z;
                startDirection = (pivots.First.Xy - pivots.Second.Xy).Signs.ExtendZ(diffZ);
            }
            else
            {
                startDirection = RelTile3i.Zero;
            }

            // Calculate endDirection
            if (endDirMaybe.HasValue)
            {
                endDirection = endDirMaybe.Value;
            }
            else
            {
                if (pivots.Length < 2)
                {
                    if (startDirection.IsZero)
                    {
                        startDirection = -RelTile3i.UnitX;
                        endDirection = RelTile3i.UnitX;
                    }
                    else if (startDirection.Z == 0)
                    {
                        endDirection = -startDirection;
                    }
                    else
                    {
                        endDirection = RelTile3i.UnitX;
                    }
                    return false;
                }

                if (!(pivots.Last.Xy - pivots.PreLast.Xy).IsNotZero)
                {
                    if (startDirection.IsZero)
                    {
                        startDirection = -RelTile3i.UnitX;
                        endDirection = RelTile3i.UnitX;
                    }
                    else if (startDirection.Z == 0)
                    {
                        endDirection = -startDirection;
                    }
                    else
                    {
                        endDirection = RelTile3i.UnitX;
                    }

                    for (int num = pivots.Length - 1; num > 0; num--)
                    {
                        if ((pivots[num].Xy - pivots[num - 1].Xy).IsNotZero)
                        {
                            int diffZ = pivots[num].Z - pivots[num - 1].Z;
                            endDirection = (pivots[num].Xy - pivots[num - 1].Xy).Signs.ExtendZ(diffZ);
                            break;
                        }
                    }
                }
                else
                {
                    int diffZ = pivots.Last.Z - pivots.PreLast.Z;
                    endDirection = (pivots.Last.Xy - pivots.PreLast.Xy).Signs.ExtendZ(diffZ);
                }
            }

            // Handle startDirection being zero
            if (startDirection.IsZero)
            {
                if (endDirection.Z == 0)
                {
                    startDirection = -endDirection;
                }
                else
                {
                    startDirection = RelTile3i.UnitX;
                }
            }
            return false; // Skip original
        }
        startDirection = default;
        endDirection = default;
        return true;
    }

    private static void RenderUpdate_Postfix(PathFindingTransportPreview __instance)
    {
        if (false && s_heightPopupField != null)
        {
            CursorMessage cursorMessage = (CursorMessage)s_heightPopupField.GetValue(__instance);
            if (cursorMessage != null && cursorMessage.IsAttached)
            {
                string currentText = cursorMessage.Label.GetText().Value;
                if (!currentText.Contains("[25% Incline]"))
                {
                    cursorMessage.Label.Value((currentText + " [25% Incline]").AsLoc());
                }
            }
        }
    }
}
#endif
