# RCA: the results list passed headless and failed on Windows

Date: 2026-10-31. Status: root causes found and confirmed by experiment. Worked around in Kofflard (K1–K3); the Bruinet actions are open. Written to improve Bruinet.

## Summary

The results list changed from a flat list of cards to lanes that fold: a grouped `CollectionView`, a header per lane ([results-list-changes.md](../design/results-list-changes.md)).

**Results:**
- On the MauiHeadless head every UI test and UAT scenario passed (12/12, 16/16).
- On the Windows head, 6 of 12 UI tests failed. Each failed as soon as it looked for a card, with an error that blamed the wrong element: *'AutomationId:Results_Items' was found but is not visible*. Read on its own, the list reported itself visible.

**Two independent causes.** Each was confirmed by switching it off with nothing else changed.

1. **Grouped lists need `HasLogicalIndexes = false`, and nothing says so.**
   - In a grouped list, WinUI restarts `PositionInSet` in every group.
   - Bruinet keys rows by `PositionInSet` unless the collection overrides `HasLogicalIndexes`.
   - The override exists, but it is documented only in a code comment and used only in one Bruinet unit test. It is not detected.
   - With it left on, Close card 1 and Stretch card 1 look like the same row. The search skips rows it believes it has seen, the "did scrolling realise new rows" check sees no change, and the search stops before the later lanes.
2. **WinUI's sticky group headers hide the rows scrolled under them, and Bruinet's scroll-into-view lands rows exactly there.**
   - A WinUI grouped `ListView` pins the current group's header at the top, and the rows scrolled under it are clipped, so UI Automation reports them `IsOffscreen`.
   - Bruinet reads a card's name only once the label is on screen. When it is not, Bruinet scrolls it into view: WinUI puts the row at the top edge of the viewport, under the sticky header. So the row is still off-screen.
   - The read fails, and `FindItem` reports that failure under the collection's locator.

Headless passed because MauiHeadless has **no viewport**:
- every row is realised;
- nothing is ever off-screen;
- nothing scrolls;
- no header is sticky.

So neither cause can happen there. The tests exercised the app's logic, but not the collection code that runs only when a list scrolls.

## Impact

- **Kofflard:** on Windows, every test that looks for a card failed (6 of 12 UI tests). The Windows UAT run was not attempted. Users are not affected: the app works, only automation could not find the cards.
- **Bruinet:** any grouped `CollectionView` taller than its window is affected the same way:
  - on Windows for certain;
  - probably on Android too, where `PositionInSet` and sticky headers behave alike (not checked).
- **Cost:** about an hour, because the error named the list, and the list was visible.

## Evidence

All runs use `LaneDiagnosticTests` (Windows head, 1280 × 800). It searches "Nick Drake", opens Stretch, Leap and Close in that order, and then looks for one card from each lane. The `KOFFLARD_RCA_*` switches are temporary experiment flags in the test project and the app.

### 1. The list says it is visible; the lookup says it is not

| Step | List visible | Scrollable | Scrolled | Headers read | Cards realised |
| --- | --- | --- | --- | --- | --- |
| all closed | True | False | — | Close 6 · Stretch 4 · Leap 2 | 0 |
| Stretch opened | True | True | 0 % | all three | 4 |
| Leap opened | True | True | 15 % | all three | 6 |
| Close opened | True | True | 0 % | **Close 6 only** | 5 |

After that, all three lookups failed with *'AutomationId:Results_Items' was found but is not visible*. The exception comes from `CollectionObjectBase.FindItem` (`Failure(...)`, line 518). Underneath it, the failed read was a card's `Suggestion_Name` label, but the message names the list.

"Headers read: Close 6 only" also shows virtualisation at work: the other headers had scrolled out and were no longer in the tree. Headless always sees all three.

### 2. Isolating the causes

| Run | Grouped switch off | Off-screen rows skipped | Sticky headers | Bill Fay (Stretch) | Ali Farka Touré (Leap) | Vashti Bunyan (Close, first) |
| --- | --- | --- | --- | --- | --- | --- |
| as built | no | no | on | not visible | not visible | not visible |
| A | yes | no | on | not visible | not visible | not visible |
| B | yes | yes | on | **found** | **found** | not found: hidden under the header |
| C | yes, read budget 5 s | no | on | not visible | not visible | not visible |
| D | yes | yes, plus scroll to top | on | found | found | not found |
| **E** | **yes** | **no** | **off** | **found** | **found** | **found**, 0 off-screen reads |
| F | no | no | off | not found, search stopped in Close | not found | found |

