# Cut buildings

Enable **Cut buildings** in the BDT settings under **Build Behaviors**. In a normal game it uses the vanilla Cut research. In Sandbox you can use it immediately. Cut buildings defaults to off in new saves. Existing Sandbox saves that used BDT Instant build mode keep the ability to cut completed buildings.

Press `X` over a building or `Ctrl+X` to start an area selection. The **Buildings**, **Surfaces**, and **Decals** buttons independently control what goes into the cut and what follows it when you paste. Surfaces includes player-laid pavement and pending paving designations. Select the whole length of a completed transport to move it.

Cut starts demolition for ordinary completed buildings. A unique building stays in place while you choose its destination. Confirming the destination starts demolition, and the replacement is ordered after the original disappears. Construction and demolition use the usual game rules. In Sandbox, you can enable **Instant build mode** separately to finish those jobs immediately.

If you cancel demolition for the only unique building in the original batch, it stays at its original location and the rest of the move continues. If the original batch had several unique buildings, canceling one of their demolitions cancels the whole move and stops the batch’s remaining demolition. Anything already demolished stays gone. Use `Ctrl+Z` while the move is still in your undo history to order its restoration.

A blocked move retries when its destination becomes legal. Use **Retarget blocked move** in BDT settings to pick a different destination for the latest blocked move.

Paste the selection to place it elsewhere, or press `Esc` to drop it. Demolition you already ordered continues. Press `Ctrl+Z` to undo the cut or move and restore the original location through the usual game rules. Confirmed moves that are still waiting for construction are carried across save and reload. A held preview drops on reload, and you can undo a cut that already changed the world.

[Runtime uniqueness API for mod authors](../api/building-move-uniqueness.md)
