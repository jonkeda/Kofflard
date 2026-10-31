# Change: adventure as an outcome, not an input

Date: 2026-10-31. Status: built, then changed: the slider was removed the same day (see [Update](#update-the-slider-goes)). Android and iOS not yet looked at.

Today the user picks how adventurous a search is before asking (Safe … Wild), and the answer comes back sorted into three lanes (Close, Stretch, Leap) that fold open. Both say "how far from the artist you typed", in two sets of words. This change asks once, for every distance, and lets the user choose how far to look on the results, with the slider there. The three outcomes take the adventure words: **Safe, Curious, Wild**. Close, Stretch and Leap go from the screen.

It changes design §3.1 (the search page), §3.2 (the lanes and the mix), §3.3 (settings) and §2's "the user turns adventurousness up" in [kofflard-design.md](kofflard-design.md), and replaces the lane headers of [results-list-changes.md](results-list-changes.md). It keeps the [Aurora](aurora.md) look.

## Update: the slider goes

Decided 2026-10-31, after it was built: the results page has no slider either. What is left:
- Every search asks for 4 Safe, 4 Curious and 4 Wild, and the results show all three lanes, Safe first, each under "Safe (4)" in its colour. Nothing folds and nothing is hidden.
- "More like this…" asks for the same even mix again, excluding what was shown.
- There is no adventure setting at all: the `adventure` key is dropped on the next save, and an old level is no longer read.
- The radio-button template (`AdventureChoice`), the slider's colours and the Windows `BareRadioButton` style are gone with it.

The rest of this doc describes the slider as it was built; where it disagrees with this section, this section holds.

## The overlap today

| | Adventure (input) | Lanes (output) |
| --- | --- | --- |
| Words | Safe, Curious, Open, Bold, Wild | Close, Stretch, Leap |
| What it says | how far to go | how far each pick is |
| Where | search page and Settings, before asking | results, after |
| What it does | sets the mix of 12 (`LaneMix`: Safe 8/4/0 … Wild 2/4/6) and the temperature (0.4 … 0.8) | groups the picks; each lane folds; open lanes are remembered for the next search |
| Effect the user sees | more or fewer picks in each lane | which lanes are open |

So a user who wants far-out picks turns the slider up *and* opens the Leap lane. One idea, two controls and eight words. The first control is a guess: you don't know you wanted Wild until you've seen the safe picks. Lanes that start closed and are remembered open already act as an adventure setting.

## Proposal

```
Before                                   After
┌─────────────────────────┐              ┌─────────────────────────┐
│ ( Who do you like? )    │              │ ( Who do you like? )    │
│ [Nick Drake           ] │              │ [Nick Drake           ] │
│ How adventurous? Curious│              │                         │
│ ○──●──○──○──○           │              │ [    Find musicians   ] │
│ [    Find musicians   ] │              │ RECENT                  │
└─────────────────────────┘              └─────────────────────────┘
┌─────────────────────────┐              ┌─────────────────────────┐
│ Like Nick Drake         │              │ Like Nick Drake         │
│ (▸ Close 6)             │              │ How adventurous? Curious│
│ (▸ Stretch 4)           │              │ ●─────●─────○           │
│ (▸ Leap 2)              │              │ Safe  Curious  Wild     │
│                         │              │ SAFE                    │
│                         │              │ [card] [card] …         │
│                         │              │ CURIOUS                 │
│                         │              │ [card] [card] …         │
└─────────────────────────┘              └─────────────────────────┘
```

1. **The search page has no adventure control.** The name, Find musicians and Recent stay. The orb gets more room.
2. **One search asks for every distance**: a fixed mix of 12, 4 Safe, 4 Curious, 4 Wild, at one temperature (0.6). Wild picks keep their bridge.
3. **The results page has the slider: "How adventurous?"**, three stops, Safe, Curious, Wild. It is a view of what's already there, not a new search:
   - at Safe, only the safe picks show;
   - at Curious, safe and curious;
   - at Wild, all 12.
   Moving it is instant: no request, no cost.
4. **It starts where the user left it**, as open lanes do now, and at Safe on a new install. Serendipity still comes slowly and only when the user asks for it (§2): nothing far shows until they slide.
5. **The groups become plain section headings** in the list, coloured as now: Safe amber, Curious teal, Wild violet. They no longer fold; the slider does that.
6. **"More like this…" gets more at the current setting**: at Wild it asks for 12 more leaning far, at Safe 12 more leaning close. That's where a lean still goes into the prompt, and only after the user has seen something.
7. **Settings loses "How adventurous"**.

### The words

One set of words, on the slider, the headings and the cards, for both "how far do I want to go" and "how far is this pick":

| Now (lane) | Now (level) | New | Means |
| --- | --- | --- | --- |
| Close | Safe | **Safe** | what fans of the artist also like, a little less obvious |
| Stretch | Curious, Open | **Curious** | the same sound, from lesser-known artists or other eras |
| Leap | Bold, Wild | **Wild** | another genre, country or decade, with a bridge back |

Three, not five: the AI sorts picks into three groups well enough to test (`SuggestionValidator`, the lane rules in the prompt), but it won't tell Open from Bold consistently, and the user can't check it either. Open and Bold go.

### What the user loses

- **A search that is all safe picks.** Safe asked for 8 close; now there are 4. "More like this…" at Safe gets more.
- **A search that leans wild from the start.** Wild asked for 6 leaps; now 4. "More like this…" at Wild gets more.

Both cost a second request, made only when the user wants it.

## How

### Core
- `Lane` is renamed and becomes the distance: `Safe < Curious < Wild`. `Adventure`, the five levels, goes.
- The prompt keeps its tested wording for the AI (`"lane": "close" | "stretch" | "leap"`, with the bridge rule on `leap`); the parser maps them to `Safe`, `Curious`, `Wild`. The AI's words never reach the screen.
- `LaneMix` becomes the fixed 4/4/4. Keep the class, for "More like this…", which asks for a mix leaning to one end (for example, Wild: 2/4/6).
- `DiscoveryPrompt.Temperature` becomes one value; `DiscoveryPrompt.User` takes the mix, not a level.
- `DiscoveryService.DiscoverAsync(artist, mix, …)`; the result cache key drops the level.
- `ResultsViewModel`:
  - `ShownUpTo` (a `Lane`), saved as the `adventure` setting;
  - the shown cards are those with `Lane <= ShownUpTo`;
  - `LaneGroupViewModel` loses `IsExpanded` and `ToggleCommand`; its heading is "Safe (4)".
- `SearchViewModel` loses `AdventureLevel`, `AdventureOptions` and `AdventureName`. `AdventureOptionViewModel` becomes the slider stop on the results, keyed by `Lane`.
- `KofflardRoutes.ForSearch` and `More →` lose the level.

### Data
- The `adventure` setting keeps its key and now holds a `Lane`. Read an old level once: 1 → Safe, 2–3 → Curious, 4–5 → Wild.
- `searches.adventure` stays in the table, unused, to keep the migration trivial; recent searches no longer show a level.
- Favourites don't store a lane (a favourite's card is always Safe), and cached results store it as a number, so neither needs migrating. The old `open_lanes` setting is dropped on the next save.

### App
- `SearchPage.xaml`: remove the slider block.
- `ResultsPage.xaml`:
  - the slider (the `AdventureChoice` stops, three of them) under the summary;
  - group headers become `Label`s in the lane colour.
- `SettingsPage.xaml`: remove "How adventurous".
- The adventure icons in `Resources/Images` go.
- Colours.xaml: `Close…`, `Stretch…`, `Leap…` become `Safe…`, `Curious…`, `Wild…`.

### Ids
| Goes | Comes |
| --- | --- |
| `Search_Adventure`, `Adventure_Safe` … `Adventure_Wild` on the search page | `Results_Adventure`, `Adventure_Safe`, `Adventure_Curious`, `Adventure_Wild` on the results page |
| `LaneHeader_Toggle` | `LaneHeader_Name` (a label) |
| `Settings_Adventure`, `Settings_AdventureName` | — |

## Tests

| Suite | Change |
| --- | --- |
| Unit | `LaneMix` is 4/4/4, and leans for "More like this…"; the prompt asks for the mix; the parser maps close/stretch/leap to Safe/Curious/Wild; `ResultsViewModel` shows `Lane <= ShownUpTo` and saves it; the old level and old favourites migrate. Drop the five-level tests. |
| Integration | `StoreTests`: the setting and favourites migrations. `DiscoveryEngineTests`: no level in the cache key. |
| UI | `SearchPage` loses `Adventure(...)`; `ResultsPage` gets it, with three stops. `LaneHeaderList` loses `Open`/`Close`, and RES.01's "opened lane stays open" becomes "the setting stays". Lane names in assertions become Safe, Curious, Wild. |
| UAT | DISC.02 "Adventurousness": slide to Wild, the wild picks show; search again, it is still at Wild. DISC.01.1 "Close 6 · Stretch 4 · Leap 2" becomes "Safe 4 · Curious 4 · Wild 4". The canned answers stay 6/4/2: the validator tops a short lane up from the others, so the fake AI still shows "Safe 6 · Curious 4 · Wild 2". |
| Video | The search scene loses the slider; the results scene slides from Safe to Wild. |

## Decided (2026-10-31)

1. **Adventure is no longer an input.** The search page and Settings lose it; the results page's slider is the only control.
2. **Safe, Curious, Wild** are the three words, for the slider and the groups. Close, Stretch and Leap confused; they stay only in the prompt to the AI.
3. **The mix is 4/4/4.** The slider hides the far picks, so relevance still comes first (§2).
4. **"More like this…" leans to the current setting**: at Safe it asks for a mix leaning close (6/4/2), at Wild leaning far (2/4/6), at Curious the even 4/4/4.
5. **The slider starts at Safe** on a new install.

## Not in this change
- The card, the bridge rule, exclusions, verification and Favourites stay as they are.
- The Aurora look stays; the slider moves pages, same drawing.