What the runs show:
- **E:** with both causes removed, everything is found, and no read ever meets an off-screen row.
- **F:** with sticky headers off but the grouped switch left on, the search reads Close's six cards twice and stops. That is cause 1 on its own.
- **A and C:** with the grouped switch fixed, the off-screen read still fails, and a longer budget does not help. That is cause 2 on its own.

### 3. What the screen showed

A screenshot (`MauiTestContext.SaveScreenshot`) was taken right after Bruinet's `ScrollToTop()`. That call reported the list at 0 %. On screen:
- "Close (6)" is pinned at the top, and the first visible card is Linda Perhacs, the third card;
- Vashti Bunyan and Bert Jansch are scrolled under the header;
- the scroll bar is not at the top.

The first two cards report every part as not visible (name, star, tap surface, text). The third reports every part visible.

With sticky headers off, the same `ScrollToTop()` reports 0.67 %. The `ScrollPattern` value on the grouped list therefore cannot be trusted to mean "at the top" while a header is sticky.

## Root cause

**Cause 1, in Bruinet: grouped lists are not recognised.**
- `CollectionObjectBase` identifies rows by `PositionInSet` (`ItemKey.LogicalIndexOf`) unless a subclass overrides `HasLogicalIndexes`.
- A grouped list breaks that assumption silently: positions repeat, and duplicates are treated as rows already seen.
- The materialisation loop (`MaterializeStep`) decides "no progress, end of list" from the same indexes.
- The switch exists, with a correct comment ("False for a grouped list: the platform restarts PositionInSet in each group"). But a page-object author meets it only by reading the source, and nothing warns when positions repeat.

**Cause 2, between WinUI and Bruinet: scroll-into-view ignores sticky headers.**
- Bruinet's reads insist on an on-screen element (`ControlCalls.GetWithElement`, `ensureVisible: true` by default). When the element is off-screen, Bruinet scrolls it into view (`EnsureVisible` → `ScrollIntoView`).
- On a grouped WinUI `ListView`, the row is brought to the top edge of the viewport, which the sticky header covers. The row stays clipped, `IsOffscreen` stays true, and the read fails however long it waits.
- A text read does not need the row on screen: UI Automation returns a realised row's name either way.

**Why headless could not catch either:** MauiHeadless models the visual tree, not a viewport. With no virtualisation, no scrolling and no sticky headers:
- the grouped-list keys never matter, because every row is present at once;
- no row is ever off-screen.

A collection test that passes headless proves the page object's logic, not the scrolling.

## Contributing factors

- **The error named the wrong element.** `FindItem` rethrows the last row-level `ElementNotReadyException` under the collection's own locator, so the message said the list was not visible while the list was fine. The real failing element, `Suggestion_Name` in row 0, appears nowhere.
- **Plain UI tests leave no screenshot on failure.** UAT scenarios do; the xUnit UI tests do not. The first screenshot came from a diagnostic test written for this RCA.
- **A scroll percent that is not what it says.** `ScrollToTop()` stopped when the reading said 0 % while the list was not at the top.
- **One page-object choice of mine.** Card names are read with a 250 ms budget (`VanishedCardReadMs`). Not a cause: run C used 5 s and failed the same way.

## Actions

### Bruinet

