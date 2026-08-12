# 01 — Entity-level removal vertical slice for Zippers

**What to build:** Add the shared removal domain and make Zippers support both regular and quick product removal through the normal entity-level interaction. The existing zipper-specific quick-removal path becomes part of the shared removal behavior, while vanilla Transport behavior remains authoritative and unchanged.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] A constructed, valid Zipper can start and cancel a one-shot regular-removal order for its current buffered products.
- [ ] Regular removal covers all supported Zipper buffers, immediately discards eligible non-waste products, exposes truck-loadable products through normal clearing jobs, and leaves unsupported products in place.
- [ ] Active regular removal gates normal input/output without changing the player’s enabled or paused state, and cleanup is idempotent on cancellation, destruction, transformation, or completion.
- [ ] Quick removal clears all supported Zipper buffers through vanilla product-accounting paths at the vanilla-equivalent cost.
- [ ] A successful quick removal supersedes an active regular order; rejected or unaffordable quick removal leaves it intact.
- [ ] Entity-level UI uses one combined regular/quick removal control with live state and cost, with no duplicate zipper quick-removal control.
- [ ] UI callbacks schedule serializable simulation commands and do not mutate simulation state directly.
- [ ] Domain tests cover empty contents, mixed product eligibility, cancellation, quick supersession, affordability rejection, and adapter cleanup.

