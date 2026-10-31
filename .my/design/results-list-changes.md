# Change: a simpler results list

Date: 2026-10-31. Status: agreed (the open questions are answered under Decisions), not built.

The results page shows each suggestion as a large card with five buttons, under a heading that repeats the band's name. This change makes the page a compact list:
- the lanes are folding sections;
- a tap on a musician opens them;
- the window starts phone-sized on Windows.

It changes design §3.1 (the results page) and §8.3 (Open with) in [kofflard-design.md](kofflard-design.md).

## Before and after

```
Before                                   After
┌─────────────────────────────┐          ┌─────────────────────────┐
│ Like Genesis   (title bar)  │          │▓ Like Genesis          ▓│  ← title bar in Primary
├─────────────────────────────┤          ├─────────────────────────┤
│ Like Genesis                │          │ English prog rock band… │
│ English prog rock band…     │          │ ▸ Close (6)             │
│ Adventure: Curious  Close 6…│          │ ▾ Stretch (4)           │
│ CLOSE                       │          │ ┌─────────────────────┐ │
│ ┌─────────────────────────┐ │          │ │☆ Camel      More → │ │
│ │ Camel                   │ │          │ │ Lyrical guitar work…│ │
│ │ Lyrical guitar work…    │ │          │ │ progressive rock ·… │ │
│ │ progressive rock · …    │ │          │ │ Known for: The Snow…│ │
│ │ Known for: The Snow…    │ │          │ └─────────────────────┘ │
│ │ [▶ YouTube] [YT Music]  │ │          │ ▸ Leap (2)              │
│ │ [Spotify]  ☆ Save       │ │          │                         │
│ │ More like this →        │ │          │ [ More like this… ]     │
│ └─────────────────────────┘ │          └─────────────────────────┘
```

## The changes

| # | Asked for | What changes |
| --- | --- | --- |
| 1 | Remove the second name of the band | The page's own `Results_Heading` label goes. The title bar keeps showing "Like Genesis". |
| 2 | Remove "Adventure: Curious" | `Results_Adventure` goes. The lane counts on that row (`Results_Lanes`) go too, because #4 puts them in the lane headers. The one-line summary of the searched musician stays. |
| 3 | Close, Stretch and Leap as expandable sub-lists | The cards are grouped by lane, each lane under a header you tap to open or close. This replaces the per-card lane label. |
| 4 | The total count behind each lane | Each header reads "Close (6)". The count is the lane's total, open or closed. Lanes without suggestions are not shown. |
| 5 | All closed by default; remember which are open | A new install starts with every lane closed. Opening or closing a lane is remembered, and every later result page (a new search, More like this, the next start of the app) opens the same lanes. |
| 6 | Remove the buttons; tap opens the default | The YouTube, YouTube Music and Spotify buttons go.<br>• A tap anywhere on the card opens the musician in the Open with service from Settings. "Opened in YouTube ✓" still shows for 3 s.<br>• A **long press** (right-click on Windows) shows "Open in: YouTube Music · Spotify" on the card, the two services that are not the default. Tapping one opens it, and the row goes away again. |
| 7 | Favourite as a star in front of the name | "☆ Save" becomes a star before the name: ☆ not saved, ★ saved. Tapping it toggles and does not open the musician. |
| 8 | "More →" at the far right of the name | "More like this →" becomes "More →", on the name row, right-aligned. Tapping it does not open the musician. The MusicBrainz ✓ sits between the name and "More →". |
| 9 | Start the Windows version phone-sized | The window opens at 420 × 860 unless `KOFFLARD_WINDOW` says otherwise. |
| 10 | The header in the main button's colour | The title bar uses `Primary` (#0F766E) with white text, on every page, so the app looks the same throughout. |

## How

### Results page (`src/Kofflard.App/Pages/ResultsPage.xaml`)
- Remove `Results_Heading`, `Results_Adventure`, `Results_Lanes` and the `Suggestion_Lane` label.
- `Results_Items` becomes a grouped `CollectionView` (`IsGrouped="True"`), bound to `Lanes`.
- A `GroupHeaderTemplate` per lane:
  - `Lane_Header`: a chevron (▸ closed, ▾ open), the lane's name and the count;
  - a `TapGestureRecognizer` bound to `ToggleCommand`;
  - the Bruinet `Tap` verb declared, so the Windows head can tap it (`uia:GestureAutomation.Verbs="Tap"`).
