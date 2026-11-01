# DesignerToolkit 0.11.7 release preparation — 2026-11-01

- Patch scope: defer initial reservoir discovery to InitState, after native
  resource creation, and restore saved reservoir level/draw arrays returned by
  the native JSON parser. Keep version 0.11.7 and max verified game version 0.8.8.
- The focused GroundwaterContracts runner reproduced the exact initial-discovery
  null-reference warning before the lifecycle fix. After that fix, the populated
  fixture exposed silently skipped history arrays (recorded months 3, draw 0
  instead of 17); accepting enumerable arrays/list results fixes restoration.
- Current 0.8.8 Debug build passes with zero warnings/errors. GroundwaterContracts
  passes new-world, loaded-world and empty-map cases, including monthly/yearly
  levels and extraction history. Update43Contracts passes native transport buffer
  and mini-zipper reserve regressions. Live gameplay verification is pending.
- Legacy 0.8.7d/b619 Debug build also passes with zero warnings/errors. Running
  GroundwaterContracts against that preserved assembly snapshot stops in native
  DependencyResolverBuilder.BuildAndClear with the game's integrity-check error
  #7, before mod initialization; legacy runtime behavior is therefore unverified.
- No player-facing strings were added or changed. Translation status reports
  complete coverage for de/es/it/pt/ru/sv/zh; English source is unchanged. Config
  and hotkey localization validation passes all eight locale files.
- Manifest descriptions and the existing local Hub source remain accurate for
  this patch and need no edits. Live authenticated Hub-source comparison is
  blocked: browser inventory reports “Local browser use is disabled in Work
  settings.” No Hub source was overwritten and no page/version was published.
- Preserve pre-existing work-order.json changes, the release-prep scratch
  directory and the planned live-language-switching document outside this release.
- The public changelog contains only the two player-facing fixes. Package
  publication and release tagging/version advancement await portal confirmation.
