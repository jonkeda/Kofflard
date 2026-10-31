# RCA: a page object kept the covered page after navigation, on Windows only

Date: 2026-10-31. Status: root cause confirmed by experiment, and fixed in Bruinet's Windows head (branch `kofflard/shown-page-and-list-search` of the Bruinet submodule, not committed). Kofflard's workaround is removed.

> **Correction, found while building the fix.** The covered page does *not* stay in the tree. It has left it, and the element a page object holds for it goes on answering (present, not off-screen, full bounds) for about a second. A probe that scanned the tree throughout never saw two results pages, and the fix below, a check that the element is still attached, makes the reproduction pass 6 of 6 with root caching on. The bridge question proposed in F1–F3 would not have worked: the bridge picks among same-id targets by position, and both pages sit at the same place, so the shown page would have answered for both. Read "stays in the tree" below as "keeps answering after leaving the tree". Follows [windows-vs-headless.md](windows-vs-headless.md), where it was the suspected cause of two intermittent failures. Written to improve Bruinet.

## Summary

VID.01.3 ("More like this follows a musician") failed now and then on the Windows head, and never headless:
- it failed in 3 of 5 runs together with VID.01.1 and VID.01.2, and in 2 of 3 full UAT runs;
- alone it passed 5 of 5.

Its error was *'AutomationId:Results_Items' was found but is not visible*. Yet the failure screenshot shows the card it looked for, Judee Sill, at the top of the screen.

**Cause:**
- "More →" pushes a second results page onto the first.
- The UAT runner holds one page object per page type and scenario, so the same `ResultsPage` object serves both pages. That object had cached the first page's root element.
- Bruinet re-checks a cached page root only for "alive and shown". On Windows the covered first page passes both checks for about a second after the push:
  - it is still in the UI Automation tree, so it is not stale;
  - UI Automation reports it not off-screen, so it counts as shown, because `IsOffscreen` does not account for one page covering another.
- So the page object kept answering from the covered page, whose cards are hidden. Whether the next step fell inside that second was timing, hence intermittent.

**Why headless never failed:** MauiHeadless defines *visible* as "on the shown page", and its tree holds only Shell's current page. A covered page drops out at once, the cached root fails its check, and the page is found again.

**Fixes:**
- **Kofflard:** the results page object no longer caches its root.
- **Bruinet, proposed:** let the Windows head ask the app which page is shown. The in-app bridge already has that check (`MauiCapabilities.IsOnScreen(Page)`), but only menu items use it.

## How this relates to Bruinet's design

The architecture expects this case. The call model ([call-model.md](../../external/test-tooling/.docs/contracts/call-model.md)) says:

- **R4:** "A page asks nobody: its root is present and shown, then `IsLoaded()`, then its busy signal."
- **R6:** "One check that a cached root is still alive, used by every scope that caches a root."

The scope chain did run on every call. What failed is the Windows head's answer to "shown" for a page. The rule is right, and the answer is wrong:

| Head | What "shown" is, for a page root | Covered page |
| --- | --- | --- |
| MauiHeadless | on the shown page (`Screen.CurrentPage`) | not shown: absent from the tree |
| Android (Appium) | found at all (`IShownWhenFound`): UiAutomator leaves out what is not drawn | not shown (not checked here) |
| Windows (FlaUI) | `!IsOffscreen` and a non-empty rectangle (`MauiElementExtensions.HasUsableBounds`) | **shown, for about a second after the push** |

`IShownWhenFound` already records the Windows side of this: "Windows keeps off-screen and hidden elements in its tree, so its elements do not say this."

## Impact

- **Kofflard:** VID.01.3 failed intermittently on Windows. By the same mechanism, any step that looks into a results page straight after "More →" or Back could fail. Users are not affected.
- **Bruinet:**
  - Any page type that can be on the stack twice, or that is navigated away from and back to quickly, is exposed on Windows.
  - It is most exposed in UAT, where page objects live per scenario.
  - xUnit tests that create a new page object per page (`ResultsPage.Arrived`) were not affected: a new object finds the root again, and the shown page comes first in the tree.

