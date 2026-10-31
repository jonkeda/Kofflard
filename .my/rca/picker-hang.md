# RCA: the Connect provider picker hung the Windows app under test

Date: 2026-10-31. Status: root cause found and fixed in Kofflard. Upstream MAUI and Bruinet issues are not yet filed.

## Summary

During the Windows UAT run, the step `I select "Azure OpenAI / Foundry" from Provider` opened the provider dropdown and never returned. The app's UI thread was blocked, so every UI Automation call after that timed out, and the run waited until it was stopped by hand.

The cause is in **.NET MAUI 10's new `Picker.IsOpen` sync on Windows**:
1. When the WinUI `ComboBox` raises `DropDownOpened`, MAUI sets `Picker.IsOpen = true`.
2. The `IsOpen` property mapper then writes `ComboBox.IsDropDownOpen` back, from inside that same event.
3. When the dropdown was opened through UI Automation, as Bruinet does, the ComboBox could still report itself *closed* at that moment. So the write went through, and the native call blocked the UI thread for good.

It is a race: about 60% of runs hung.

**Fix:** Kofflard never opens a picker from code, so on Windows the `IsOpen` mapping is made a no-op (`src/Kofflard.App/MauiProgram.cs`). After the fix: 10 of 10 runs pass, each selection taking 24–30 ms.

## Impact

- **Tests:** the UAT suite hung on Windows, and a video recording would have hung the same way. Headless runs were not affected, because MauiHeadless has no WinUI ComboBox.
- **Users:** not observed. The hang was reproduced only when the dropdown was opened through UI Automation. A mouse click raises the same event, but no hang was seen; that has not been tested systematically. Screen readers also use UI Automation, so they may be exposed; the fix covers them too.

## Timeline

