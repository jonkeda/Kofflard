# Kofflard — video design

A narrated video of Kofflard, about 90 seconds long. Bruinet records it from the app's own UAT scenarios, so the video is re-made by running tests, not by editing footage. The voice is Tomorrow's: Azure Speech, en-GB-Emily.

Status: built and recorded, 2026-10-31 (§6 done; scene 7 changed, see below). Owner: Daan Jonkers. It builds on [kofflard-design.md](kofflard-design.md) and Bruinet's [narrated recordings guide](../../external/test-tooling/docs/guides/narrated-recordings.md).

---

## 1. Goals

1. One short video, **"Kofflard in a minute and a half"**, that shows what the app is for and why it is different. It covers:
   - the three lanes and their bridges
   - opening a suggestion in the service you prefer
   - the user's own AI, with no Kofflard server
2. It is made by Bruinet from scenarios that also run as tests. When the UI changes, re-recording is one command, and a scene that no longer fits fails like a test.
3. It can be repeated: by default the same answers, the same pacing and the same voice clips (Bruinet caches them).
4. No secret ever reaches the video, the captions or the timeline.

**Non-goals.** Recording on a phone (the Android head works, but its screen recording is a later step). Editing by hand. Music under the voice.

## 2. Secrets (done)

Both secret files are copied from Tomorrow and ignored by git (`.gitignore`: `**/voice.secrets.json`, `**/ai.secrets.json`). Neither value is in this document or in any commit.

| File | Holds | From Tomorrow | Used by |
| --- | --- | --- | --- |
| `tests/Kofflard.Uat/voice.secrets.json` | `azureSpeech` (Emily) and `azureFoundry` (`azure:coral`) voices | `external/test-tooling/voice.secrets.json` | Bruinet's narrator. It looks beside `uat.config.md`, then in folders above it, then in `%USERPROFILE%\.bruinet` |
| `ai.secrets.json` (repository root) | `azure`: endpoint, deployment, key | the endpoint and deployment from `src/Tomorrow.Studio/appsettings.json` (`AI:Foundry`), the key from Tomorrow's user-secrets (`AI:Foundry:Key`) | `LiveAi` in `Kofflard.TestSupport`: the **Live** integration tests, and `KOFFLARD_LIVE_AI=1` for the UI and UAT suites |

- `ai.secrets.example.json` shows the shape and is committed.
- The `KOFFLARD_LIVE_AI_ENDPOINT`, `_DEPLOYMENT` and `_KEY` variables override the file, for a build agent.
- `BRUINET_VOICE_*` and `BRUINET_SPEECH_*` do the same for the voice.

The live AI reaches the app the way the fake one does: as launch settings (`KOFFLARD_AI_PROVIDER=azure`, `KOFFLARD_AI_BASEURL`, `KOFFLARD_AI_MODEL`, `KOFFLARD_AI_KEY`). The key is held in memory, never in the app's database, and never typed into a field. So a video recorded live shows no key either.

Checked on 2026-10-31: `dotnet test tests/Kofflard.IntegrationTests --filter Category=Live` passes against Tomorrow's Foundry deployment (`gpt-6-luna`): a connection test and a real search. That run found and fixed a bug: the connection test allowed only 16 output tokens, which a reasoning model spends before it answers.

## 3. Fake AI or live AI

| | Fake AI (`FakeAi`, the default) | Live AI (`KOFFLARD_LIVE_AI=1`) |
| --- | --- | --- |
| Answers | canned, real musicians (`Answers/nick-drake.json`, …) | whatever the model says today |
| The same every take | yes | no |
| Scenarios can check names | yes | no: only states, counts and that leaps have bridges |
| Cost and time | none, instant | one model call per search, about 15 s each |
| Honest about the product | the answers are hand-written | yes |

**Decision: record the video with the fake AI**, and record one optional closing scene live. The canned answers are real musicians with real albums, so nothing shown is false. The video can be re-made every time the UI changes, and a scene never waits 15 seconds for a model. The narration does not claim the answers are live; it says what the app does.

The live scene ("Your AI, your answers") runs a scenario with no name checks (§5, scene 7), tagged `@live`. It is recorded only when `KOFFLARD_LIVE_AI=1`; otherwise it is left out of the video (Q2).

## 4. Where it records

