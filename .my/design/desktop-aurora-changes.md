# Change: the desktop and tablet design, round 1

Date: 2026-10-31. Status: built 2026-10-31 (see [As built](#as-built)), then replaced by [one-page-desktop.md](one-page-desktop.md): the desktop is one page now, so #1 (waiting on the search page) is gone; #3 (no ✓) and #4 (✕ on Recent) stay.

Kofflard has two designs:
- **Phone** (Android and iOS phones): one page, the search at the bottom ([single-page-phone.md](single-page-phone.md)).
- **Desktop and tablet** (Windows, iPad, Android tablets): Aurora D, two pages, a search page that pushes a results page ([aurora.md](aurora.md)).

This doc changes the desktop and tablet design. Where a change also suits the phone, it says so under [The phone](#the-phone).

## The changes

| # | Asked for | What changes | State |
| --- | --- | --- | --- |
| 1 | No results: don't show the results page, show it on the search page | A search stays on the search page until its first card arrives. If no card comes, the message shows on the search page and no results page opens. | built |
| 2 | Aurora: remove the adventure filter; rename Close, Stretch, Leap to Safe, Curious, Wild | The slider is gone, and the lanes are Safe, Curious and Wild on the screen. | **done**: 6af582e, 5fd25f3 |
| 3 | Remove the checkboxes in the list items | The ✓ marks go from the cards. | built |
| 4 | An X on the Recent buttons, to remove them | Each Recent chip gets ✕, which removes that search from Recent. | built |

## 1. No results stay on the search page

```
Now                                         After
┌──────────────────┐   ┌──────────────────┐  ┌──────────────────────────┐
│   ( Who do   )   │   │ Like Xyzzy       │  │   ( Who do you like? )   │
│   ( you like?)   │──▶│                  │  │   [Xyzzy               ] │
│ [Find musicians] │   │ No musicians     │  │ [ Searching…  ◌        ] │  waits here
└──────────────────┘   │ found.           │  │ No musicians found like  │
                       └──────────────────┘  │ "Xyzzy". Try another     │  the answer, here
                                             │ name.                    │
                                             │ RECENT …                 │
                                             └──────────────────────────┘
```

Since the answer is streamed ([streaming-answers.md](streaming-answers.md)), the first card comes in a second or two. The search page waits for it:

| The search ends with… | Now | After |
| --- | --- | --- |
| the first card | results page, cards stream in | **results page opens on the first card**, the rest stream in as now |
| no cards (`Empty`) | results page, "No musicians found" | **search page**: the message under the name field |
| an error before any card | results page, the error, Retry, Fix connection | **search page**: the error under the name field, with Fix connection when it is about the connection. Find musicians is the retry. |
| "Did you mean…" (`Candidates`) | results page, the candidates | **search page**: the candidates under the name field; tapping one fills the field and searches it |
| an error after some cards | results page, cards and the error below | unchanged |

While it waits:
- Find musicians reads "Searching…" with a spinner and can't be pressed again;
- changing the name or tapping a Recent chip cancels the wait and starts over;
- leaving the page (★, ⚙) cancels it.

The same rule holds for a search started **on the results page** (a card's "More →"): the next results page opens on its first card. Until then the card shows "Searching…"; with no cards, it shows "Nothing found like Vashti Bunyan", and no page opens. A Recent search from the cache opens at once, as it has cards.

### How
- The search runs in a `ResultsViewModel` from the start, made by the search page instead of by the results page. `SearchViewModel` makes it, starts it, and watches its `State`:
  - `Content` (the first card): navigate to the results page and **hand it the running view model** (Shell navigation parameters, an object, not a query string), so nothing is asked twice;
  - `Empty`, `Error`, `Candidates`: show them on the search page, and stay.
- `ResultsPage` takes the view model it is handed. Its `Empty`, `Error` and `Candidates` states stay, for "More like this…" and a stream that breaks off.
- A card's "More →" does the same through the results page's view model, with the waiting and the message on the card.
- `KofflardRoutes.ForSearch` stays for the cached Recent case.

### Ids
| Comes | Where |
| --- | --- |
| `Search_Busy` | the spinner in Find musicians |
| `Search_Message` | "No musicians found like …" and errors |
| `Search_FixConnection` | under the message, when the connection is the cause |
| `Search_Candidates`, `CandidateRow`, `CandidateRow_Name` | the "Did you mean" list |
| `Suggestion_Searching`, `Suggestion_NothingFound` | on a card, after "More →" |

## 2. Adventure filter gone, Safe, Curious, Wild

Done by the Aurora session (commits 6af582e and 5fd25f3). Nothing left here. "Save" in the request is read as **Safe**, the word on screen.

## 3. No ✓ on the cards

A card has two ✓ today:
- `Suggestion_Verified`, after the name, once MusicBrainz confirms the artist (only when **Check musician names** is on);
- "Opened in YouTube ✓", for a moment after the card opens a service.

Both go. What they said stays in words:
- verified: nothing on the card; an unconfirmed artist keeps "Couldn't confirm this artist". So the only mark is on the odd one out.
- opened: "Opened in YouTube".

`VerifiedMark` and `Suggestion_Verified` go; `IsVerified` stays for the Spotify link and the unconfirmed line.

## 4. ✕ on a Recent chip

```
RECENT
(Nick Drake  ✕)  (Khruangbin  ✕)  (Vashti Bunyan  ✕)
```

- ✕ removes that artist from Recent, at once, with no question. Redoing it is one search.
- The cache stays: searching the artist again still answers from the cache. **Clear history** in Settings still empties everything.
- Tapping the chip itself searches, as now. ✕ is its own button, so it doesn't select the chip.
- Screen readers: "Remove Nick Drake from recent".
- When the last chip goes, "RECENT" goes with it.

### How
- `IDiscoveryStore.RemoveSearchAsync(string artist)`: `DELETE FROM searches WHERE normalised = $normalised`, so every search of that name goes, not only the newest.
- `SearchViewModel.RemoveRecentCommand` (a `RecentSearch`): removes it from the store and from `Recent`.
- `SearchPage.xaml`: the chip becomes `Name` + `✕` (`LinkButton`), id `RecentRow_Remove`.

## The phone

The phone design takes 3 and 4 too: the cards and the Recent chips are the same templates. Change 1 needs nothing there: the phone shows any outcome in its one list, and the trail only grows when a search has cards.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `SearchViewModel`: waits on the first card, then navigates with the view model; `Empty`, `Error` and `Candidates` stay on the page; a new name cancels the wait. `RemoveRecentCommand` removes from the store and the list. Cards have no `VerifiedMark`; `OpenedText` has no ✓. |
| Integration | `StoreTests`: `RemoveSearchAsync` removes every search of the name and keeps the cache. |
| UI | `SearchPage` page object: `Message`, `Candidates`, `RemoveRecent(name)`. Tests that expected `Results_Empty` or `Results_Error` after a search read `Search_Message` instead, and check the results page did not open. Opened-in checks drop the ✓. |
| UAT | DISC.01: "No musicians found" scenario reads on the search page. New DISC.05 "Remove a recent search": search, back, ✕, it's gone, the others stay. AI.01: a bad connection shows on the search page. |
| Device | Windows at the merge (AD-032): the wait on Find musicians and the ✕ on a chip. |

## Decided (2026-10-31)

1. **The "checkboxes" are the ✓ marks** on a card: verified, and "Opened in … ✓". Both go.
2. **"Did you mean…" shows on the search page**: it is a question about the name, and the name field is there.
3. **Errors before any card show on the search page**: there is nothing to show on a results page. An error after some cards stays on the results page.

## As built

What differs from the plan above:

- **Handing over the search**: not a Shell parameter. `SearchLauncher` gives the running `ResultsViewModel` to `RunningSearch` and then navigates; `ResultsPage` takes it as it is made, so it binds to the running search from the start and never swaps.
- **An unknown name with no candidates** ("No musician called Xyzzy is known. Check the spelling…") shows as the message, not as an empty "Did you mean".
- **Ids**: a candidate is `Candidate_{name}`, a chip's ✕ is `RecentRow_Remove_{artist}` (unique, so a page object needs no list); the card's note after "More →" is one label, `Suggestion_ExploreNote`.
- **Tests**: unit tests for the wait, the outcomes, ✕ and the card's note; UI journeys DISC.04.2 (an unknown name stays) and DISC.07.1 (✕); UAT DISC.01.3 and DISC.01.4 now read on the search page. There is no UAT scenario for ✕ yet.

## Not in this change
- The phone layout ([single-page-phone.md](single-page-phone.md)).
- The streaming, the AI request, the mix and the data.
