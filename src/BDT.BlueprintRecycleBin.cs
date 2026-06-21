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
using HarmonyLib;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Entities.Blueprints;
using Mafi.Localization;
using Mafi.Unity.Ui.Blueprints;
using System.Text.RegularExpressions;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal static class BlueprintRecycleBin
{
    private static readonly ModLogger s_log = new ModLogger("BDT.RecycleBin");

    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            // 1. Patch DeleteItem on BlueprintsLibrary
            var deleteItemMethod = typeof(BlueprintsLibrary).GetMethod(
                nameof(BlueprintsLibrary.DeleteItem),
                new[] { typeof(IBlueprintsFolder), typeof(IBlueprintItem) });

            if (deleteItemMethod == null)
            {
                s_log.Warning("DeleteItem method not found on BlueprintsLibrary.");
            }
            else
            {
                harmony.Patch(deleteItemMethod,
                    prefix: new HarmonyMethod(typeof(BlueprintRecycleBin), nameof(DeleteItemPrefix)));
                s_log.Info("Patched BlueprintsLibrary.DeleteItem for Recycle Bin.");
            }

            // 2. Patch deleteConfirm on BlueprintsWindow
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var windowType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintsWindow");
            if (windowType == null)
            {
                s_log.Warning("BlueprintsWindow type not found — skipping confirmation suppression patch.");
                return;
            }

            var deleteConfirmMethod = windowType.GetMethod(
                "deleteConfirm",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (deleteConfirmMethod == null)
            {
                s_log.Warning("deleteConfirm method not found on BlueprintsWindow.");
            }
            else
            {
                harmony.Patch(deleteConfirmMethod,
                    postfix: new HarmonyMethod(typeof(BlueprintRecycleBin), nameof(DeleteConfirmPostfix)));
                s_log.Info("Patched BlueprintsWindow.deleteConfirm for confirmation suppression.");
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "BlueprintRecycleBin.ApplyPatches");
        }
    }

    private static bool DeleteItemPrefix(BlueprintsLibrary __instance, IBlueprintsFolder parentFolder, IBlueprintItem item)
    {
        try
        {
            // If the feature is disabled, bypass and do normal permanent delete
            if (!DesignerToolkitSettings.UseRecycleBin)
            {
                return true;
            }

            IBlueprintsFolder root = __instance.Root;
            if (root == null) return true;

            // 1. If deleting the Recycle Bin folder itself, allow permanent delete
            if (item is IBlueprintsFolder folderItem && IsRecycleBinFolder(folderItem, root))
            {
                return true;
            }

            // 2. If deleting from within the Recycle Bin folder, allow permanent delete
            if (IsInRecycleBin(parentFolder, root))
            {
                return true;
            }

            // 3. Create Recycle Bin if not exists
            IBlueprintsFolder? recycleBin = FindRecycleBinFolder(root);
            if (recycleBin == null)
            {
                recycleBin = CreateRecycleBinFolder(__instance, root);
            }

            // 4. Determine unique name with suffix _n (first one _0) in the replicated folder path
            IBlueprintsFolder targetFolder = ReplicateFolderHierarchy(__instance, recycleBin, parentFolder);
            string uniqueName = GetUniqueName(targetFolder, item.Name);

            // 5. Copy the item to the Recycle Bin target folder
            if (item is IBlueprint bp)
            {
                CopyBlueprintToFolder(__instance, bp, targetFolder, uniqueName);
            }
            else if (item is IBlueprintsFolder folder)
            {
                CopyFolderRecursively(__instance, folder, targetFolder, uniqueName);
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed in DeleteItemPrefix");
        }

        return true; // Let the original DeleteItem delete the original item
    }

    private static void DeleteConfirmPostfix(object __instance, ref LocStrFormatted __result)
    {
        try
        {
            if (!DesignerToolkitSettings.UseRecycleBin)
            {
                return;
            }

            var window = (BlueprintsWindow)__instance;
            IBlueprintsFolder root = window.BlueprintsLibrary.Root;
            if (root == null) return;

            // Determine if the item being deleted is the Recycle Bin or nested inside it
            bool inBin = IsInRecycleBin(window.CurrentFolder, root);

            if (!inBin)
            {
                var selectedItemField = window.GetType().GetField("m_selectedItem", BindingFlags.Instance | BindingFlags.NonPublic);
                if (selectedItemField != null)
                {
                    object? selectedItemOpt = selectedItemField.GetValue(window);
                    if (selectedItemOpt != null)
                    {
                        var hasValueProperty = selectedItemOpt.GetType().GetProperty("HasValue");
                        if (hasValueProperty != null && (bool)hasValueProperty.GetValue(selectedItemOpt))
                        {
                            var valueProperty = selectedItemOpt.GetType().GetProperty("Value");
                            object? tile = valueProperty?.GetValue(selectedItemOpt);
                            if (tile != null)
                            {
                                var folderProp = tile.GetType().GetProperty("Folder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (folderProp != null)
                                {
                                    var folder = folderProp.GetValue(tile) as IBlueprintsFolder;
                                    if (folder != null && IsInRecycleBin(folder, root))
                                    {
                                        inBin = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // If we are NOT deleting from within the Recycle Bin (and not deleting the Recycle Bin itself), suppress confirmation popup
            if (!inBin)
            {
                __result = LocStrFormatted.Empty;
            }
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "Failed in DeleteConfirmPostfix");
        }
    }

    private static IBlueprintsFolder ReplicateFolderHierarchy(BlueprintsLibrary library, IBlueprintsFolder recycleBin, IBlueprintsFolder parentFolder)
    {
        IBlueprintsFolder root = library.Root;
        if (parentFolder == root)
        {
            return recycleBin;
        }

        var path = new List<IBlueprintsFolder>();
        IBlueprintsFolder? current = parentFolder;
        while (current != null && current != root)
        {
            if (IsInRecycleBin(current, root))
            {
                break;
            }
            path.Insert(0, current);
            current = current.ParentFolder.ValueOrNull;
        }

        IBlueprintsFolder currentDest = recycleBin;
        foreach (IBlueprintsFolder pathFolder in path)
        {
            IBlueprintsFolder? nextDest = null;
            for (int i = 0; i < currentDest.Folders.Count; i++)
            {
                if (currentDest.Folders[i].Name == pathFolder.Name)
                {
                    nextDest = currentDest.Folders[i];
                    break;
                }
            }

            if (nextDest == null)
            {
                nextDest = library.AddNewFolder(currentDest);
                library.RenameItem(nextDest, pathFolder.Name);
                if (!string.IsNullOrEmpty(pathFolder.Desc))
                {
                    library.SetDescription(nextDest, pathFolder.Desc);
                }
            }
            currentDest = nextDest;
        }

        return currentDest;
    }

    private static string StripRichText(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static bool IsRecycleBinFolder(IBlueprintsFolder folder, IBlueprintsFolder root)
    {
        if (folder.ParentFolder.ValueOrNull != root) return false;
        string configName = DesignerToolkitSettings.RecycleBinFolderName;
        string strippedName = StripRichText(folder.Name);
        return string.Equals(strippedName, configName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(strippedName, "Recycle Bin", StringComparison.OrdinalIgnoreCase);
    }

    private static IBlueprintsFolder? FindRecycleBinFolder(IBlueprintsFolder root)
    {
        string configName = DesignerToolkitSettings.RecycleBinFolderName;
        for (int i = 0; i < root.Folders.Count; i++)
        {
            var folder = root.Folders[i];
            string strippedName = StripRichText(folder.Name);
            if (string.Equals(strippedName, configName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strippedName, "Recycle Bin", StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }
        }
        return null;
    }

    private static IBlueprintsFolder CreateRecycleBinFolder(BlueprintsLibrary library, IBlueprintsFolder root)
    {
        string targetName = DesignerToolkitSettings.GetFormattedRecycleBinName();
        IBlueprintsFolder newBin = library.AddNewFolder(root);
        library.RenameItem(newBin, targetName);
        return newBin;
    }

    private static bool IsInRecycleBin(IBlueprintsFolder folder, IBlueprintsFolder root)
    {
        IBlueprintsFolder? current = folder;
        while (current != null && current != root)
        {
            if (IsRecycleBinFolder(current, root))
            {
                return true;
            }
            current = current.ParentFolder.ValueOrNull;
        }
        return false;
    }

    private static string GetUniqueName(IBlueprintsFolder folder, string baseName)
    {
        int n = 0;
        while (true)
        {
            string candidate = $"{baseName}_{n}";
            bool candidateExists = false;

            for (int i = 0; i < folder.Blueprints.Count; i++)
            {
                if (folder.Blueprints[i].Name == candidate)
                {
                    candidateExists = true;
                    break;
                }
            }

            if (!candidateExists)
            {
                for (int i = 0; i < folder.Folders.Count; i++)
                {
                    if (folder.Folders[i].Name == candidate)
                    {
                        candidateExists = true;
                        break;
                    }
                }
            }

            if (!candidateExists)
            {
                return candidate;
            }
            n++;
        }
    }

    private static void CopyBlueprintToFolder(BlueprintsLibrary library, IBlueprint bp, IBlueprintsFolder targetFolder, string targetName)
    {
        Option<IBlueprint> newBpOpt = library.AddBlueprint(targetFolder, bp.Items, bp.Surfaces, bp.Decals);
        if (newBpOpt.HasValue)
        {
            IBlueprint newBp = newBpOpt.Value;
            library.RenameItem(newBp, targetName);
            if (!string.IsNullOrEmpty(bp.Desc))
            {
                library.SetDescription(newBp, bp.Desc);
            }
            library.SetOverlapDeltas(newBp, bp.OverlapDeltaX, bp.OverlapDeltaY);
        }
        else
        {
            s_log.Warning($"Failed to add copy of blueprint '{bp.Name}' in Recycle Bin.");
        }
    }

    private static void CopyFolderRecursively(BlueprintsLibrary library, IBlueprintsFolder sourceFolder, IBlueprintsFolder targetParent, string targetName)
    {
        IBlueprintsFolder newFolder = library.AddNewFolder(targetParent);
        library.RenameItem(newFolder, targetName);
        if (!string.IsNullOrEmpty(sourceFolder.Desc))
        {
            library.SetDescription(newFolder, sourceFolder.Desc);
        }

        for (int i = 0; i < sourceFolder.Blueprints.Count; i++)
        {
            // Suffix formatting inside copied folders:
            // The prompt says "When deleting to RB, if that BP name already exists in RB, append _n..."
            // Inside the folder itself, we can just copy them with their original names since the folder itself
            // has a unique name under the Recycle Bin, but wait: is it required to append _n to nested BPs?
            // "When deleting to RB, if that BP name already exists in RB, append _n to the one in the BP..."
            // It says "When deleting to RB... if that BP name already exists in RB". So only the top level item
            // being deleted needs the unique name check in the Recycle Bin. Inside a folder, we can keep original names.
            CopyBlueprintToFolder(library, sourceFolder.Blueprints[i], newFolder, sourceFolder.Blueprints[i].Name);
        }

        for (int i = 0; i < sourceFolder.Folders.Count; i++)
        {
            CopyFolderRecursively(library, sourceFolder.Folders[i], newFolder, sourceFolder.Folders[i].Name);
        }
    }
}