## Evidence

### 1. The screen at the failure

The UAT failure screenshot of VID.01.3: the "Like Bill Fay" page, with Judee Sill as the first card, on screen. The step that failed, `Suggestions should suggest "Judee Sill"`, said the list was not visible. So the list it searched was not the one on screen.

### 2. Which page a page object reads

`StackedPageDiagnosticTests`, Windows head. It searches "Nick Drake", explores "Bill Fay", and then samples the one-line summary through two page objects:
- the **first** object, used before "More →", as the UAT runner does;
- the **second** object, new after it.

| After "More →" | First object reads | Second object reads |
| --- | --- | --- |
| Windows, 0 ms | **"English folk singer-songwriter, 1969–74"**: Nick Drake's page, covered | "English singer-songwriter and pianist, 1970–": Bill Fay's |
| Windows, 1 s to 5 s | Bill Fay's | Bill Fay's |
| Headless, 0 ms to 5 s | Bill Fay's | Bill Fay's |

Over the same five seconds both objects reported themselves loaded, with their lists visible, on both heads. So nothing a page object can check today tells the covered page from the shown one.

### 3. Reproduced at will, then removed

| Run | Root cached (as built) | Root not cached |
| --- | --- | --- |
| First page object reused straight after "More →", Windows | 6 of 6 fail (*Suggestion_Name … not visible*) | 6 of 6 pass |
| Same, headless | 2 of 2 pass | — |
| VID.01.1 to VID.01.3 together, Windows | 3 of 5 fail | 6 of 6 pass |
| Full Windows UI suite | the lanes journey failed in 1 of 3 runs | 4 of 4 pass |
| Full Windows UAT suite | VID.01.3 failed in 2 of 3 runs | VID.01.3 passed in 3 of 3 |

### 4. Why it looked order-dependent

- A new page object always found the right page, so xUnit tests and lone runs passed.
- The UAT runner registers page objects per scenario (`TestComposition`, `ServiceLifetime.Scoped`): "I should be on the Results page" binds the first page, and "I explore" keeps that object.
- The window in which the covered page still passes is about a second. Whether the next step falls inside it depends on how long the steps between take.

## Root cause

**In Bruinet's Windows head: "shown" for a page root is answered by UI Automation's `IsOffscreen`, which does not see one page covering another.**

`PageObjectBase` resolves its root as the first element with the page's id that `HasUsableBounds()`, and re-checks a cached root with the same test (`IsCachedRootValid`).

On Windows, after a push, the covered page stays in the tree with `IsOffscreen = false` and its full rectangle until WinUI unloads it, about a second later. In that time:
- a cached root pointing at it passes R6 (alive) and R4 (shown);
- a newly made page object would find the new page, which comes first in the tree, but an existing one never looks again.

The information needed is in the app. The bridge in `Bruinet.Maui.AppSupport` has `MauiCapabilities.IsOnScreen(Page)`: "Whether this page is the one the user is looking at. On top of its navigation stack, and not covered by a modal. In a Shell app, Shell's own current page". The Windows head never asks it about pages; it is used only to refuse menu items on a page not shown.

The bridge has already been bitten by stale pages. The notes on `NavigateBack` and `NavigationDepth` describe a popped page that "keeps its handler for a while and stays answerable", and both verbs now refuse to answer for a page that is not on top. Page roots never got the same rule.

## Contributing factors

- **Page objects per scenario in UAT.** It is a sensible lifetime, but it means a page type visited twice in a scenario reuses one object, and so one cached root.
- **The error named the list, not the page.** "Results_Items not visible" pointed at the list, while the problem was which page the list belonged to (see B5 in [windows-vs-headless.md](windows-vs-headless.md)).
- **No test in Bruinet pushes the same page twice.** The Todo sample's pages are all different types.

## Actions

### Bruinet: what was built

