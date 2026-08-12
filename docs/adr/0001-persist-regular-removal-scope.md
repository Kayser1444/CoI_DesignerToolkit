# Persist the scope of regular removal orders

BDT persists each active regular removal order as an entity ID, prototype ID, and remaining quantity per product. On restoration, removal is capped to the lesser of current and persisted quantity; missing quantity is considered fulfilled and is never awaited. This preserves one-shot semantics across saves and periods when BDT is absent without turning an old order into a standing request for future products.
