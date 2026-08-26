# Groundwater Reservoir Insights

BDT adds a stats icon to the **Reserve** header in Groundwater pump inspectors. Select the icon or the header to open the rich tooltip.

The tooltip includes:

- the last year's average monthly groundwater draw
- the maximum sustainable monthly draw after year 10
- the expected yearly reserve change, marked as filling, depleting, balanced, or idle
- an estimate of the expected years remaining when the reservoir is depleting
- a rolling monthly chart with the current level and up to 12 monthly snapshots
- a yearly chart with the current level and up to 10 January 1 snapshots

Reserve levels below the pump's low-reserve threshold are shown with warning coloring. The years-remaining indicator uses a stronger critical color below 10 years.

The sustainable-draw estimate uses the post-year-10 replenishment rate for the current **Weather** difficulty. The **Rainwater yield** setting affects Rainwater Harvesters only, not groundwater.

Newly tracked reservoirs need up to 12 completed months before the average-draw estimate is fully populated. History and monthly draws are retained across save and load sessions while BDT remains installed.
