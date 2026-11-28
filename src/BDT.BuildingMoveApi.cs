// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Mafi.Core;

namespace CoIDesignerToolkit;

/// <summary>Runtime integration for mods with research-dependent building limits.</summary>
public static class BuildingMoveApi
{
    private static readonly object s_lock = new object();
    [ThreadStatic] internal static EntityId? PreviewSource;
    [ThreadStatic] internal static string? PreviewPrototype;

    private static readonly HashSet<string> s_unique = new HashSet<string>(StringComparer.Ordinal);
    private static HashSet<string> s_relaxed = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Reports whether completed instances of this exact prototype ID must be removed before BDT
    /// places their replacement. False withdraws the report and requests a retry for queued moves
    /// on the next simulation pass. Vanilla uniqueness and placement validation still apply.
    /// Call on world load and when the effective limit changes.
    /// </summary>
    public static void SetUniqueForMove(string prototypeId, bool isUnique)
    {
        if (string.IsNullOrWhiteSpace(prototypeId))
            throw new ArgumentException("A prototype ID is required.", nameof(prototypeId));

        lock (s_lock)
        {
            if (isUnique)
                s_unique.Add(prototypeId);
            else if (s_unique.Remove(prototypeId))
                s_relaxed.Add(prototypeId);
        }
    }

    /// <summary>
    /// During BDT destination preflight, identifies the original that a custom uniqueness validator
    /// should exclude from its count. Returns false during actual construction.
    /// </summary>
    public static bool TryGetUniqueMoveSource(string prototypeId, out EntityId source)
    {
        if (PreviewSource is { } original && string.Equals(prototypeId, PreviewPrototype, StringComparison.Ordinal))
        {
            source = original;
            return true;
        }

        source = default;
        return false;
    }

    internal static bool IsReportedUnique(string prototypeId)
    {
        lock (s_lock)
            return s_unique.Contains(prototypeId);
    }

    internal static HashSet<string>? TakeRelaxedPrototypes()
    {
        lock (s_lock)
        {
            if (s_relaxed.Count == 0)
                return null;

            HashSet<string> changed = s_relaxed;
            s_relaxed = new HashSet<string>(StringComparer.Ordinal);
            return changed;
        }
    }

    internal static void Clear()
    {
        lock (s_lock)
        {
            s_unique.Clear();
            s_relaxed.Clear();
        }
    }
}
