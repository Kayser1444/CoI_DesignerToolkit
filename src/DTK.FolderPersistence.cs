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
using Mafi.Core.Entities.Blueprints;
using Mafi.Core.Mods;
using Mafi.Unity.Ui.Blueprints;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

/// <summary>
/// Persists the last-open folder in the blueprint book to config.json and
/// restores it when the window is constructed.
/// </summary>
internal static class FolderPersistence
{
    private static readonly ModLogger s_log = new ModLogger("DTK.FolderPersist");

    internal const string CONFIG_KEY = "last_blueprint_folder";
    private const char PATH_SEP = '>';

    private static ModJsonConfig? s_config;
    private static MethodInfo? s_setFolderMethod;

    internal static void ApplyPatches(Harmony harmony, ModJsonConfig config)
    {
        s_config = config;

        try
        {
            var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
            var windowType = assembly.GetType("Mafi.Unity.Ui.Blueprints.BlueprintsWindow");
            if (windowType == null)
            {
                s_log.Warning("BlueprintsWindow type not found — skipping folder persistence.");
                return;
            }

            s_setFolderMethod = windowType.GetMethod(
                "setFolder",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(IBlueprintsFolder) },
                null);

            if (s_setFolderMethod == null)
            {
                s_log.Warning("setFolder method not found — skipping folder persistence.");
                return;
            }

            // Save on every folder navigation.
            harmony.Patch(s_setFolderMethod,
                postfix: new HarmonyMethod(typeof(FolderPersistence), nameof(SetFolderPostfix)));

            // Restore on window construction.
            var ctors = windowType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length > 0)
                harmony.Patch(ctors[0],
                    postfix: new HarmonyMethod(typeof(FolderPersistence), nameof(CtorPostfix)));

            s_log.Info("Folder persistence patches applied.");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "FolderPersistence.ApplyPatches");
        }
    }

    // Runs after the BlueprintsWindow constructor completes.
    // Navigates to the last saved folder by invoking setFolder via the cached MethodInfo.
    private static void CtorPostfix(object __instance)
    {
        try
        {
            s_log.Info("CtorPostfix: entered");
            if (s_config == null || s_setFolderMethod == null)
            {
                s_log.Warning("CtorPostfix: s_config or s_setFolderMethod is null — aborting");
                return;
            }
            string savedPath = s_config.GetString(CONFIG_KEY, "");
            s_log.Info($"CtorPostfix: savedPath='{savedPath}'");
            if (string.IsNullOrEmpty(savedPath))
                return;

            var window = (BlueprintsWindow)__instance;
            s_log.Info($"CtorPostfix: root folder name='{window.BlueprintsLibrary.Root.Name}', children={window.BlueprintsLibrary.Root.Folders.Count}");
            IBlueprintsFolder target = FindFolder(window.BlueprintsLibrary.Root, savedPath);
            s_log.Info($"CtorPostfix: resolved target='{target?.Name}'");
            if (target == null || target == window.BlueprintsLibrary.Root)
            {
                s_log.Info("CtorPostfix: target is null or root — not navigating");
                return;
            }

            s_setFolderMethod.Invoke(window, new object[] { target });
            s_log.Info($"CtorPostfix: navigated to '{target.Name}'");
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "FolderPersistence.CtorPostfix");
        }
    }

    // Runs after setFolder completes. Serializes the new CurrentFolder path to config.
    private static void SetFolderPostfix(object __instance)
    {
        try
        {
            if (s_config == null) return;
            var window = (BlueprintsWindow)__instance;
            string path = BuildPath(window.CurrentFolder, window.BlueprintsLibrary.Root);
            bool ok = s_config.TrySetValue(CONFIG_KEY, path, out string err);
            s_log.Info($"SetFolderPostfix: saved path='{path}', ok={ok}" + (ok ? "" : $", err={err}"));
        }
        catch (Exception ex)
        {
            s_log.Exception(ex, "FolderPersistence.SetFolderPostfix");
        }
    }

    // Walks up via ParentFolder to build a PATH_SEP-delimited path from root to folder.
    // Returns "" for root.
    private static string BuildPath(IBlueprintsFolder folder, IBlueprintsFolder root)
    {
        if (folder == root)
            return "";

        var segments = new List<string>();
        IBlueprintsFolder current = folder;
        while (current != null && current != root)
        {
            segments.Add(current.Name);
            current = current.ParentFolder.ValueOrNull;
        }
        segments.Reverse();
        return string.Join(PATH_SEP.ToString(), segments);
    }

    // Walks down from root following each path segment by name.
    // Returns the deepest folder found (graceful degradation if a segment is missing).
    private static IBlueprintsFolder FindFolder(IBlueprintsFolder root, string path)
    {
        if (string.IsNullOrEmpty(path))
            return root;

        string[] parts = path.Split(PATH_SEP);
        IBlueprintsFolder current = root;
        foreach (string name in parts)
        {
            IBlueprintsFolder? next = null;
            for (int i = 0; i < current.Folders.Count; i++)
            {
                if (current.Folders[i].Name == name)
                {
                    next = current.Folders[i];
                    break;
                }
            }
            if (next == null)
                break; // folder was renamed or deleted; stop at last known good folder
            current = next;
        }
        return current;
    }
}
