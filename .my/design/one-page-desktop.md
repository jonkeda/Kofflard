# Change: one page on the desktop too, one page with two layouts

Date: 2026-10-31. Status: built 2026-10-31 (see [As built](#as-built)); checked headless and on Windows, not on a tablet or by resizing a window.

The new desktop design (the mock below, the canvas board "D · Aurora light — Desktop",
claude.ai/artifact/2JsCoSm3nwxwTWBNaVRBRJ) is a one-pager, as the phone is: the search on the left, the
results on the right, side by side. So there is no longer a reason for two page structures. This change
makes **one page and one view model for both**, with **two layouts**: the phone's ([single-page-phone.md](single-page-phone.md))
for a narrow window, and this two-pane one for a wide window.

It replaces the desktop's two pages (Aurora D's search page that pushes a results page, and
[desktop-aurora-changes.md](desktop-aurora-changes.md) #1, "no results stay on the search page"), which a
one-pager makes moot: nothing is pushed, so an empty answer simply shows where the cards would.

## The mock

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Kofflard                                                                    (☆)  (⚙)  │
│                       │                                                                │
│   ╭──────────────╮    │  Like Nick Drake                       ○ Vashti   ○ Bert       │
│  (  Who do you    )   │  English folk singer-songwriter,          ╲      ╱             │  the artist map
│  (  like?         )   │  1969–74                        ○ Ali ── (ND) ── ○ Ichiko      │
│  ( [Nick Drake ]  )   │                                          ╱      ╲              │
│   ╰──────────────╯    │                                   ○ Adrianne  ○ José           │
│                       │                                                                │
│ [  Find musicians  ]  │  ● CLOSE (2)         ● STRETCH (2)        ● LEAP (2)           │
│                       │  ┌──────────────┐    ┌──────────────┐     ┌──────────────┐     │  three lanes,
│ RECENT                │  │☆ Vashti … More│   │☆ Adrianne …   │    │☆ Ali Farka … │     │  side by side
│ (Khruangbin)(Arooj)   │  │[VB] Fragile…  │   │[AL] Hushed…   │    │[AF] Bridge:… │     │
│                       │  └──────────────┘    └──────────────┘     └──────────────┘     │
│                       │  ┌──────────────┐    ┌──────────────┐     ┌──────────────┐     │
│                       │  │☆ Bert Jansch  │   │☆ José …       │    │☆ Ichiko Aoba │     │
│                       │  └──────────────┘    └──────────────┘     └──────────────┘     │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

| Part | What it is |
| --- | --- |
| **Header** | The Kofflard wordmark; ☆ Favourites and ⚙ Settings as round icon buttons. |
| **Search pane** (left, fixed width, about 290) | The orb with "Who do you like?" and the name inside it; Find musicians; Recent chips. A thin line divides it from the results. |
| **Heading** | "Like Nick Drake" and the one-line summary. |
| **Artist map** (top right) | The searched musician in the middle (ND), the picks around it, coloured by lane, joined by lines. |
| **Lanes** | Three columns side by side, each headed by a dot in its colour, its name and a count badge. Cards stack in their column. |
| **Card** | ☆, the name, More →, a photo (initials for now), why (or the bridge), tags, known for. |

## What the mock differs on from what is decided

The mock was drawn before some decisions. The decisions stand ([Decided](#decided-2026-10-31)):

| In the mock | Decided before | Here |
| --- | --- | --- |
| Close, Stretch, Leap | Safe, Curious, Wild ([adventure-as-outcomes.md](adventure-as-outcomes.md)) | Safe, Curious, Wild |
| ✓ after a verified name | the ✓ marks go ([desktop-aurora-changes.md](desktop-aurora-changes.md) #3) | no ✓ |
| No ✕ on the Recent chips | ✕ removes a recent search | keep ✕ |
| No lane toggles | the phone's toggles set what is asked and shown | the lane headers are the toggles (below) |
| A photo with initials | Aurora left photos out: there are none yet | the initials in the lane's colour, as drawn, in the wide layout only |
| No "More like this…", no trail | both exist on the phone | under the columns; above the heading |

## One page, two layouts

```
              DiscoverViewModel (one)
            ┌─────────────┴─────────────┐
   narrow: PhoneLayout          wide: DesktopLayout
   list, dock at the bottom     search pane | heading, map, three lane columns
```

- **One page**, `DiscoverPage`, and one view model, `DiscoverViewModel`, as built for the phone: the
  search, the results (one `ResultsViewModel`, loaded again per search), the lane setting, the trail.
- **Two layout views**, `Views/PhoneLayout.xaml` and `Views/DesktopLayout.xaml`, both bound to that view model.
  The page shows one, by the window's width:
  - **narrow** (under 900): the phone's layout;
  - **wide** (900 and up): the two-pane layout.

  It switches when the window is resized, so a Windows window dragged narrow becomes the phone layout, and a
  tablet turned from landscape to portrait changes with it. The idiom no longer decides; `KOFFLARD_LAYOUT`
  still forces one, for tests.
- **What goes**: `SearchPage`, `ResultsPage`, the `results` route, `SearchLauncher`, `RunningSearch`, and the
  stacked-page problem with them ([stacked-pages.md](../rca/stacked-pages.md)). Favourites, Settings and Connect
  stay pushed pages.
- **Windows** opens wide (1280 × 800) instead of phone-sized.

### The lanes as toggles, on the desktop

The phone's toggles live in its dock. The desktop has no dock, and the mock's lane headers (dot, name,
count) already look like chips, so **the lane headers are the toggles**. A header that is off is
faded and its column shows "Wild is off: click to ask for wild picks"; clicking it turns it on, which asks
for that lane alone, as on the phone. Both layouts read and write the same `lanes` setting, so the choice
carries over when the window changes width.

### States in the results pane

| When | Results pane |
| --- | --- |
| Nothing searched | "Who do you like?" hint, and the empty map with only the centre |
| Searching | the heading, and the columns filling as cards arrive |
| Nobody found, an error, "Did you mean" | the message (and Fix connection, or the candidates) in place of the columns |
| Results | as drawn |

## The artist map

The searched musician in the middle, each shown pick around it on its lane's side (Safe up and left,
Curious below, Wild right), joined by thin lines in the lane's colour; a few faint dots for depth.

- Drawn with MAUI shapes in an `AbsoluteLayout` (ellipses, lines, labels), as Aurora draws the orb: no
  `GraphicsView`, no SkiaSharp. Node positions come from a fixed template for up to four picks per lane.
- A node is a button: clicking it does what the card's "More →" does.
- It shows only in the wide layout, and only the lanes that are on.

## How

### Core
- `DiscoverViewModel` stays as built. It gains `IsWide` (set by the page from its width) only if a
  binding needs it; the layouts themselves need nothing more.
- `ResultsViewModel` gains `LaneColumns`: the three lanes always, each with its cards (possibly none) and
  whether it is on, for the desktop's columns. `Lanes` (the shown groups) stays for the phone's list.
- `ArtistMap` (new, pure): the node and line positions for the shown cards, unit-tested.

### App
- `DiscoverPage.xaml` holds a `ContentView` whose content is the layout for the current width
  (`SizeChanged`, with a little hysteresis so it doesn't flicker at 900).
- `PhoneLayout.xaml`: today's DiscoverPage content, moved.
- `DesktopLayout.xaml`: `Grid` of two columns (290, *): the search pane (the orb from Aurora's search page,
  Find musicians, Recent); then heading and map in a row, and the three lane columns, each a
  `CollectionView` of its lane (`Results_Safe`, `Results_Curious`, `Results_Wild`), with the card template
  shared with the phone. Twelve cards need no virtualisation, but a `CollectionView` per column keeps the
  card click (selection opens, .my/rca/card-click.md) and Bruinet's list support.
- `SuggestionCardView` gains the initials photo (a `Border` with the lane's gradient), shown in the wide
  layout only: the phone's cards stay compact.
- `App.xaml.cs`: Windows opens at 1280 × 800.

### Ids

| Kept, both layouts | New, wide layout |
| --- | --- |
| `DiscoverPage`, `Search_Name`, `Search_Go`, `Search_Recent`, `RecentRow_Remove_{artist}`, `Discover_Heading`, `Discover_Back`, `Suggestion*`, `Lane_{lane}` (the toggles: the dock's on a phone, the column headers on the desktop) | `Results_Safe`, `Results_Curious`, `Results_Wild` (the columns), `Discover_Map`, `MapNode_{name}` |

## Tests

| Suite | Change |
| --- | --- |
| Unit | `ArtistMap` positions; `LaneColumns` (every lane, on or off, its cards). The search and results tests stay; `SearchLauncher` and `RunningSearch` tests go with them. |
| UAT | One set of scenarios, run in **both** layouts: the desktop fixture forces wide, the phone fixture narrow. The desktop scenarios that navigate ("I should be on the Results page") read the one page, as the phone's already do (`Layouts`). Layout-only scenarios are tagged `@phone` or `@wide` (the map, the columns). |
| UI | The results page object's `Suggestions` reads the three columns in the wide layout (one list per lane), the one list in the narrow. The stacked-page diagnostic goes, with the stack. |
| Device | Windows at the merge (AD-032): resizing across 900 switches layouts and keeps the search. |

## Decided (2026-10-31)

1. **Safe, Curious, Wild**, not the mock's Close, Stretch, Leap.
2. **No ✓** on the cards, as decided before.
3. **Clicking a node in the map** searches that musician, as "More →" does.
4. **The initials photo** shows in the wide layout only, for now.
5. **The lane toggles on the desktop** are the column headers.
6. **The layouts switch at 900** wide; a tablet in portrait gets the phone's layout.

## As built

What differs from the plan above:

- **The columns** are no `CollectionView`s: one `ScrollView` (`Results_Items`) holds the three lanes, each a
  `BindableLayout` of cards, so the page objects read headers (`LaneHeader`) and cards (`Suggestion`) in it as
  they do in the phone's list. A card in a column takes its own click (`SuggestionCardView.OpensOnTap`, a tap
  gesture), as it is no list item; tests tap it through the card's gesture sink, which now names Tap too.
- **The column headers** read "Safe (6)", as the phone's do; a lane that is off shows "Wild (off)" and what a
  click does. The headers are the toggles (`Lane_{lane}`).
- **The map's dots** are round `Border`s with a tap; a `Button` was drawn square on Windows.
- **A lane turned on while a search runs** is asked for once the search ends. Found by PHONE.01.2: the search
  step no longer waits for every card, and the toggle came while the answer was still loading.
- **The test suites** start every test with a new install's lanes: all three, Wild too. One set of page objects serves both
  layouts: `SearchPage` and `ResultsPage` both root at `DiscoverPage`.
- **Gone**: `SearchPage`, `ResultsPage`, the `results` route, `SearchLauncher`, `RunningSearch`, the card's
  "Searching…" note, and the stacked-page diagnostic test. Favourites' "More →" goes back to the one page and
  searches there.
- **Not checked**: switching layouts by resizing a window, and a tablet. Every test forces a layout.

## Not in this change
- Real photos of musicians.
- Favourites, Settings and Connect: they stay pushed pages, in the Aurora look.
- The AI request, the mix and the data.
