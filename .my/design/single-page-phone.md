# Change: one page on a phone, search at the bottom

Date: 2026-10-31. Status: built 2026-10-31 (see [As built](#as-built)); checked headless and on Windows, not yet on an Android or iOS device.

Today a search is two pages: the search page (the orb, Find musicians, Recent) pushes a results page, and tapping a card pushes another results page on top. On a phone this means the name field is at the top, out of thumb reach, and every chained search adds a page to the stack. This change folds search and results into one page **on phones (Android and iOS)**: the list fills the screen and the search sits at the bottom, as in a chat or a maps app. Favourites, Settings and Connect stay separate pages.

Kofflard has two designs: this one for **phones**, and **desktop and tablet** (Windows, iPad, Android tablets), which is Aurora D: the search page and the results page from [aurora.md](aurora.md), changed by [desktop-aurora-changes.md](desktop-aurora-changes.md). This change does not touch that design. Its ✓-free cards and ✕ on Recent chips apply here too, as the templates are shared.

It changes design §3.1 (screens) in [kofflard-design.md](kofflard-design.md) for phones. It takes the Aurora look. Aurora removed the adventure slider; the phone gets [three lane toggles](#safe-curious-wild-one-filter-for-the-ask-and-the-list) instead, one filter for what is asked and what is shown. The AI request, the cards and the data stay as they are.

The mock-ups are on the design canvas (claude.ai/artifact/2JsCoSm3nwxwTWBNaVRBRJ, row "E · Phone — one page"): the results in dark and light, with the Safe, Curious and Wild toggles working, and the first open with Recent. Their source is in [canvas/project](canvas/project).

## Before and after

```
Before: two pages                                 After: one page
┌──────────────────┐   ┌──────────────────┐       ┌────────────────────────┐
│ Kofflard     ★ ⚙ │   │ ← Nick Drake     │       │ Kofflard           ★ ⚙ │
│    ╭────────╮    │   │ Like Nick Drake  │       │ ‹ Nick Drake           │  ← where you came from
│   ( Who do   )   │   │ SAFE             │       │ Like Vashti Bunyan     │
│   ( you like?)   │──▶│ [card]           │       │ ┌────────────────────┐ │
│    ╰────────╯    │   │ CURIOUS          │       │ │ Linda Perhacs      │ │
│ [Find musicians] │   │ [card]  tap ─────┼──▶    │ │ …                  │ │  the list scrolls
│ RECENT           │   │ [More like this] │ new   │ └────────────────────┘ │
│ Nick Drake       │   │                  │ page  │ CURIOUS                │
│ Khruangbin       │   │                  │       │ [card] …               │
└──────────────────┘   └──────────────────┘       │ [ More like this… ]    │
                                                   ├────────────────────────┤
                                                   │ [■Safe][■Curious][□Wild]│  the dock: always there,
                                                   │ ((Who do you like?)) ➤ │  over the keyboard
                                                   └────────────────────────┘
```

## The page

From top to bottom:

| Part | What it is |
| --- | --- |
| **Title bar** | Kofflard, ★ Favourites, ⚙ Settings, as now. |
| **Trail** | `‹ Nick Drake`, only after a card was tapped: the search this one came from. Tapping it, or the system back, goes back one search. |
| **Heading** | "Like Vashti Bunyan", and the error, loading and "did you mean" states (below). |
| **List** | The cards in their sections and "More like this…" at the end. Before the first search it holds Recent. |
| **Dock** | Fixed at the bottom: the Safe, Curious and Wild toggles, then the name field in the orb's glow, with a send button (➤). It stays visible while the list scrolls, and rises with the keyboard. |

### What the list shows

| When | List |
| --- | --- |
| First open, nothing searched | "Who do you like?" hint and **Recent** (tap one to search it again); on a new install the field is focused |
| Field focused, empty | Recent |
| Searching | the loading state in place of the cards; the old cards go |
| Results | heading, sections, More like this… |
| Name unclear | "Did you mean…" candidates |
| Error / empty | the message and Retry (and Fix connection) |

### Safe, Curious, Wild: one filter for the ask and the list

Three buttons in the dock, in the lane colours (Safe amber, Curious teal, Wild violet). Each is a toggle that looks like a button: **on** it is filled in its colour with white text; **off** it is glass with an edge in its colour and the text in that colour. Any mix can be on, but not none: the last one on can't be turned off.

They do two jobs at once:

| | What the toggles do |
| --- | --- |
| **The ask** | A search asks only for the lanes that are on, 12 picks split between them: one lane 12, two lanes 6 + 6, all three 4 + 4 + 4. "More like this…" asks the same way. |
| **The list** | Only the sections that are on show. Turning one off hides its section at once, with no request. |
| **A lane turned on that has no cards yet** | It asks for that lane alone, 4 picks, and they stream into their section; the other cards stay. |

- They are remembered across restarts, as the `lanes` setting.
- A new install starts with Safe and Curious on, Wild off: serendipity comes when the user asks for it (design §2).
- The heading counts what shows: "Like Nick Drake · 8 of 12".
- Screen readers: "Wild, shown" or "Wild, hidden"; a tap says the new state.

### Chaining: a card's search replaces the list

A card's "More →" searches that musician **on the same page**: the list swaps, the heading changes, and the trail grows (`‹ Vashti Bunyan`). Back walks the trail one step, showing the earlier results from the cache, not asking again. The trail lasts the session; only its last step is shown. Back goes as far as the first search, then to Recent, then out of the app.

On phones this also ends the stack of results pages behind the shown one, which is what the stacked-page fault came from ([stacked-pages.md](../rca/stacked-pages.md)). The desktop design keeps the stack, and Bruinet's fix stays.

### The search field

- The big orb goes on the phone. Its rings become the glow around the field in the dock, so the Aurora look keeps its centre piece, smaller.
- ➤ (and the keyboard's Search key) does what Find musicians does now. An empty name shows the error above the field.
- After a search the field keeps the name, selected, so typing replaces it.

## How

### Two layouts, one set of view models
- `AppShell` picks the root by `DeviceInfo.Idiom`: **Phone → `DiscoverPage`**; Desktop and Tablet → `SearchPage`, which pushes `ResultsPage` (Aurora D).
- The view models don't know which layout they are in. Where `SearchViewModel` and `ResultsViewModel` navigate to `KofflardRoutes.Results` today, they ask an **`ISearchFlow`** port to show a search (`ShowAsync(artist, cached)`):
  - `NavigatingSearchFlow` (desktop and tablet) navigates to the results route, as now;
  - `DiscoverViewModel` (phones) is the flow itself, and loads the results in place.
  
  The port is chosen with the root page in `MauiProgram`.

### Core
- **`DiscoverViewModel`** (new) is the phone page's view model and its `ISearchFlow`. It holds:
  - `Search`, the `SearchViewModel` as now (the name, its error, Recent);
  - `Results`, a `ResultsViewModel`, made per search;
  - `Trail`, a stack of artists, and `BackCommand`.
- Back from the cache uses `LoadAsync(artist, cached: true)`, as Recent does now.
- **The toggles**: `LaneToggleViewModel` (`Lane`, `IsOn`, `ToggleCommand`), three of them on `DiscoverViewModel`, which refuses to turn off the last one.
  - `LaneMix.For(IReadOnlySet<Lane> lanes)`: 12 split between them (12; 6 + 6; 4 + 4 + 4). The prompt asks for that mix; the result cache key takes the set.
  - `ResultsViewModel.Shown`, the set: `Lanes` holds only the sections in it; the cards of the others are kept, hidden.
  - A lane turned on with no cards: `ResultsViewModel.AskForAsync(lane)`, a request for 4 picks of that lane, excluding the cards already there; its cards stream into the section.
  - The `lanes` setting (`AppSettings.Lanes`), saved on each toggle.
  - The desktop design keeps asking for 4 + 4 + 4 and shows everything: it has no toggles.
- `SearchViewModel.LoadAsync` still sends a user with no AI to Connect.

### App
- **`DiscoverPage.xaml`** (new). Its grid is `Auto,Auto,*,Auto`: trail, heading, list, dock.
- The list is the results `CollectionView`, as on `ResultsPage`. Recent and the states become its `EmptyView` and header, so there is one scrolling list, not a list inside a scroll view. The card and section templates are shared with `ResultsPage` (moved to a resource dictionary), so both layouts draw the same cards.
- The dock is a `Border` in row 3. The keyboard:
  - Android: `WindowSoftInputModeAdjust.Resize` (in `App.xaml.cs`), so the page shrinks and the dock sits on the keyboard.
  - iOS: MAUI scrolls the focused field into view by default, which moves the whole page. Turn that off (`KeyboardAutoManagerScroll.Disconnect()`) and pad the dock by the keyboard height instead. Check on a device.
- The toggles are three `Button`s in a `Grid` of three columns, styled by a `DataTrigger` on `IsOn` (fill, text, edge), ids `Lane_Safe`, `Lane_Curious`, `Lane_Wild`; the state for screen readers and tests is `SemanticProperties.Description` ("Safe, shown"). A `CheckBox` can't be templated in MAUI, which is why these are buttons.
- System back: `OnBackButtonPressed` calls `BackCommand` while the trail has steps.

### Ids

The parts keep their ids, so most page objects keep working:

| Phone | Desktop and tablet |
| --- | --- |
| `DiscoverPage` (page root) | `SearchPage`, `ResultsPage` (page roots, unchanged) |
| `Search_Go` (the ➤ button) | `Search_Go` (Find musicians) |
| `Discover_Trail`, `Discover_Back`, `Discover_Dock` | — |
| `Lane_Safe`, `Lane_Curious`, `Lane_Wild` | — |

`Search_Name`, `Search_NameError`, `Search_Recent`, `Results_*`, `Suggestion*` and `LaneHeader*` are the same on both.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `LaneMix.For` splits 12 between the lanes that are on; the prompt asks for that mix; the last toggle can't go off; a toggle off hides a section with no request; a toggle on with no cards asks for that lane only; the setting is saved and read back. `DiscoverViewModel`: a search loads results in place; a card's search pushes the trail; back pops it and loads from the cache; Recent shows before the first search. `NavigatingSearchFlow` navigates as before. |
| UI | Page objects take their root from the layout: `SearchPage` and `ResultsPage` root at `DiscoverPage` on a phone. `Explore` waits for a new page on the desktop design and for the heading to change on a phone. `StackedPageDiagnosticTests` stays a Windows test; a trail test is added for phones. |
| Heads | Headless runs both layouts: the phone layout by setting the idiom to Phone for a collection, the desktop layout as now. Windows runs the desktop layout, Android the phone layout. |
| UAT | DISC.01 reads the same on both. A new DISC.04 "Back through the trail" (`@phone`): search, tap a card, back, the first results show without a new request. A new DISC.06 "Choose the lanes" (`@phone`): with Wild off, a search shows only Safe and Curious; Wild on, the wild picks arrive; Safe off, its section goes. |
| Device | Android: the dock sits on the keyboard and the list scrolls under it. iOS before a release (AD-032): the same, by hand once. |
| Video | Recorded on Windows, so unchanged. A phone video is not part of this change. |

## Decided (2026-10-31)

1. **Phones only.** Desktop and tablet have their own design (Aurora D) and keep two pages.
2. **No adventure slider**; instead **Safe, Curious, Wild toggles** in the dock, shaped as buttons, that set both what a search asks for and what the list shows.
3. **The orb** becomes the glow around the field in the dock.
4. **The trail** lasts the session; only its last step is shown.
5. **Recent** shows when the field is focused and empty (and before the first search), in the list's place; no chips above the field.

6. **A lane turned on with no cards yet** asks for that lane alone, 4 picks, at once.
7. **A new install** has Safe and Curious on, Wild off.

## As built

What differs from the plan above, or was left out:

- **The layout** is chosen in `AppShell`: by `DeviceInfo.Idiom`, or by the launch setting `KOFFLARD_LAYOUT=phone|desktop`. The UI suite's own fixture asks for the desktop design on every head, a phone's included; `PhoneAppFixture` asks for the phone's, so headless runs both.
- **The seam** is `ISearchFlow` as planned, with no `NavigatingSearchFlow`: on the desktop design `Flow` is null and the search waits for its first card (`SearchLauncher`, [desktop-aurora-changes.md](desktop-aurora-changes.md)).
- **The trail** grows only from a card's "More →"; a search from the dock or Recent starts a new trail.
- **The list** is `Views/ResultsView.xaml`, shared with the desktop results page, so both draw the same cards with the same ids. Recent is a separate layer over it, not the list's `EmptyView`.
- **Ids**: `Discover_Heading`, `Discover_Count` and `Discover_Recent` were added; a Recent chip is `Recent_{artist}` with `RecentRow_Remove_{artist}`.
- **iOS keyboard**: not changed. MAUI scrolls the focused field into view; whether the dock then sits right needs a device.
- **Tests**: unit tests for the mix, the prompt, the toggles and the trail; UI tests `PhoneTests` (`Layout=phone`: the toggles, the trail, and a screenshot for a look). No UAT scenarios yet: the UAT runner has one fixture, the desktop one.

## Not in this change
- The desktop and tablet design.
- Favourites, Settings and Connect stay pushed pages.
- The cards, the AI request, verification and the data.
- Tablets: they take the desktop design (`Idiom` is Tablet).