| When | What |
| --- | --- |
| 00:5x | The Windows UAT run stops responding on AI.01.1, with the dropdown open and "Local" still marked (the user's screenshot). The run is stopped. |
| 01:2x | Workaround so the video could be recorded: the tests start connected to Azure, and scene 7 no longer opens the dropdown. Video recorded. |
| 01:4x | Diagnostic test `PickerDiagnosticTests` times each phase of a selection, with a 30 s watchdog per phase. |
| 01:5x | Reproduced; hypothesis 1 refuted; hang shown to be intermittent; stacks captured; fix 1 failed; fix 2 confirmed (below). |

## Evidence

### 1. It is the selection, on Connect only

`PickerDiagnosticTests`, Windows head:

| Phase | Settings › Open with | Connect › Provider |
| --- | --- | --- |
| open (Expand) | 32 ms | 17 ms |
| read items | 13 ms | 10 ms |
| close (Collapse) | 8 ms | 3 ms |
| SelectByText | 41 ms | **21 966 ms: UIA Timeout** |
| any later call | fine | **~21.8 s: COMException 0x80131505** (the app no longer answers) |

On Windows, Bruinet's `SelectByText` for a picker that declares no bridge verb goes through `FlaUIMauiElement.SelectFromDropdown`:
1. `ExpandCollapse.Expand()`, then wait up to 2 s for it to report open.
2. Find the `ListItem` descendants.
3. `SelectionItem.Select()` on the match.
4. Wait up to 2 s for it to close, then `Collapse()`.

Every wait in that path is bounded, so a hang of minutes means a blocked cross-process call, which points at a blocked UI thread in the app.

### 2. Hypothesis 1 (the view model's side effects): refuted

Choosing a provider resets Model, clears the status, and shows or hides the endpoint, key and Sign in controls. With all of that switched off, the selection still hung (1 run of 5). With the side effects back on, 3 of 5 runs hung. So the side effects change the odds, but they are not the cause, and the hang is a race.

### 3. The UI thread's stack during a hang

From `dotnet-stack report -p <app>`, taken 12 s into the hang:

```
Thread (UI):
  [Native Frames]
  Microsoft.Maui.Handlers.PickerHandler.MapIsOpen(IPickerHandler, IPicker)
  Microsoft.Maui.Controls.BindableObject.SetValueActual(…)
  Microsoft.Maui.Controls.Picker.set_IsOpen(bool)
  Microsoft.Maui.Handlers.PickerHandler.OnMauiComboBoxDropDownOpened(object, object)
  … WinRT event dispatch …
  Microsoft.UI.Xaml.Application.Start(…)
  Kofflard.App.WinUI.Program.Main(string[])

Thread (finalizer):
  WinRT.Context.CallInContext(…)   ← waiting to get onto the blocked UI thread
  WinRT.IObjectReference.Finalize()
```

The UI thread is inside WinUI's `DropDownOpened` → MAUI `Picker.IsOpen = true` → `MapIsOpen` → native ComboBox, and does not come back.

### 4. Fix 1 (write only when the state differs): failed

`PickerHandler.Mapper.ModifyMapping("IsOpen", …)` was changed to call the original mapping only when `ComboBox.IsDropDownOpen != Picker.IsOpen`. It still hung in 5 of 8 runs. The new stack showed the guarded mapping itself calling `MapIsOpen`. So inside `DropDownOpened` the ComboBox reported **closed** while MAUI's `IsOpen` said open, the guard let the write through, and that write blocked.

### 5. Fix 2 (no `IsOpen` mapping on Windows): confirmed

With `PickerHandler.Mapper.ModifyMapping("IsOpen", (handler, picker, map) => { })` on Windows, 10 of 10 runs pass, each selection taking 24–30 ms.

## Root cause

**.NET MAUI 10.0.110 on Windows.** `PickerHandler` mirrors the WinUI ComboBox's open state into the new `Picker.IsOpen` property on `DropDownOpened`, and the `IsOpen` mapper mirrors it straight back to `ComboBox.IsDropDownOpen`. That is a feedback loop through the platform control, run synchronously inside the platform's own event.

When UI Automation expands the dropdown, the ComboBox's own state can lag the event. The loop then *sets* `IsDropDownOpen` while WinUI is still opening the popup, and the native call never returns. Mouse-driven opening apparently keeps the state in step, which is why it is a race and not a constant failure.

**Why it hit Connect and not Settings:** the Settings picker never hung in our runs; the Connect picker hung in 3 of 5 (no fix) and 5 of 8 (fix 1). The Connect picker does more work when its selection changes (more bindings, and controls shown and hidden), so the popup's timing differs. The side-effect experiment fits this: they change the odds, not the outcome.

## Contributing factors

- Bruinet drives WinUI pickers through UI Automation `ExpandCollapse`, not inside the app, because Kofflard declares no `SelectByText` bridge verb on its pickers. The in-app route (`uia:GestureAutomation.Verbs`) would never open the popup.
- UI Automation calls have no deadline shorter than the ~22 s UIA timeout, and the UAT runner has no per-step watchdog. So the hang looked like a frozen run, not a failed step.

## Actions

| # | Action | Status |
| --- | --- | --- |
| 1 | Make the `IsOpen` mapping a no-op on Windows (`MauiProgram.cs`), with a comment pointing here | Done |
| 2 | Keep `PickerDiagnosticTests` as a regression test (Connect and Settings pickers, phase timing, a watchdog, and a failed phase fails the test). Without the fix it failed 1 run in 4 here (3 in 5 earlier), so one green run proves little: run it about 10 times after touching the mapping | Done |
| 3 | AI.01.1 can select a provider again; it was rewritten to avoid the dropdown for the recording | Open (optional) |
| 4 | File a MAUI issue: "WinUI PickerHandler: setting IsDropDownOpen from DropDownOpened (IsOpen sync) can block the UI thread when opened via UI Automation", with this repro | Open |
| 5 | Bruinet: a per-step watchdog in the UAT runner, so a blocked UIA call fails the step in seconds instead of hanging the run | Open (Bruinet, on `move`) |
| 6 | Consider declaring the `SelectByText` bridge verb on Kofflard's pickers, so tests select inside the app on Windows | Open |
| 7 | Drop the workaround in action 1 when a MAUI release fixes the loop; check by removing it and running `PickerDiagnosticTests` 10 times | Open |
