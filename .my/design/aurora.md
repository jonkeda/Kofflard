# Change: the Aurora look

Date: 2026-10-31. Status: built, on Windows; Android and iOS not yet looked at.

Kofflard looked like an office form: a teal title bar, grey page, white boxes. Aurora gives it a night sky in dark mode and a dawn sky in light mode, with glowing rings around the search, a dot slider for the adventure, and cards tinted by lane. The mock-ups are on the design canvas (claude.ai/artifact/2JsCoSm3nwxwTWBNaVRBRJ, boards "D · Aurora"). It restyles design §3 in [kofflard-design.md](kofflard-design.md) and the look of [search-page-changes.md](search-page-changes.md) and [results-list-changes.md](results-list-changes.md); no behaviour, page structure or AutomationId changes.

## How it is drawn

No SkiaSharp and no background image: everything is MAUI shapes and brushes.

- **The sky.** Every page gets the `AuroraPage` control template (Styles.xaml): the base colour and three soft glows, circles filled with a radial gradient that fades to transparent. They are circles because Windows takes a radial gradient's radius from the width alone; an oval is cut off before it fades. The title bar takes the base colour, so it melts into the sky.
- **Light and dark.** Every colour is a `…Light`/`…Dark` pair in Colors.xaml, picked with `AppThemeBinding`. The app follows the system theme.
- **The orb.** Three ellipses: a ring in a teal → violet → pink gradient, a dotted inner ring and a glass disc; the question and the name field sit on top. The ring's glow is two wide, faint strokes under it. A `Shadow` on a shape is drawn as a square on Windows, so shapes have none.
- **The slider.** The adventure choices are still radio buttons (`AdventureChoice`); each draws a dot and its name, over one track line. The chosen dot is bigger, brighter and has a halo. On Windows, WinUI's own radio template kept room for its circle and pushed each stop off its column; `BareRadioButton` (Platforms/Windows/App.xaml, set from MauiProgram) is just the content. The adventure icons are no longer shown.
- **Buttons.** The main button is a gradient pill (`Button`); `SoftButton` is glass with a thin edge; `LinkButton` reads as a link (the star, More →, Get a key).
- **Lanes.** Close is amber, Stretch teal, Leap violet. A card's background fades from its lane's tint in the top corner, with an edge in the lane's colour; the lane header is a glass chip in that colour. Both follow `LaneName` with data triggers.
- **Header icons.** The star and the cog are white on the dark sky and dark (`star_dark`, `settings_dark`) on the light one.

## Left out

- The artist map (constellation) from the desktop mock: it needs a `GraphicsView`, and the phone layout dropped it.
- The photo placeholder with initials on each card: there are no photos yet.
- The Outfit font: the app keeps the platform font.
- A two-column layout on a wide window: the search page keeps one column, at most 560 wide.
