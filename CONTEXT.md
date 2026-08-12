# Blueprint Designer's Toolkit

BDT extends Captain of Industry's blueprint-design and area-editing workflows while preserving vanilla-compatible saves and interactions.

## Language

**Removal control**:
The vanilla-style inspector interaction that toggles a regular removal order and exposes quick removal with its live Unity cost in an interactive floater.
_Avoid_: Trash button, quick-remove button

**Removal scope**:
The per-product quantities captured by a one-shot regular removal order. Missing quantities are considered fulfilled; future products never enlarge the scope.
_Avoid_: Target contents, removal quota

**Product**:
Any material transported or buffered by an entity, including raw materials such as ore. This deliberately follows vanilla terminology.
_Avoid_: Goods, raw materials (as the general category)
