# .my

Kofflard's own notes: the design, the root-cause analyses, and the narrated videos.

## Design

| File | What |
| --- | --- |
| [design/kofflard-design.md](design/kofflard-design.md) | The design: goals, screens, lanes, AI, links, data, tests. Start here. |
| [design/one-page-desktop.md](design/one-page-desktop.md) | The one page and its wide layout: search pane, artist map, lane columns. |
| [design/single-page-phone.md](design/single-page-phone.md) | The narrow layout: the list, the dock at the bottom, the toggles, the trail. |
| [design/streaming-answers.md](design/streaming-answers.md) | Cards appearing as the AI writes them. |
| [design/aurora.md](design/aurora.md) | The look: the sky, the orb, the lane colours. |

The mock-ups are on the design canvas (claude.ai/artifact/2JsCoSm3nwxwTWBNaVRBRJ).

## Root-cause analyses

| File | What |
| --- | --- |
| [rca/card-click.md](rca/card-click.md) | A click on a card on Windows: the list's selection opens it. |
| [rca/picker-hang.md](rca/picker-hang.md) | The picker that blocked the UI thread on Windows. |
| [rca/windows-vs-headless.md](rca/windows-vs-headless.md) | Why the lanes test differed between Windows and headless. |
| [rca/stacked-pages.md](rca/stacked-pages.md) | A covered results page answering for a second on Windows (fixed in Bruinet; no stack since the one page). |
| [rca/android-and-ios.md](rca/android-and-ios.md) | What the Windows findings mean on Android, and predicted for iOS. |

## Videos

| Folder | What |
| --- | --- |
| [video/kofflard-desktop](video/kofflard-desktop) | Kofflard on the desktop: De Dijk, Doe Maar, Eminem, Favourites. |
| [video/kofflard-phone](video/kofflard-phone) | The same tour on a phone. |
| [video/kofflard-in-a-minute](video/kofflard-in-a-minute) | The first video, with Nick Drake, from before the one page. |

Each is recorded from `tests/Kofflard.Uat/VideoScripts/*.video.md`; the phone's with `KOFFLARD_LAYOUT=phone`.
A `*.scenes` folder keeps the scenes, so one can be recorded again and the video stitched again.
