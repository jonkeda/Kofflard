# Change: one page on a phone, search at the bottom

Date: 2026-10-31. Status: agreed (answers under [Decided](#decided-2026-10-31)), not built.

Today a search is two pages: the search page (the orb, Find musicians, Recent) pushes a results page, and tapping a card pushes another results page on top. On a phone this means the name field is at the top, out of thumb reach, and every chained search adds a page to the stack. This change folds search and results into one page **on phones (Android and iOS)**: the list fills the screen and the search sits at the bottom, as in a chat or a maps app. Favourites, Settings and Connect stay separate pages.

Windows keeps its own design, from [aurora.md](aurora.md): the search page and the results page as they are there. This change does not touch it.

It changes design §3.1 (screens) in [kofflard-design.md](kofflard-design.md) for phones. It takes the Aurora look, without the adventure slider, which Aurora removes. The AI request, the cards and the data stay as they are.

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
                                                   │ ((Who do you like?)) ➤ │  the dock: always there,
                                                   └────────────────────────┘  over the keyboard
```

## The page

From top to bottom:

| Part | What it is |
| --- | --- |
| **Title bar** | Kofflard, ★ Favourites, ⚙ Settings, as now. |
| **Trail** | `‹ Nick Drake`, only after a card was tapped: the search this one came from. Tapping it, or the system back, goes back one search. |
| **Heading** | "Like Vashti Bunyan", and the error, loading and "did you mean" states (below). |
| **List** | The cards in their sections and "More like this…" at the end. Before the first search it holds Recent. |
| **Dock** | Fixed at the bottom: the name field in the orb's glow, with a send button (➤). It stays visible while the list scrolls, and rises with the keyboard. |

### What the list shows

| When | List |
| --- | --- |
| First open, nothing searched | "Who do you like?" hint and **Recent** (tap one to search it again); on a new install the field is focused |
| Field focused, empty | Recent |
| Searching | the loading state in place of the cards; the old cards go |
| Results | heading, sections, More like this… |
| Name unclear | "Did you mean…" candidates |
| Error / empty | the message and Retry (and Fix connection) |

### Chaining: a card's search replaces the list

Tapping a card body searches that musician **on the same page**: the list swaps, the heading changes, and the trail grows (`‹ Vashti Bunyan`). Back walks the trail one step, showing the earlier results from the cache, not asking again. The trail lasts the session; only its last step is shown. Back goes as far as the first search, then to Recent, then out of the app.

On phones this also ends the stack of results pages behind the shown one, which is what the stacked-page fault came from ([stacked-pages.md](../rca/stacked-pages.md)). Windows keeps the stack, and Bruinet's fix stays.

### The search field

- The big orb goes on the phone. Its rings become the glow around the field in the dock, so the Aurora look keeps its centre piece, smaller.
- ➤ (and the keyboard's Search key) does what Find musicians does now. An empty name shows the error above the field.
- After a search the field keeps the name, selected, so typing replaces it.

## How

### Two layouts, one set of view models
- `AppShell` picks the root by `DeviceInfo.Idiom`: **Phone → `DiscoverPage`**; anything else → `SearchPage`, which pushes `ResultsPage` as now.
- The view models don't know which layout they are in. Where `SearchViewModel` and `ResultsViewModel` navigate to `KofflardRoutes.Results` today, they ask an **`ISearchFlow`** port to show a search (`ShowAsync(artist, cached)`):
  - `NavigatingSearchFlow` (Windows) navigates to the results route, as now;
  - `DiscoverViewModel` (phones) is the flow itself, and loads the results in place.
  
  The port is chosen with the root page in `MauiProgram`.

### Core
- **`DiscoverViewModel`** (new) is the phone page's view model and its `ISearchFlow`. It holds:
  - `Search`, the `SearchViewModel` as now (the name, its error, Recent);
  - `Results`, a `ResultsViewModel`, made per search;
  - `Trail`, a stack of artists, and `BackCommand`.
- Back from the cache uses `LoadAsync(artist, cached: true)`, as Recent does now.
- `SearchViewModel.LoadAsync` still sends a user with no AI to Connect.

### App
- **`DiscoverPage.xaml`** (new). Its grid is `Auto,Auto,*,Auto`: trail, heading, list, dock.
- The list is the results `CollectionView`, as on `ResultsPage`. Recent and the states become its `EmptyView` and header, so there is one scrolling list, not a list inside a scroll view. The card and section templates are shared with `ResultsPage` (moved to a resource dictionary), so both layouts draw the same cards.
- The dock is a `Border` in row 3. The keyboard:
  - Android: `WindowSoftInputModeAdjust.Resize` (in `App.xaml.cs`), so the page shrinks and the dock sits on the keyboard.
  - iOS: MAUI scrolls the focused field into view by default, which moves the whole page. Turn that off (`KeyboardAutoManagerScroll.Disconnect()`) and pad the dock by the keyboard height instead. Check on a device.
- System back: `OnBackButtonPressed` calls `BackCommand` while the trail has steps.

### Ids

The parts keep their ids, so most page objects keep working:

| Phone | Windows |
| --- | --- |
| `DiscoverPage` (page root) | `SearchPage`, `ResultsPage` (page roots, unchanged) |
| `Search_Go` (the ➤ button) | `Search_Go` (Find musicians) |
| `Discover_Trail`, `Discover_Back`, `Discover_Dock` | — |

`Search_Name`, `Search_NameError`, `Search_Recent`, `Results_*`, `Suggestion*` and `LaneHeader*` are the same on both.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `DiscoverViewModel`: a search loads results in place; a card's search pushes the trail; back pops it and loads from the cache; Recent shows before the first search. `NavigatingSearchFlow` navigates as before. |
| UI | Page objects take their root from the layout: `SearchPage` and `ResultsPage` root at `DiscoverPage` on a phone. `Explore` waits for a new page on Windows and for the heading to change on a phone. `StackedPageDiagnosticTests` stays a Windows test; a trail test is added for phones. |
| Heads | Headless runs both layouts: the phone layout by setting the idiom to Phone for a collection, the Windows layout as now. Windows runs the Windows layout, Android the phone layout. |
| UAT | DISC.01 reads the same on both. A new DISC.04 "Back through the trail" (`@phone`): search, tap a card, back, the first results show without a new request. |
| Device | Android: the dock sits on the keyboard and the list scrolls under it. iOS before a release (AD-032): the same, by hand once. |
| Video | Recorded on Windows, so unchanged. A phone video is not part of this change. |

## Decided (2026-10-31)

1. **Phones only.** Windows has its own Aurora design and keeps two pages.
2. **No adventure slider** on the phone page: Aurora removes it.
3. **The orb** becomes the glow around the field in the dock.
4. **The trail** lasts the session; only its last step is shown.
5. **Recent** shows when the field is focused and empty (and before the first search), in the list's place; no chips above the field.

## Not in this change
- Windows and its layout.
- Favourites, Settings and Connect stay pushed pages.
- The cards, the AI request, verification and the data.
- Tablets: they get the Windows layout (`Idiom` is Tablet), until they have a design of their own.
