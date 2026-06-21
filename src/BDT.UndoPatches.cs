// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using HarmonyLib;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Commands;
using Mafi.Core.Input;
using Mafi.Core.Terrain.Designation;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal static class UndoPatches
{
    private static readonly ModLogger s_log = new ModLogger("BDT.UndoPatches");

    public static void Apply(Harmony harmony)
    {
        try
        {
            // We apply patches in this class manually or via harmony.PatchAll
            // For safety and control, let's patch the inner nested classes manually.
            harmony.PatchAll(typeof(UndoPatches).Assembly);
            s_log.Info("Successfully applied UndoPatches.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed to apply UndoPatches");
        }
    }

    [HarmonyPatch(typeof(InputScheduler), "processCmd", new Type[] { typeof(IInputCommand), typeof(bool) })]
    private static class ProcessCmdPatch
    {
        [HarmonyPrefix]
        private static void Prefix(IInputCommand cmd)
        {
            if (cmd is BatchCreateStaticEntitiesCmd || cmd is PasteSurfaceDesignationsCmd || cmd is BatchAddSurfaceDecalCmd || cmd is ReplaceEntityCmd)
            {
                UndoManager.BeginRecord();
            }
        }

        [HarmonyPostfix]
        private static void Postfix(IInputCommand cmd)
        {
            if (cmd is BatchCreateStaticEntitiesCmd || cmd is PasteSurfaceDesignationsCmd || cmd is BatchAddSurfaceDecalCmd || cmd is ReplaceEntityCmd)
            {
                if (cmd is PasteSurfaceDesignationsCmd pasteCmd)
                {
                    UndoManager.RecordPastedSurfaces(pasteCmd.Data);
                }
                else if (cmd is BatchAddSurfaceDecalCmd decalCmd)
                {
                    UndoManager.RecordPastedDecals(decalCmd.Data);
                }

                UndoManager.EndRecord();
            }
        }
    }

    [HarmonyPatch(typeof(EntitiesManager), "addEntityInternal", new Type[] { typeof(IEntity), typeof(EntityAddReason), typeof(bool) })]
    private static class AddEntityInternalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(IEntity entity)
        {
            if (UndoManager.IsRecording)
            {
                UndoManager.RecordPlacedEntity(entity.Id);
            }
        }
    }

    [HarmonyPatch(typeof(EntitiesManager), "removeEntityInternal", new Type[] { typeof(IEntity), typeof(EntityRemoveReason) })]
    private static class RemoveEntityInternalPatch
    {
        [HarmonyPrefix]
        private static void Prefix(IEntity entity)
        {
            if (UndoManager.IsRecording)
            {
                UndoManager.RecordRemovedEntity(entity);
            }
        }
    }

    [HarmonyPatch(typeof(ConstructionManager), nameof(ConstructionManager.StartDeconstruction), new Type[] { typeof(IStaticEntity), typeof(bool), typeof(EntityRemoveReason), typeof(bool) })]
    private static class StartDeconstructionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(IStaticEntity staticEntity)
        {
            if (UndoManager.IsRecording)
            {
                UndoManager.RecordDeconstructedEntity(staticEntity);
            }
        }
    }
}
