// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using Mafi;
using Mafi.Core.Console;

namespace CoIDesignerToolkit;

/// <summary>Registers BDT console commands.</summary>
[GlobalDependency(RegistrationMode.AsSelf, false, false)]
public sealed class BdtConsoleCommands
{
    [ConsoleCommand(false, false, "Gets or sets the session-only BDT diagnostic level. Allowed: warning, info, debug, trace.", "bdt_diagnostic_level")]
    private string bdtDiagnosticLevel(string value = "")
    {
        if (string.IsNullOrWhiteSpace(value))
            return $"[BDT] Diagnostic level: {BdtDiagnostics.Describe()}.";

        if (!BdtDiagnostics.TrySetSessionLevel(value, out string error))
            return $"[BDT] Invalid diagnostic level '{value}'. {error}";

        return $"[BDT] Diagnostic level set for this session: {BdtDiagnostics.Describe()}.";
    }

    [ConsoleCommand(false, false, "Sets the BDT pollution heatmap glow color (white, brown, purple, or #RRGGBB).", null)]
    private string bdtSetPollutionGlowColor(string? value = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"[BDT] Pollution glow color is {DesignerToolkitSettings.FormatPollutionGlowColor()}.";
        }

        if (!DesignerToolkitSettings.TrySetPollutionGlowColor(value, out string error))
            return $"[BDT] {error}";

        return $"[BDT] Pollution glow color set to {DesignerToolkitSettings.FormatPollutionGlowColor()}. It will be saved with the current game.";
    }
}
