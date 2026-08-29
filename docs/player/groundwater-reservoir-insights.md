# Groundwater & Reservoir Insights

BDT adds a stats icon to the **Reserve** header in Groundwater pump and other virtual resource well inspectors (such as oil wells and modded reservoirs). Select the icon to open the rich tooltip.

The tooltip includes:

- the last year's average monthly resource draw
- the maximum sustainable monthly draw after year 10 (groundwater only; hidden for non-replenishing reservoirs)
- the expected yearly reserve change, marked as filling, depleting, balanced, or idle
- an estimate of the expected years remaining when the reservoir is depleting
- a rolling monthly chart with the current level and up to 12 monthly snapshots, colored according to the product's characteristic color
- a yearly chart with the current level and up to 10 January 1 snapshots

Reserve levels below the pump's low-reserve threshold are shown with warning coloring. The years-remaining indicator uses a stronger critical color below 10 years.

The sustainable-draw estimate uses the post-year-10 replenishment rate for the current **Weather** difficulty. The **Rainwater yield** setting affects Rainwater Harvesters only, not groundwater. Non-replenishing reservoirs (oil, natural gas, etc.) omit the sustainable-draw KPI row and calculate depletion directly from actual draw.

Newly tracked reservoirs need up to 12 completed months before the average-draw estimate is fully populated. History and monthly draws are retained across save and load sessions while BDT remains installed.

