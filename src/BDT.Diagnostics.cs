// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository
// is intended to contain only original mod code/configuration; if MaFi Games
// material is included by mistake, I intend to correct it promptly upon
// discovery or notice.
using System;
using System.Diagnostics;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

internal enum BdtDiagnosticLevel
{
    Warning = 0,
    Info = 1,
    Debug = 2,
    Trace = 3,
}

internal static class BdtDiagnostics
{
#if DEBUG
    internal const BdtDiagnosticLevel BuildDefaultLevel = BdtDiagnosticLevel.Debug;
#else
    internal const BdtDiagnosticLevel BuildDefaultLevel = BdtDiagnosticLevel.Info;
#endif

    private static BdtDiagnosticLevel s_level = BuildDefaultLevel;

    internal static BdtDiagnosticLevel Level => s_level;

    internal static bool IsEnabled(BdtDiagnosticLevel level) => s_level >= level;

    internal static string Describe()
        => $"active={s_level}, buildDefault={BuildDefaultLevel}";

    internal static bool TrySetSessionLevel(string? value, out string error)
    {
        if (!TryParseLevel(value, out BdtDiagnosticLevel parsed))
        {
            error = "Use warning, info, debug, or trace.";
            return false;
        }

        s_level = parsed;
        error = string.Empty;
        return true;
    }

    [Conditional("DEBUG")]
    internal static void Debug(ModLogger logger, string message)
    {
        if (IsEnabled(BdtDiagnosticLevel.Debug))
            logger.Info(message);
    }

    [Conditional("DEBUG")]
    internal static void Trace(ModLogger logger, string message)
    {
        if (IsEnabled(BdtDiagnosticLevel.Trace))
            logger.Info(message);
    }

    private static bool TryParseLevel(string? value, out BdtDiagnosticLevel level)
    {
        if (Enum.TryParse(value?.Trim(), true, out level)
            && Enum.IsDefined(typeof(BdtDiagnosticLevel), level))
            return true;

        level = BuildDefaultLevel;
        return false;
    }
}