- **Head:** the Windows app (FlaUI). The window recorder captures exactly the app's window.
- **Tools:**
  - **ffmpeg** is on `PATH` (installed with winget).
  - **The recorders.** A `net10.0-windows` suite cannot reference the window recorder, so it is loaded from the folder that `BRUINET_RECORDERS` names. Build it once with `dotnet build external/test-tooling/tools/Bruinet.Cli` and point `BRUINET_RECORDERS` at its `recorders` folder.
- **Window size:** set a fixed 1280×800 when the app is under test (a launch setting, §7), so every take frames the same way.
- **Voice:** `azure-speech-service:en-GB-Emily:MAI-Voice-2.1`, as in Tomorrow's `todo-emily` and `platform-020-emily` videos. Coral (`azure:coral`) is the fallback; both are in the copied secrets.
- **Touches:** on, in Kofflard's teal `#0F766E` rather than Exact red.

## 5. Storyboard

About 90 seconds over seven scenes. Each scene is one scenario. Rows of the script are the scenario's steps, with the line said while each one runs.

| # | Scene | Scenario | Shows | ~s |
| --- | --- | --- | --- | --- |
| 1 | Who do you like? | DISC.01.1 (exists) | type a name, Go, twelve suggestions arrive | 15 |
| 2 | Three lanes | VID.01.1 (new) | scroll from Close to Stretch to Leap; read a leap's bridge | 15 |
| 3 | Open it anywhere | DISC.03.1 + VID.01.2 (new) | the main button opens YouTube, and a small one Spotify; a visible "Opened in …" confirmation | 12 |
| 4 | Follow the thread | VID.01.3 (new) | "More like this →" on a card starts a new search from it | 10 |
| 5 | Which Genesis? | DISC.01.3 (exists) | an ambiguous name asks; pick one | 12 |
| 6 | Make it yours | SET.01.2 (exists) | Settings: Open with Spotify; the main button follows | 12 |
| 7 | Your AI, your answers | AI.01.1 (new) | Connect opens on Azure: provider, endpoint, deployment, key field | 10 |

### Draft script

The draft script is `tests/Kofflard.Uat/VideoScripts/kofflard-in-a-minute.video.md`; this is its first scene:

```markdown
# Video: Kofflard in a minute and a half

| Field | Value |
| --- | --- |
| Voice | azure-speech-service:en-GB-Emily:MAI-Voice-2.1 |
| Beat | 600 |
| Pause | 700 |
| TouchColor | #0F766E |

## Scene: Who do you like?

Scenario: Features/DISC.01-find-musicians.feature.md#DISC.01.1-Typing-a-name-shows-twelve-suggestions-in-three-lanes

Kofflard starts with one question: who do you like?

| Step | Say |
| --- | --- |
| When I enter "Nick Drake" into Name | Say, Nick Drake. |
| And I tap Go | Kofflard asks an AI for musicians you may not know yet. |
| Then I should be on the Results page | Twelve of them, in three lanes. |

Close ones first. Then stretches. Then a few leaps.
```

The other scenes follow the same pattern. Their lines:

| Scene | Lines (in order) |
| --- | --- |
| 2 Three lanes | "Close: the same quiet folk, well chosen." / "A stretch: less known, another year, the same feeling." / "And a leap: Ali Farka Touré, from Mali." / "Every leap says why: the same hushed fingerpicking." / Outro: "Relevance first. Surprise, but never out of the blue." |
| 3 Open it anywhere | "One tap opens YouTube." / "Or Spotify, or YouTube Music." / Outro: "On a phone, in the app. On Windows, in the browser." |
| 4 Follow the thread | "Like Bill Fay?" / "Follow him, and the next twelve start from there." |
| 5 Which Genesis? | "Some names are shared." / "Kofflard asks which one you mean." / "Genesis Owusu, then." |
| 6 Make it yours | "Spotify more your thing?" / "Make it the main button, everywhere." |
| 7 Your AI, your answers | "There is no Kofflard server." / "Bring your own AI. Azure, OpenAI, Claude, Gemini, or sign in with OpenRouter." / "Your key stays on your device." / Outro: "Kofflard. Find the music you didn't know you were looking for." |

Lines are short on purpose: each runs while its step runs, and the beat (600 ms) follows. Scene 2's line about the leap should be checked against `nick-drake.json`, so it never claims a bridge the data does not give.

### Pronunciations

`tests/Kofflard.Uat/pronunciations.md` holds how names are said:

| Term | Say | IPA |
| --- | --- | --- |
| Kofflard | Koff-lard | ˈkɒflɑːd |
| Touré | Too-ray | tuːˈreɪ |
| Bunyan | Bun-yan | |
| Owusu | Oh-woo-soo | |
| Perhacs | Per-hacks | |

## 6. What the app and the tests needed (done)

All of these are built. One change from the plan: on Windows, the provider dropdown hung the app under test (root cause: MAUI 10's Picker.IsOpen sync, fixed; see [../rca/picker-hang.md](../rca/picker-hang.md)). So the tests now start connected to **Azure** (the fake AI answers on Azure's `/openai/v1/` path as well), and the Connect scene shows Azure without opening the dropdown. Building this also exposed an app bug, now fixed: coming back from an explored musician ran the earlier search again and showed nothing.

| # | Change | Why |
| --- | --- | --- |
| 1 | An **"Opened in YouTube ✓"** line on the card for 2 s after an open (`Suggestion_Opened`) | In a test the link goes to the launch log, and on Windows a real browser would open outside the recorded window. Without the line, scene 3 shows nothing happening. It is also good for users on a slow handoff. |
| 2 | Scroll steps for scene 2: `I look at {item}` (scrolls a card into view) on `SuggestionList` | The leaps are below the fold; the viewer must see them. |
| 3 | New scenarios **VID.01.1–3** (`Features/VID.01-for-the-video.feature.md`) and **AI.01.1** (Connect: Azure fields filled, Test against the fake AI). Each is also a test. | Scenes 2, 3, 4 and 7. |
| 4 | Copy `VideoScripts\**\*.video.md` and `pronunciations.md` to the output in `Kofflard.Uat.csproj` | As in Todo's UAT project, so the narrator finds them. |
| 5 | A window-size launch setting (`KOFFLARD_WINDOW=1280x800`) read in `App.CreateWindow` on Windows | The same frame every take. |
| 6 | The Connect scene must not type a real key: it uses `sk-demo`, which the fake AI accepts | The video never shows a secret. The field is a password field anyway. |
| 7 | `@live` scenes skipped unless `KOFFLARD_LIVE_AI=1` (a check in `KofflardUatFixture.BeforeScenario`) | The gate never calls the real AI. |

## 7. How to record

From `tests/Kofflard.Uat`, after building the app for Windows and Bruinet's CLI:

```powershell
$env:BRUINET_RECORD    = "VideoScripts\kofflard-in-a-minute.video.md"
$env:BRUINET_RECORDERS = "..\..\external\test-tooling\tools\Bruinet.Cli\bin\Debug\net10.0\recorders"
$env:BRUINET_RECORD_KEEP = "1"
dotnet test --filter "Category=UAT"
```

The video, its `.srt` captions and its `.timeline.json` land in `TestResults\<run>\recordings`. With `BRUINET_RECORD_KEEP`, each scene is kept, so one scene can be re-recorded (here, or in Bruinet Presenter's Narrator mode) and the video stitched again. The finished video is copied to `.my/video/kofflard-in-a-minute/` (git ignores media there; Q3).

For the live closing scene, set `KOFFLARD_LIVE_AI=1` as well. The app then talks to the Foundry deployment in `ai.secrets.json`.

## 8. Checks before it is done

- The video plays in about 90 s; no scene is over 20 s.
- Every line is heard in full before the next one starts (Bruinet waits for it), and the captions match.
- Search for the key's first eight characters in the video's `.srt` and `.timeline.json`: it must not appear.
- A dropdown (Open with) stays open long enough to read. Tomorrow's `.my/video/plan.md` describes Bruinet's fast-click problem. Check the Settings scene for it, and raise `Pause` there if needed.
- `dotnet test` without `BRUINET_RECORD` takes no longer than before: the pacing exists only while recording.

## 9. Open questions

| # | Question | Default |
| --- | --- | --- |
| Q1 | Emily or Coral as the voice? | Emily (Azure Speech), as in Tomorrow's recent videos |
| Q2 | Is scene 7 recorded live, or always with the fake AI? | Fake by default; live when the run sets `KOFFLARD_LIVE_AI=1` |
| Q3 | Are finished videos kept in git (`.my/video`), or only as release assets? | Not in git: add `.my/video/**/*.mp4` to `.gitignore`; keep the `.srt` and the script |
| Q4 | A phone version (Android, portrait) later? | Later, from the same script: only the head changes |