| # | Change | Where |
| --- | --- | --- |
| Done | `IDetachable { bool IsAttached }`: whether an element is still in its app's tree. The Windows element walks the control view up to the app window. | `Bruinet.Maui.Interfaces`, `FlaUIMauiElement` |
| Done | A cached page root is valid only while it is attached as well as shown. | `PageObjectBase.IsCachedRootValid` |
| Done | Pin test `PageRoot_DetachedButAnswering_IsFoundAgain`; the call model says it (R6). | `ListAndPageRootPinTests`, `.docs/contracts/call-model.md` |

### Bruinet: fix it in the Windows head (as first proposed, superseded)

| # | Action | Where |
| --- | --- | --- |
| F1 | The bridge answers `GetState("IsShownPage")` for a page, with the existing `IsOnScreen(Page)`: `True` or `False`, and `UIA_E_NOTSUPPORTED` for an element that is not a page. No new verb. Pages already declare `GetState` (Kofflard's `PageAutomation` does). | `MauiVerbDispatcher.ReadState`, `MauiCapabilities.IsOnScreen` |
| F2 | The Windows element answers "is this the shown page". If the element declares `GetState`, ask F1. If it does not, answer null (unknown), not true. A small interface, as `IShownWhenFound` is: `IPageShown { bool? IsShownPage { get; } }`, implemented by `FlaUIMauiElement`. Headless implements it from `Screen.CurrentPage`. | `Bruinet.Maui.Interfaces`, `FlaUIMauiElement`, `MauiHeadlessElement` |
| F3 | A page root is shown only if it is the shown page. In `PageObjectBase`, `FindContainerRootElement` takes the match that answers `IsShownPage == true` (else the first with usable bounds, as now). `IsCachedRootValid` drops a cached root that answers `false`. This is R4 as written ("present and shown"), with "shown" now true for pages on Windows. | `PageObjectBase` |
| F4 | Without a bridge (F2 answers null), a cached page root is checked against the tree once more when more than one element carries the page's id. The cost is one extra `FindElements` per check, paid only by page types that can be stacked. | `PageObjectBase.IsCachedRootValid` |
| F5 | A regression in Bruinet: a sample page pushed onto itself (detail → related detail), and a test that reuses the first page object straight after the push, on the Windows head and headless. | `samples/Bruinet.Samples.Maui.App`, `tests/Bruinet.Maui.UITests` |
| F6 | Write the rule into the call model: "shown, for a page, means the page the app shows; a head answers it from the app where its tree cannot (Windows)". | `.docs/contracts/call-model.md` |

With F1–F3, a page object that outlives a navigation follows the shown page on every head, and the per-scenario page objects in UAT need no change.

### Bruinet: when a fix is not possible, make the difference known

This is the rule for head differences that are not fixed: write them down, pin them with a test, and keep headless free of a layout engine. This difference needs no layout to model. If F1–F3 are not done:
- **Document it** in `docs/guides/maui-headless.md`, under "What Behaves Differently": "On Windows, a page covered by a push stays in the tree, on screen by UI Automation's account, for about a second. A page object that cached it keeps it unless the page is found again."
- **Pin it with a test** (F5), marked `[DeviceFact]` with that reason, so the difference is listed in every headless run.

### Kofflard

| # | Action | Status |
| --- | --- | --- |
| K1 | `ResultsPage` found its root again on every call (`CacheContainerRoot => false`). | Removed: Bruinet's attachment check replaces it |
| K2 | `StackedPageDiagnosticTests` kept as a regression: reuses the first page object straight after "More →", and must read Bill Fay's page and find Judee Sill. | Done |

## Not this cause

- **DISC.03.1 failed once in a full Windows UAT run** (1 of 3 runs, after K1). The step was "I should be on the Results page": "I tap Go" had passed, but the screenshot shows the app still on Search with "Nick Drake" typed in. The tap did not navigate. It is a different problem and still open.
- **The adventure buttons are empty on Windows.** The same screenshot shows the five boxes without their icons or names. It is an app bug in the templated `RadioButton` on WinUI, unrelated to this RCA. Headless cannot show it, as nothing is drawn there.
