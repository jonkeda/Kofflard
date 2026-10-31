# What the Windows findings mean on Android and iOS

Date: 2026-10-31. Status:
- **Android:** measured on the emulator (`Brinell_Android`, Android 16, 1080 × 2400, Appium 3.1.2 with UiAutomator2).
- **iOS:** predicted, not run. It needs a Mac, and Kofflard's fixture does not offer iOS yet.

Follows [windows-vs-headless.md](windows-vs-headless.md) and [stacked-pages.md](stacked-pages.md). Written to improve Bruinet: every head difference either fixed or known.

## Update: fixed in Bruinet (same day)

Built on the Bruinet branch `kofflard/shown-page-and-list-search` (not committed), pinned by `ListAndPageRootPinTests` (7 tests: 6 fail without the fixes):

| Action | Status | Android after the fix |
| --- | --- | --- |
| A1 Shell title | Done: `title` maps to the toolbar's text (Android) and the navigation bar's text (iOS, not run) | `AnAmbiguousName…` passes, title checked twice |
| A2 Partly visible rows | Done: a search moves past them | the sliver row no longer fails the search |
| A3 Search the whole list | Done: at the end, the search goes on upwards, a step at a time | the lane regression passes (Close above the viewport) |
| `ScrollToTop` stopping mid-list | Done: steps while the rows change, swipes included | — |
| Grouped positions | Done: noticed without `HasLogicalIndexes` | Kofflard's overrides removed |
| Covered page | Done: a page root must be attached | Kofflard's no-cache override removed |
| A4 view tree with failures, A5 clear data | Open | — |

Kofflard page objects changed with it: lanes are opened and closed by name (a header scrolled away is not in the Android tree), and card searches get the page budget.

Android, each test alone on a fresh install:
- **Pass:** the lane and stacked-page regressions, `AnAmbiguousName…`, and `TheStarAndMore…`. In the last, the star and "More →" are tapped for real, and the app stays in front, so the card's tap did not fire under them.
- **`Open_ATap…`** finds the card and long-presses it, then fails on the launch log, which Android cannot reach (K1).
- **`Search_ShowsTwelve…`** runs out of its 10 s searching up a long list. Swipes on the emulator move a fraction of the list at about a second each. It needs a larger budget, or a list search that jumps (`ScrollToTop`) when it is far from the top.

## Summary

The three Windows causes do not happen on Android, because UiAutomator lists only what is drawn:
- the grouped-list positions;
- sticky headers;
- the covered page.

The same family of problem shows up in Android's own form, though: **a list row that is only partly on screen breaks Bruinet's search.**
- Windows keeps the hidden part of the row and calls it not visible.
- Android leaves the hidden part out.

Either way, the search fails instead of moving past the row.

Android also showed four more things:
- Bruinet has no page-title locator for Android (or iOS).
- Its list search does not scroll back up.
- Kofflard's fixture resets nothing between tests on Android.
- Bruinet has no way to save the device's view tree for diagnosis.

**Prediction for iOS:** the star reads like Windows, and partly visible rows behave like Windows. A covered page is a short risk during the push animation, and the title has the same gap as Android.

## Finding by finding

