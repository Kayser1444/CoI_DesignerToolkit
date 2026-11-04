# Log-feedback fixes and build 626 radiation verification

Verified 2026-11-02. This is development verification, not a release record.

## Changes

- ATD: migrate 210 localization IDs to AutoTerrainDesignations ownership; use
  native translations on 0.8.8 and preserve late rebind on 0.8.7d.
- BDT: migrate 211 localization IDs, including shortcut text, to DesignerToolkit
  ownership. Shortcut binding/preference identities are unchanged. Native locale
  filenames are used for Portuguese and Chinese. All existing bundle text and
  format values were checked against Git and preserved exactly. The pre-existing
  pending work order retains its values, with only key identities migrated.
- Shared helper: native shortcut text uses its owning mod prefix; older games
  receive the Kb_ aliases expected by legacy KbAttribute.
- KPIE: the six manual-worker graphics registration sites use empty engine and
  movement sound arguments on 0.8.8, retaining EMPTY for older games. Existing
  native worker impact registration is unchanged.
- BDT radiation: resolve the optional b626 RadiationShielded tag once at runtime.
  Exclude tag-protected ordinary holders from new samples without introducing a
  hard reference absent on earlier 0.8.8 builds. Do not let a prototype tag override
  an active IShieldedEntity's own per-product contract. Existing averages decay
  as new daily samples arrive; old history is not cleared.

## Automated evidence

- Before fixes, the generic localization runner rejects ATD's old IDs, the
  production radiation fixture counts a holder the native b626 manager identifies
  as protected, and native vehicle graphics constructors warn on EMPTY arguments.
- After fixes, all five current-game Debug builds pass with zero warnings/errors.
  ATD/BDT/KPIE and AH also build against preserved 0.8.7d references without
  replacing current-game deployment. BDT builds and runs its sampler/transport
  fixtures against b622, where the optional shielding tag does not exist.
- Native localization: ATD 7,590 checks, BDT 6,963 checks (including actual shortcut
  handles), unchanged AFD pilot 3,004 checks. Every supplied ATD locale includes
  Korean. Covers ownership/IDs, format tokens, typed strings, early copies,
  language changes, missing-key fallback and captured versus deferred text.
- Legacy Swedish, Portuguese and Chinese checks pass in fresh processes for
  each ATD and BDT DLL: all static fields rebound and older BDT shortcut aliases
  translated. No locale text was regenerated or machine-translated.
- BDT radiation contracts pass native tag protection, untagged inventory, distinct
  prototypes, active dynamic-shielding precedence and absent-tag fallback. Transport
  mixed/plain buffer and mini-zipper regressions, groundwater history/initialization
  contracts, and config/hotkey localization validation pass.
- KPIE native graphics warning fixture passes with production arguments; its
  baseline mode deliberately reproduces four warnings with EMPTY. Separate native
  worker sound prototype/tuning checks pass. Audible playback remains untested.
- Root and selected-payload game references resolve for all four mods, including
  the supplemental nested-type check for ATD DumpingJob.Factory.EnqueueJob.

## Live integration check

Managed test PID 23848 loaded the existing paused save on build 626. Game ID
2983648954427638228, session ID 1755830305895322429, tick 110437. API readiness
used /api/v1/game-info and the native Loaded game marker. The log is
`%APPDATA%/Captain of Industry/Logs/26-11-02_08-10-47_8925.log`.

The log confirms the rebuilt DLL timestamps and native English localization for
AFD/ATD/BDT. It has zero reported invalid-ID/cannot-be-translated warnings, zero
ignored SoundBindingsRegistrator argument warnings, zero initial groundwater
discovery warnings and zero exception/error headers. The process closed normally
without forced termination or a requested save.

Separate remaining warnings: ATD's eight prototype-generated legacy strings,
KPIE's legacy localization notices and 14 unowned translation entries, a null
string during save deserialization, and a regenerated terrain fingerprint mismatch.
These were not silently classified as fixed or as new regressions from this task.
The KPIE localization ownership issue is outside the reported sound cleanup.

No populated radioactive plant was visually checked in this paused save. Corrected
sampling is established by the actual production-sampler fixture; a live plant
overlay comparison after daily samples remains a useful visual follow-up.

## Delivery state

Source and installed Debug payloads include these changes. The previously prepared
0.11.7 ZIP does not. After this verification, the owner confirmed that 0.11.7 was
already released; the working version is now 0.11.8 and these follow-up changes
belong to its unreleased entry. No package rebuild or publication was performed.
Pre-existing AFD/localization/settings and other unrelated working-tree changes
were preserved.