| # | Action | Where | Priority |
| --- | --- | --- | --- |
| B1 | Detect grouped lists: when realised rows report a repeated `PositionInSet`, stop using logical indexes for this collection. Also say so in the call log. Headless can read `GroupableItemsView.IsGrouped` directly. Keep `HasLogicalIndexes` as an explicit override. | `CollectionObjectBase` (`LogicalIndexOf`, `KeyOf`, `MaterializeStep`) | High |
| B2 | Document grouped collections: what a grouped list needs, and a header collection beside the item collection (as Kofflard's `LaneHeaderList`). | `docs/controls`, the `maui-control` skill | High |
| B3 | Reads should not require the element on screen. `GetText` and `GetAttribute` on a realised element can read off-screen; keep visibility for actions and for `AssertVisible`. Failing that, inside a collection search treat a not-visible row as "not this one yet" and keep scrolling, not as a failure of the search. | `ControlCalls.GetWithElement` (`ensureVisible` default), `ViewBase` reads | High |
| B4 | Scroll-into-view that accounts for sticky headers. If the element is still off-screen after `ScrollItemPattern.ScrollIntoView`, scroll back by the header's height (small `ScrollPattern` decrements) and check again. | `FlaUIMauiElement.ScrollIntoView`, `ControlCalls.EnsureVisible` | Medium |
| B5 | Report the element that failed. When `FindItem` / `ItemWhere` gives up on a row-level `ElementNotReadyException`, name the inner locator and the row: "Suggestion_Name in row 0 is off-screen". | `CollectionObjectBase.Failure` | Medium |
| B6 | A headless viewport, opt-in: a window size, a realisation window of N rows, and off-screen rows reported as such. Then collection scrolling, keys and materialisation run headless too. At the least, say in the headless guide that it does not test scrolling. | `MauiHeadless` (`ItemRealizer`), `docs/guides` | Medium |
| B7 | A screenshot on failure for xUnit UI tests, as UAT scenarios have. | the MAUI test fixture base | Medium |
| B8 | Check `ScrollToTop()` against the first row actually being on screen, not only the `ScrollPattern` percent. | `CollectionObjectBase.ScrollToTop` | Low |
| B10 | Make `Text` mean the same on every head. On Windows, `FlaUIMauiElement.Text` falls back to the UI Automation name. For a button that is the `SemanticProperties.Description` when one is set, so the star read "Remove Vashti Bunyan from favourites" on Windows and "★" headless. Read the button's content (its text child) before the name, or offer the two as separate reads (`GetText`, `GetAccessibleName`). | `FlaUIMauiElement.Text` | Medium |
| B11 | Superseded by [stacked-pages.md](stacked-pages.md), F1–F6: a page root counts as shown only while it is the page the app shows. | `PageObjectBase`, `FlaUIMauiElement`, the bridge | High |
| B9 | A Bruinet sample and test: a grouped `CollectionView` with sticky headers, taller than the window, on the Windows head. With it, B1, B3 and B4 have a regression test. | `samples/Bruinet.Samples.Maui.App`, `tests/Bruinet.Maui.UITests` | High |

### Kofflard

| # | Action | Status |
| --- | --- | --- |
| K1 | `HasLogicalIndexes => false` on `SuggestionList` and `LaneHeaderList` (both read the grouped `Results_Items`). | Done |
| K2 | Sticky lane headers turned off on Windows (`MauiProgram.cs`, `ItemsStackPanel.AreStickyGroupHeadersEnabled = false`). The lane name now scrolls away with its cards. Turn them back on when B3/B4 land. | Done (decided 2026-10-31) |
| K3 | Experiment flags (`KOFFLARD_RCA_*`) removed. `LaneDiagnosticTests` kept as a regression test (trait `Rca=windows-vs-headless`): every lane open, one card from each lane must be found. | Done |
| K4 | Run the Windows UI and UAT suites after K1–K3. Commit only when both heads are green. | Open |

## Found after the workaround

With K1–K3 in place, the card lookups passed on Windows. Two more differences showed up:

- **The star's text.** Headless the star reads "★". On Windows it reads its screen-reader description, "Remove Vashti Bunyan from favourites" (B10). The page object now accepts either, with a comment.
- **Two Windows tests fail only in a full run.** VID.01.3 failed in 2 of 3 full Windows UAT runs, and the lanes journey (RES.01.1) in 1 of 3 full UI runs. Run alone, each passed 5 of 5.
  - VID.01.3's error is the same "'Results_Items' was found but is not visible". It came on the results page that "More →" opened, which is the second results page on the Shell stack.
  - **Confirmed** in [stacked-pages.md](stacked-pages.md): the UAT page object kept the covered results page for about a second after "More →". Worked around there; the Bruinet fix is in its Windows head (F1–F3).

## Lesson

Headless is the fast proof of behaviour. Anything that depends on a viewport, such as scrolling, virtualisation, sticky headers or what is on screen, is proved only on a device head. A change to a list's shape (flat to grouped) is such a change, so run it on Windows before calling it done.
