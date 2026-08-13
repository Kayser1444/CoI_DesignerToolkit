# Transport product removal

BDT adds regular product removal to lifts, connectors, sorters, and balancers and provides one area tool for regular removal, quick removal, and cancellation.

## Remove products from one entity

Open a supported entity inspector and use its removal control:

- Regular removal discards eligible non-waste products immediately and exposes truck-loadable products to normal vehicle collection.
- Quick removal clears all buffered products immediately for the displayed Unity cost.
- Cancelling regular removal restores normal input and output without returning products already removed.

While a regular order is active, the entity does not accept or output products. The order is one-shot: products arriving later do not enlarge its saved removal scope.

## Remove products from an area

Activate **Remove products in area** from the toolbar or with `Alt+Backspace` by default, then drag over the desired entities. An entity is selected when the area overlaps at least one of its tiles.

The dialog shows selected entity types and their counts. Disable a type to exclude all entities of that type. The world highlight, product preview, action counts, Unity cost, and affordability update immediately.

- **Remove** sends regular removal orders to eligible selected entities, including refreshing an existing order from the entity's current contents.
- **Quick remove** immediately clears products from eligible selected entities.
- **Cancel** cancels active regular removal orders among the enabled entity types.

Product sources and sinks are excluded. Empty entities and actions that would do nothing remain visible but disabled.

## Saving and mod removal

Active BDT regular-removal orders survive normal save, quit, and reload cycles while BDT remains installed. Removing BDT and then saving the world without it causes the game to purge BDT's per-mod cache; reinstalling BDT later does not restore those earlier orders.