| Finding | Windows | Headless | Android (measured) | iOS (predicted) |
| --- | --- | --- | --- | --- |
| **Grouped list positions** (`PositionInSet` restarts per lane) | Broke the search; fixed with `HasLogicalIndexes => false` | No positions | **No positions, so it does not happen** | No `PositionInSet` in XCUITest, so it does not happen (high confidence) |
| **Sticky lane headers** hide cards | Hid cards; turned off on Windows | No layout | **None.** MAUI's grouped `RecyclerView` does not pin headers (screenshot: "Close" scrolled away) | Probably none: MAUI's grouped `UICollectionView` does not pin section headers by default (medium) |
| **Covered page kept as root** | Kept for about 1 s; worked around (no root cache) | Drops out at once | **Drops out at once.** With root caching back on: 3 of 3 pass | Probably drops out after UIKit's push animation (about 0.35 s); a shorter version of the Windows window (medium) |
| **Star text** (description replaces text) | Reads "Remove … from favourites" | Reads "★" | Reads "★" (the widget's `text`) | Reads the description, like Windows: [maui-ios.md](../../external/test-tooling/docs/run/maui-ios.md) says the description becomes the element's label (high) |
| **A row partly on screen** | The hidden child is in the tree but "not visible": the read fails | Everything is visible | **The hidden child is absent: "Element not found within SuggestionCard [position 0]"** | Probably like Windows: realised cells stay in the tree with `visible=false` (medium) |
| **A list item above the viewport** | Hit before the sticky-header fix (Vashti Bunyan) | n/a | **"Expected a lane 'Close'. Lanes shown: Leap 2"**: the search only scrolls down | The same: the search logic is shared (high) |
| **Shell page title** | `title` | `title` (mapped) | **No locator in Bruinet**; Kofflard uses the toolbar text next to "Navigate up" | No locator in Bruinet (high) |
| **Picker open/close** (picker diagnostic) | UI Automation `ExpandCollapse` | In-app | "AppiumMauiElement does not implement OpenDropdown": a clear refusal, by design | The same refusal (high) |

## Evidence (Android)

### The partly visible row

Page source taken through Appium during `Open_ATapUsesYouTubeByDefault…`. The results list runs from y 374 to 2179:

| Row bounds (y) | Name | Children in the tree |
| --- | --- | --- |
| **374–399** (25 px on screen) | — | **`Suggestion_Card` only** |
| 399–815 | Michael Chapman | card, star, name, More, why, tags, known for |
| 815–1231 | Sibylle Baier | all |
| 1231–1347 | *Leap lane header* | |
| 1347–1763 | Ali Farka Touré | all |
| 1763–2179 | Arthur Russell | all |

What happens:
- Five rows are in the tree, but only four have a name.
- The first row is a card scrolled almost out of view. Only its full-card tap surface still reaches the screen, so UiAutomator includes the row and leaves out the name label.
- Bruinet's `FindItem` reads every row's name. On row 0 it gets `ElementNotFoundException` and fails the whole search: *Element not found within SuggestionCard [position 0]. Child locator: AutomationId:Suggestion_Name*.

Only one lane header is in the tree; the others scrolled out. The lane regression test, which opens Stretch, Leap and Close in that order, then fails with *Expected a lane 'Close'. Lanes shown: Leap 2*. Close is above the viewport, and the header search only scrolls down.

### The covered page

`StackedPageDiagnosticTests`, which reuses the first results page object straight after "More →":
- 2 of 2 passed with Kofflard's workaround;
- 3 of 3 passed with root caching switched back on.

On Android the covered page is gone from the tree as soon as the new page shows. The cached root goes stale, and R6 finds it again, as the call model intends.

### What went wrong while diagnosing

- `adb shell uiautomator dump` during a run stopped Appium's UiAutomator2 server and broke the session: the app was left on the home screen or the Connect page. A dump must go through the Appium session (`GET /session/{id}/source`), which needs the server started with `--allow-insecure "*:session_discovery"`.
- Bruinet saves no view tree on failure, and has no helper to save one. On Windows a screenshot was enough; on Android, the tree was what showed the missing name.

## Not measured on Android

Most journeys could not reach these, because of the row and fixture issues above:
- the tap-through layering (star and "More →" do not open the card);
- the long press;
- the adventure radio buttons, which are empty on Windows.

The tap-through layering and the long press matter most here, because Android is the only head that drives them with real touch. They should be checked once K1 and A2 are done.

## Kofflard's fixture on Android

Not Bruinet's problems, but they decide what an Android run can show:
- **Nothing is reset between tests.** `ForgetHistory` and `DefaultSettings` run on Windows and headless only. The app leaves out names it has already shown, so after the first search for Nick Drake every later one is empty. Of 14 UI tests, 11 failed this way or on the title.
- **The launch log is not reachable.** The app is not told about it on Android, so a tap opens YouTube or Spotify for real, and the tests that read the log cannot check what opened.
- **`ResetAppState` does not clear data.** On Android it terminates and activates the app, so the app's database survives. Clearing it needs UiAutomator2's `mobile: clearApp`.

## iOS: the reasoning

Kofflard's fixture throws for iOS (`GetDefaultAppPath`), and a run needs macOS with Xcode. So these are predictions, from how XCUITest reports the tree and from [maui-ios.md](../../external/test-tooling/docs/run/maui-ios.md):

- **Text and descriptions.** "A view with `SemanticProperties.Description` becomes one accessibility element … reports the description as the element's label".
  - The star and "More →" therefore read their descriptions, as on Windows.
  - The card's tap surface has no children, so nothing is hidden there.
  - The adventure choices carry a description, so their icon and name children are hidden from automation. Kofflard reads only the choice itself, so that is fine.
- **Partly visible rows.** XCUITest keeps realised cells, and their children, in the tree with `visible=false`. Bruinet would then fail a read as on Windows, unless the read skips not-visible children (A2).
- **Covered pages.** `UINavigationController` takes the previous page's view out of the window when the push animation ends. During the animation both are in the tree. A cached page root could point at the covered page for that moment: the Windows bug, with a shorter window. F1–F3 in [stacked-pages.md](stacked-pages.md) cover iOS too, because the bridge question ("is this the shown page") does not depend on the head.
- **Sticky headers.** MAUI does not pin section headers on iOS by default. If an app turns pinning on, the cells under the header stay `visible=true` in XCUITest, and taps can land on the header. Untested.
- **Long press.** XCUITest's touch-and-hold drives `TouchBehavior` for real. `FlyoutBase.ContextFlyout` is a Windows and Mac Catalyst feature; on iOS the long press is the only route, which is what Kofflard intends.
- **Zero-size elements are missing.** An empty `Label` measures 0 high on iOS. Kofflard's `Suggestion_Opened` and `Suggestion_NotFound` labels are often empty or hidden, so tests must not expect them to exist.

## Actions

### Bruinet

| # | Action | Where | Covers |
| --- | --- | --- | --- |
| A1 | **A Shell title for every head.** Add the title to the Shell chrome locators: Android, the toolbar's text view; iOS, the navigation bar's static text. Accept `title` as headless does, or give `AppRoot` a `PageTitle` label. | `AppiumMauiDriver` (`ShellChromeLocators`), `AppRoot` | Android, iOS |
| A2 | **A row partly on screen is not a failed search.** In `FindItem` / `ItemWhere`, a row whose key cannot be read, because the child is absent (Android) or not visible (Windows, iOS), counts as "not this one yet", and the search moves on and scrolls. This generalises B3 of [windows-vs-headless.md](windows-vs-headless.md). | `CollectionObjectBase.SearchAttempts` | Android, Windows, iOS |
| A3 | **Search the whole list, not only below.** When a search reaches the end without a match, scroll to the top once and search down again, within the budget. | `CollectionObjectBase.FindItem` | every device head |
| A4 | **A view tree with every failure, beside the screenshot.** On Android and iOS through the Appium session (`PageSource`), on Windows the UI Automation subtree. And a way to save one on purpose (`MauiTestContext.SavePageSource`). Without it, diagnosing pushes people to `uiautomator dump`, which breaks the session. | `MauiTestContext`, the failure capture | every device head |
| A5 | **`ResetAppState(clearData: true)`** on Android: `mobile: clearApp`, then activate. A suite whose app keeps state in its own storage can then start each test clean. | `AppiumMauiDriver.ResetApp` | Android, iOS (reinstall) |
| A6 | **A head-differences table**, one row per difference, saying "fixed" or "known", with the test that pins it. It extends "What Behaves Differently" in `docs/guides/maui-headless.md` to the device heads. The rows found so far: grouped positions, sticky headers, covered pages, description as text, partly visible rows, title locator. | `docs/guides` | all |
| F1–F3 | From [stacked-pages.md](stacked-pages.md): a page root counts as shown only while the app says it is the shown page. This also covers iOS's animation window. | the bridge, `PageObjectBase` | Windows, iOS |

### Kofflard

| # | Action |
| --- | --- |
| K1 | Reset per test on Android: clear the app's data (A5, or `mobile: clearApp` until then), and give the app a launch log it can write on the device, read back through Appium (`pull_file`). |
| K2 | Mark `PickerDiagnosticTests` Windows-only (`[WindowsOnlyFact]`): its phases are the Windows dropdown's. |
| K3 | `ResultsPage.Heading` uses an Android XPath (the text next to "Navigate up") until A1. *Done.* |
| K4 | Once K1 and A2 are done, run the Android suite in full. It is the only head that taps for real, so it is where the card's layering and the long press are proved. |
| K5 | iOS: add it to the fixture (`GetDefaultAppPath`) and run on a Mac. Expect the star text, partly visible rows and the title, as predicted above. |
