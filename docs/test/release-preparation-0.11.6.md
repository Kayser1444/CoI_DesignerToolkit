# DesignerToolkit 0.11.6 release preparation — 2026-10-31

- Release scope is b624 mini-zipper reserve accounting, previews, selective/quick
  removal, and retained-reserve promotion. Keep working version 0.11.6 and
  max verified game version 0.8.8. No new UI strings need translation; current
  source coverage is complete for the seven translated locales.
- Debug and Release implementations build cleanly against complete stable
  0.8.7d/b619 and installed experimental 0.8.8/b624 references.
- Update43Contracts passes actual native mixed/plain pile removal, all four
  adapters, reserve-sensitive snapshots, cached totals, conservation, retained
  order, promotion and idempotence.
- Native b624 save/reload, selective and quick removal, throughput and supported
  exclusion tests are recorded in `b624-mini-zipper-reserve-2026-10-31.md`.
- Final Release load with KPIE passes in PID 199460, log
  `26-10-31_12-15-02_3009.log`, BDT DLL timestamp 12:09:03. Native readiness,
  zero error/assertion rows and an empty temporary-probe command index are
  verified. Closed normally without saving; original save hash is unchanged.
- Authenticated Hub mod 1081 Edit → raw Markdown matches the clean committed
  local source before editing (23,696 Unicode code points; no terminal newline).
  Retain that existing Git checkpoint. The draft documents reserve handling and
  corrects its old minimum-version text to the existing 0.8.5 manifest value.
  All Hub image links remain unchanged. No live page or version is published.
- ATD/AFD require no player-facing release: their API-startup changes are
  Debug-only. Shared source changes are verified through all four consumers.
