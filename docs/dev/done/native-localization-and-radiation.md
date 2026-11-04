# Native localization and build 626 radiation shielding

BDT uses native mod localization on 0.8.8. All 211 string IDs start with
`DesignerToolkit.` and use a valid single string-ID segment. Shortcut translations
use `DesignerToolkit.Kb_<existing shortcut ID>__label` and `__tooltip`; shortcut
binding IDs, superseded IDs and saved preferences are unchanged. All supplied
translation text and formatting are preserved. Portuguese and Chinese bundles are
named `pt_BR.json` and `zh_Hans.json`.

The native loader merges bundles; the current payload excludes the legacy private
dictionary/rebind adapter. The earlier payload retains that adapter, native locale
filename selection and unprefixed Kb_ runtime aliases for the older KbAttribute.
LocStr follows native language changes, but formatted text and existing UI controls
remain snapshots. This does not implement automatic repainting of every open UI.

The pending work order has only its string identities migrated; its existing
translation values remain intact. Do not apply it as a request to replace completed
translations. Authoring skip keys now use the migrated identities.

## Radiation

Build 626 introduces `CoreProtoTags.RadiationShielded` without changing the assembly
version. BDT resolves the optional field once by reflection and excludes tagged
ordinary product holders from new radiation samples. This covers native Chemical
Plant II, uranium enrichment and nuclear reprocessing, and other tagged prototypes.
An IShieldedEntity with active shielding retains its own per-product contract; a
prototype tag must not imply that all of such an entity's contents are safe.

Earlier 0.8.8 builds lack the tag and keep their existing sampling behavior;
0.8.7d does not compile the new path. No direct reference to the new field is emitted.
Existing averaging history decays normally as new daily samples arrive, so an old
positive average need not disappear immediately. This change does not modify
vanilla radiation, product inventory, gameplay penalties or save state.

Native localization/shortcut, legacy rebind and actual sampler contracts are
described in AutoHelpers/tools/ModLocalizationContracts and BDT/tools/Update43Contracts.