- A lane is closed by emptying its group, not by hiding its rows. That keeps virtualisation working and leaves no hidden rows for automation to find. The header stays.

  Both MauiHeadless (`ItemRealizer` reads `GroupableItemsView`) and the Windows head handle grouped lists.
- Built: on Windows the lane headers do not stick to the top while their cards scroll, because sticky headers hide the cards from UI Automation ([windows-vs-headless.md](../rca/windows-vs-headless.md)). The page objects for the list set `HasLogicalIndexes => false`, as a grouped list needs.

### View models (`src/Kofflard.Core/ViewModels`)
- New `LaneGroupViewModel : ObservableCollection<SuggestionViewModel>`, with:
  - `Lane`, `Name`, `Count` (the total) and `HeaderText` ("Close (6)");
  - `IsExpanded`, `Chevron` and `ToggleCommand`.

  It keeps all its cards and fills or empties itself as it opens and closes.
- `ResultsViewModel`:
  - `Items` gives way to `Lanes`, holding only the lanes that have suggestions.
  - `AdventureText` and `LaneSummary` go.
  - Toggling a lane saves the open lanes in settings.
- `SuggestionViewModel`:
  - Drops `OpenText`. `OpenCommand` is what a tap on the card runs.
  - `OpenSecondCommand` and `OpenThirdCommand` stay, behind the long press; `SecondText` and `ThirdText` name them.
  - New `ShowOtherServicesCommand` and `ShowsOtherServices`; opening either of the other two clears it.
  - `SaveText` becomes `StarText` (☆ or ★), with `StarDescription` ("Add Camel to favourites" or "Remove Camel from favourites", see [search-page-changes.md](search-page-changes.md)) for screen readers.

### Card (`src/Kofflard.App/Views/SuggestionCardView.xaml`)

**Layers: the fix for a tap on the star also opening the card.** On Android, a gesture on a view can also fire when a button *inside* it is tapped. So the tap does not go on the card's `Border`, which holds the buttons. The card becomes one `Grid` with two layers in the same cell:

1. **Below: the tap surface.** `Suggestion_Card`, an empty `ContentView` that fills the card. It has:
   - the `TapGestureRecognizer` (`OpenCommand`);
   - the long press (`ShowOtherServicesCommand`);
   - the `SemanticProperties`.
2. **Above: the content.**
   - The text (why, tags, known for, "Opened in…") sits in a stack with `InputTransparent="True"`, so a tap on the text falls through to the surface.
   - The star, "More →" and the "Open in" links are buttons on top. They are *siblings* of the surface, not its children, so a tap on them never reaches the surface's gestures, on any platform.

   The name label is input-transparent too; its row's `Grid` is `CascadeInputTransparent="False"` so its buttons still take taps.

This is the usual MAUI fix: no platform code and no checks on where a touch landed, and it behaves the same on Windows, Android and iOS. A UI test on Android checks it: tap the star, and nothing opens (the launch log stays empty).

**Name row:** `Grid ColumnDefinitions="Auto,*,Auto,Auto"`, holding:
- the star (`Suggestion_Save`, a transparent `Button`);
- the name;
- the MusicBrainz ✓;
- `Suggestion_Explore` ("More →", a transparent button).

The `FlexLayout` of buttons goes.

**Long press: "Open in…".**
- MAUI has no long-press recognizer. Use CommunityToolkit.Maui's `TouchBehavior`, already in the app, on the tap surface, with `LongPressCommand` bound to `ShowOtherServicesCommand`.
- On Windows, a right-click also runs it: `FlyoutBase.ContextFlyout` with one item, "Open in…". Windows users expect a right-click, not holding the mouse down.
- It shows `Suggestion_OtherServices`, a row inside the card: "Open in:" and a link-style button for each of the two other services (`Suggestion_OpenIn_YouTubeMusic`, `Suggestion_OpenIn_Spotify`).
- Tapping one opens that service and hides the row. A second long press also hides it.
- An inline row, not an action sheet: it can be tested like any button on every head. A platform action sheet on Windows is a `ContentDialog` that Bruinet would have to drive separately.
- Screen readers: the surface gets a `SemanticProperties.Hint` "Long press for other services". The row's buttons are ordinary buttons.
- Windows automation: MAUI has no long-press recognizer for Bruinet's bridge to find. Bruinet's `LongPress` verb therefore needs a sink. A small `CardGestureSink : IBruinetGestureSink` in `Kofflard.App/Automation` names `LongPress` and runs `ShowOtherServicesCommand`. It is attached with `uia:GestureAutomation.Sink` beside `Verbs="Tap,LongPress"`. MauiHeadless and Android do a real long press.

