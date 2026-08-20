# Pre-color pipes

BDT can give empty fluid and molten pipes an initial color based on their connected upstream sources.

Open the BDT settings panel and enable **Pre-color pipes** under **BUILD BEHAVIORS**.

BDT follows connected pipes and compatible transport links upstream to find fluid or molten-product sources. When multiple source fluids are found, their transport colors are blended equally. The search is bounded, so a loop or unusually long network can leave an isolated pipe unresolved.

Pre-coloring only seeds the initial color of an empty pipe. Once the pipe contains products, vanilla transport coloring takes over and fades toward the color determined by the current contents and flow. Changes to connected topology, recipes, source products, or construction refresh the affected connected cluster, including while the game is paused.
