# Environmental overlays

BDT's environmental overlays help inspect pollution and radiation while testing a design.

## Pollution overlay

The pollution overlay displays averaged pollution sources as local labels and heat-map glows. It can show the supported air, ground/water, vehicle, and ship categories, with independent visibility filters and configurable averaging.

## Radiation overlay

The radiation overlay displays the amount of unsafe radioactive product held locally by an entity. It samples at the same daily boundary used by vanilla radiation accounting, then averages those daily samples over the configured sliding window.

Radiation sources include supported machines and storage buffers, connectors, lifts, flat conveyors, and trucks. Reactor and radioactive-waste-storage buffers are treated as safe storage, matching vanilla's radiation handling, and are not displayed as local sources. Transporting a barrel through a connector or flat conveyor does not create an additional source; the overlay reports the unsafe inventory present at the daily sample point.

Labels use the translationless `#N.D#` format. With the glow option enabled, the configurable heat-map glow is applied to the source entity or vehicle, not to the label. Stronger sources receive a proportionally stronger glow relative to the strongest currently visible source; the radiation heat map uses zero as its baseline.

The pollution glow color defaults to white. Set `pollution_glow_color` in BDT's `config.json` to `white`, `brown`, `purple`, or a six-digit RGB value such as `#8B4513`. During a game, use `bdt_set_pollution_glow_color white|brown|purple|#RRGGBB`; omitting the value reports the current color. The command changes the current game's setting immediately, and it is saved with the BDT settings state.

### Settings

- **Radiation overlay** toggles radiation labels and the heat map. The default hotkey is `Alt+R`.
- **Enable heatmap glow effect** toggles the pollution entity/vehicle glow separately from the labels.
- **Averaging period** accepts `0` to `360` game days and defaults to `30`. Setting it to `0` disables radiation data collection.

The overlay remains visible while the BDT Mod Settings window is open. Deleting a source entity also removes its glow immediately.
