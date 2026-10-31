# Transport product-removal adapters

BDT supports compatible third-party transport entities through explicit, prototype-keyed adapter registration. It does not discover or mutate arbitrary modded buffers.

Register an `ITransportProductRemovalAdapter` factory with `TransportProductRemovalAdapterRegistry.Register(prototypeId, factory)` and retain the returned `IDisposable` registration handle for the lifetime of your mod integration. The adapter must support the complete regular-removal, quick-removal, and inspector-control contract and must never identify an infinite product source or sink as eligible.

To add the combined removal interaction to a custom inspector, call `TransportProductRemovalUi.AddCombinedRemovalControl(parent, scheduler, entityProvider)` at the appropriate product-buffer panel insertion point.

Important contract requirements:

- `GetBufferedProducts` returns a complete snapshot of all relevant internal buffers.
- `GetProductQuantity`, `RemoveProduct`, and quick-removal accounting remain consistent with that snapshot.
- `SetRegularRemovalActive` idempotently gates normal input/output without changing the player's paused or enabled state.
- Construction validity and destroyed state are reported accurately.
- Registration metadata and adapter instances are runtime-only and are not serialized.

The built-in mini-zipper adapter includes the optional b624 reserve pile in
content snapshots, removal limits and returned products. If selective removal
empties the reserve's input slot, it promotes the retained reserve into that
slot with its original arrival step so native sending can continue. It resets
reserve ownership when empty and preserves the input-only path on older builds.
Native `MixedPile.AggregateInto` expands mixed members only; plain piles must
also be included explicitly when enumerating inventory.

The integration seam is public, but v0.9.0 has not been validated against a third-party adapter implementation. Integrators should test entity-level regular and quick removal, area selection, save/reload restoration, cancellation, destruction, and mod removal before shipping support.