**Rest of the card:**
- The tap surface has the automation id `Suggestion_Card`, and `SemanticProperties.Description` "Open Camel in YouTube", so a screen reader announces what the tap does.
- The Saved page uses the same card, so it changes the same way. That is intended: one card, one behaviour.

### Settings (`AppSettings`, `SqliteStores`)
- `AppSettings` gains `IReadOnlySet<Lane> OpenLanes`. The default is empty: all closed.
- It is stored as `open_lanes` = `"Close,Leap"` beside `open_with`, in the same key–value table, so no migration is needed. An unknown or missing value reads as empty.
- The Settings page does not show it; it is remembered, not set.

### Shell and window
- Shell colours:
  - `Styles.xaml` gets a `Shell` style: `Shell.BackgroundColor` `Primary`, `Shell.TitleColor` `White`, `Shell.ForegroundColor` `White`.
  - Check the dark theme too: `Primary` works on both, but white must stay white.
- Window size:
  - `App.CreateWindow` sets 420 × 860 when `KOFFLARD_WINDOW` is not set (Windows only; a phone has no window size).
  - Constants `PhoneWidth` and `PhoneHeight`.
  - The window can still be resized by hand.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `ViewModelTests`:<br>• lanes start closed;<br>• the counts are totals whether open or closed;<br>• toggling saves `OpenLanes`;<br>• a new `ResultsViewModel` opens the saved lanes;<br>• the star toggles saved;<br>• `OpenCommand` opens the Open with service;<br>• `ShowOtherServicesCommand` shows the other two, and opening one hides them again. |
| Integration | `SqliteStores`: `open_lanes` round-trips, and a bad value reads as empty. |
| UI (`Kofflard.UITests`) | `ResultsPage`: drop `Heading` and `Lanes`; add `Lane(name)`, which has `Header`, `Count`, `IsOpen` and `Toggle()`.<br>`SuggestionCard`:<br>• drop `OpenButton`, `SecondButton` and `ThirdButton`;<br>• add `Tap()` and `LongPress()` on `Suggestion_Card`, and `OpenIn(service)` on the "Open in" row;<br>• `SaveButton` becomes `Star`.<br>`SuggestionList` steps first open the lane the musician is in.<br>New journeys:<br>• "lanes start closed";<br>• "an opened lane stays open on the next search";<br>• "long press opens Spotify";<br>• "tapping the star or More → opens nothing" (all heads, Android included). |
| UAT | DISC.01: "Lanes should equal …" becomes "Close should show 6", and so on.<br>DISC.03 and SET.01: "should open in X first" becomes "tapping X opens Y", read from the launch log.<br>New feature RES.01, "the lanes fold": closed by default, open one, search again, still open. |
| Video | VID.01 and `kofflard-in-a-minute.video.md`:<br>• recorded in the phone frame: `KOFFLARD_WINDOW=420x860` in the video's launch;<br>• scene 2 opens the lanes one by one, which shows them better;<br>• scene 3, "open in a service", becomes a tap on the card, then a long press for Spotify.<br>Rerecord after this change. The UI and UAT suites keep 1280 × 800 unless a layout check needs the phone size. |

The fixture's `DefaultSettings` resets `OpenLanes` to empty, so every test starts with the lanes closed.

## Decisions (answered 2026-10-31)

1. **The other services: a long press.** The card's long press (right-click on Windows) shows "Open in" with the two other services. See [Card](#card-srckofflardappviewssuggestioncardviewxaml).
2. **Open lanes survive app restarts.** They are stored in settings (`open_lanes`).
3. **The video uses the phone frame**, 420 × 860.
4. **Tap on the star inside a tappable card: fixed by layering.** The tap surface sits below the content as a sibling, not around it, so a button's tap never reaches the card's gestures. An Android UI test checks it. See [Card](#card-srckofflardappviewssuggestioncardviewxaml).

## Not in this change
- Search, Settings and Connect stay as they are, apart from the title bar colour.
- Lane order (Close, Stretch, Leap) and the 12-per-search mix (`LaneMix`) stay.
- The bottom "More like this…" button stays. It asks for a new batch; "More →" on a card searches from that musician.
