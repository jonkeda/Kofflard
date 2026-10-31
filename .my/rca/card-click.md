# RCA: a click on a card did not open it on Windows

Date: 2026-10-31. Status: root cause found by experiment; fixed in Kofflard and checked on Windows through Bruinet and headless. The Bruinet gap it showed (B1) is fixed. A real mouse click on the fixed build is still to be tried by hand.

## Summary

On Windows, a click on a suggestion card should open the musician in the Open with service (YouTube by default) and show "Opened in YouTube ✓" ([results-list-changes.md](../design/results-list-changes.md) #6). It did nothing.

**Root cause:** the card's tap was a CommunityToolkit `TouchBehavior` (`Command="{Binding OpenCommand}"`) on a transparent tap surface. On Windows its command never ran on a mouse click, even with the click landing on the surface itself.

**Why no test saw it:** Bruinet tapped the card through its gesture bridge. `CardGestureSink` ran the card's `OpenCommand` directly, so no pointer event and no `TouchBehavior` were involved. Every UI and UAT test that opens a card passed.

**Fix:** the list handles the click, as Bruinet's Todo sample does. The results and favourites lists use `SelectionMode="Single"`; selecting a card opens it, and the selection is cleared at once. A click on a list item selects it on every platform. Bruinet's page objects now open a card with `SelectItem`, UI Automation's `SelectionItemPattern.Select` on the card's list item: the path the Todo sample probed as the same as a tap.

## Impact

- **Users:** on Windows, the card's main action did nothing. The star, More →, and the right-click menu worked.
- **When:** since the card got its tap surface (the results list changes, before 2026-10-31). Not a regression from that day: the commit from its start (`c439026`) behaves the same.
- **Android and iOS:** not checked. The fix changes the tap there too (see Open).

## How it was found

A temporary UI test started the Windows app on the fake AI, searched "Nick Drake" and held the results open; a script found the first `Suggestion_Card` through UI Automation and clicked its body with the real mouse (60 % across, 70 % down: text, not a button). The result was read from the card's `Suggestion_Opened` line and the launch log.

| Step | Change | Result |
| --- | --- | --- |
| 0 | None | Did not open. Same on `c439026`. |
| — | A real click on the star instead | Saved the musician: real clicks reach the app. |
| A | The content layer above the tap surface fully click-through | Did not open: the content layer is not what swallows the click. |
| B | The tap surface above everything (`ZIndex="10"`) | Did not open: the click reaches the surface, and `TouchBehavior`'s command does not run. |
| C | A `TapGestureRecognizer` instead of `TouchBehavior` | Not run (see Lessons). |

Many early runs did not count: the test window was behind the Claude window, and the click landed there. The script was changed to click only when the window under the pointer belonged to the app. Bringing the window forward for a real click goes against how Bruinet runs (in the background), so the real-click probe was stopped after B; the fix was checked through Bruinet instead.

Why `TouchBehavior` does not run its command on a Windows mouse click was not traced into CommunityToolkit.Maui (14.0.1); B shows that it doesn't, which is enough to move off it.

## Fix

- `ResultsPage.xaml`, `SavedPage.xaml`: `SelectionMode="Single"`, `SelectedItem="{Binding SelectedCard}"`.
- `ResultsViewModel`, `SavedViewModel`: `SelectedCard`, as the Todo sample's `SelectedItem`: setting it runs the card's `OpenCommand`, then clears it.
- `SuggestionCardView`: no `TouchBehavior` tap. The tap surface keeps the long press: the right-click menu on Windows, and a `TouchBehavior` with only `LongPressCommand` on Android and iOS, added in code.
- `CardGestureSink`: the bridge's `Tap` goes; `LongPress` stays.
- `SuggestionList` (UI tests): `Open`, `AssertOpensFirstIn` and `OpenIn` select the card's list item (`SelectItem`), as the Todo sample's `TodoList.Open` does.
- A unit test: selecting a card opens it and clears the selection.

Checked:

| Suite | Windows (Bruinet) | Headless |
| --- | --- | --- |
| Unit | — | 68/68 |
| Integration | — | 28/28 (2 live skipped) |
| UI | 14/14 | 14/14 (12/14 before B1 was fixed) |
| UAT | 15/15 | 15/15 (12/15 before B1 was fixed) |

## Open

- **B1 (Bruinet), fixed: MauiHeadless could not select an item in a grouped `CollectionView`.** `Interactions.Select` looked for the item in `ItemsSource`, which in a grouped list holds the groups, and failed with *"it is not in ItemsSource"*. It now looks inside the groups when `IsGrouped` is true, and says *"in none of the groups"* when the item is in none. A tap inside a row already worked; only the direct select, which `SelectItem` uses, refused. Pinned by `HeadlessAppTests.Select_finds_an_item_inside_a_group_of_a_grouped_list` (MauiHeadless.Tests 52/52, Bruinet.Maui.Headless.Tests 4/4). Changed on request, in `external/test-tooling`, not committed there yet.
- **A real mouse click on the fixed build**, by hand, on Windows.
- **Android and iOS:** a `TouchBehavior` with only a long-press command may still take a short tap before the list sees it. Check on a device that a tap opens the card and a long press shows "Open in".

## Lessons

- A test that runs a control's command proves the command, not the click. The gesture bridge was right for the long press, which MAUI cannot recognise; for the tap, the platform's own path (selecting the list item) was there to use.
- The Todo sample had already solved "open a row": selection, probed on Windows. Starting from it would have avoided the tap surface for the tap.
- Real-mouse probes need the window in front, which fights both the user's own work and Bruinet's background runs. Keep them for diagnosis, briefly, and check fixes through Bruinet.
