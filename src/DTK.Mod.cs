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
using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public sealed class DesignerToolkitMod : IMod, IDisposable
{
    private static readonly ModLogger s_log = new ModLogger("DTK");
    private Harmony? m_harmony;

    public string Name => "Designer Toolkit";

    public int Version => 1;

    public bool IsUiOnly => false;

    public ModManifest Manifest { get; }

    public ModJsonConfig JsonConfig { get; }

    public Option<IConfig> ModConfig { get; set; }

    public static string ModVersion { get; private set; } = "?";

    public DesignerToolkitMod(ModManifest manifest)
    {
        Manifest = manifest;
        ModVersion = manifest.Version.ToString();
        JsonConfig = new ModJsonConfig(this);
    }

    public void RegisterPrototypes(ProtoRegistrator registrator)
    {
        m_harmony = new Harmony("DesignerToolkit");
        BlueprintUpdater.ApplyPatches(m_harmony);
        FolderPersistence.ApplyPatches(m_harmony, JsonConfig);
        BlueprintStats.ApplyPatches(m_harmony);
        BlueprintExport.ApplyPatches(m_harmony);
        NormalizeSymmetric.ApplyPatches(m_harmony);
    }

    public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
    }

    public void EarlyInit(DependencyResolver resolver)
    {
    }

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        s_log.Info($"[DTK] Designer Toolkit v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(DesignerToolkitMod).Assembly)}");
    }

    public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
    }

    public void Dispose()
    {
        m_harmony?.UnpatchAll("DesignerToolkit");
    }
}
