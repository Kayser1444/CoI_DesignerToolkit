# AoE transport removal — tickets 05 and 06 integration test script

Run these cases in a disposable sandbox save with BDT logging enabled. For each
case, inspect the log for unexpected command-result errors or uncaught
exceptions. Unless a case says otherwise, all selected entity-type toggles are
enabled.

## Selection and live dialog

1. Drag across only one tile of each supported entity type, including the middle
   of a long belt or pipe. Confirm every intersected entity appears and the whole
   parent transport appears once, matching native Upgrade and BDT Throughput.
2. Drag across several pieces of the same transport and across a whole entity.
   Confirm each stable entity ID contributes once.
3. Select Transport, Lift, Zipper, MiniZipper, and Sorter targets containing
   different products. Confirm every present type starts enabled and the live
   entity count, product breakdown, buffered total, active regular-order count,
   quick cost, and affordability match the enabled types.
   Confirm each type row has an entity icon and the product breakdown uses
   vanilla icon-and-quantity tiles with product tooltips.
4. Toggle each type off and on. Confirm all live values and both actions use the
   same filter, while no removal state changes merely from toggling. Enabled
   types must be highlighted in the world and disabled types must immediately
   lose their highlight.
5. Open the dialog, then change buffer contents. Confirm product quantities and
   quick cost update. Start or cancel an inspector regular-removal order and
   confirm the active-order count updates.
6. Open the dialog, then demolish one selected target. Confirm live values omit
   it. Trigger either action and confirm the remaining original selection is
   processed without a player-facing error.
7. Place a sandbox product source and sink inside the drag. Confirm neither is
   included.
8. Confirm Remove, Quick remove, and the conditional Cancel remove button have
   equal widths, equal heights, and even gaps at every displayed quick-removal
   cost. Confirm the footer has no redundant Close button; the title-bar X
   closes the dialog.
9. Select a transport through only a partial segment. Confirm the dialog
   highlights its normalized whole parent, and closing the dialog removes every
   entity and trajectory highlight.

## Quick removal

1. Populate mixed entity types with quantities that exercise per-entity cost
   rounding. Confirm the dialog total equals the sum of individual inspector
   costs, not a cost calculated from the aggregate quantity.
2. With sufficient Unity, quick-remove the mixed selection. Confirm every
   product is cleared, Unity decreases by the displayed total, and successful
   targets lose active regular-removal orders.
3. Make the total unaffordable while at least one individual target remains
   affordable. Confirm the button is disabled and, if the command is scheduled
   before the balance changes, execution clears nothing, consumes no Unity, and
   leaves all regular orders intact.
4. Enable sandbox **Ignore lack of Unity**, repeat with insufficient balance,
   and confirm the button remains enabled, all targets clear, and Unity ends at
   zero.
5. Include an empty entity. Confirm it is a no-op and contributes no cost.
6. With a failure-injection adapter, make cost preflight throw for one target.
   Confirm it is logged and independent valid targets still execute.
7. With a failure-injection adapter, make clearing throw. Confirm that target is
   not charged and retains its regular order, while independent valid targets
   clear and are charged.

## Regular removal

1. Select a mixture of inactive and active targets. Trigger **Remove** and
   confirm inactive targets start while active targets receive a fresh request
   scoped from their current contents; no target is cancelled by this action.
2. Repeat a selection that intersects the same transport several times. Confirm
   it toggles only once.
3. Confirm native Transport targets use vanilla removal state and never appear
   in BDT's persisted regular-order cache.
4. Confirm empty targets and targets with only unsupported contents remain
   ungated and create no persisted order.
5. Start orders containing truck-loadable products, save, quit, and reload.
   Confirm BDT orders resume and native Transport orders retain vanilla state.
6. Confirm **Cancel remove** is visible when any enabled target has an active
   order. Trigger it and confirm all active orders among enabled targets cancel,
   jobs/reservations are removed, remaining products stay in place, and
   input/output gating is released.
7. Confirm **Cancel remove** is hidden when no enabled target has an active
   order, including when all types containing active orders are toggled off.

## Result

Record the game version, BDT build, save name, and pass/fail result beside each
case when executing this script. Cases requiring a failure-injection adapter may
remain blocked until a compatible fixture mod is available; they must not be
silently treated as passed.
