# Kofflard

A self-contained .NET MAUI app that helps people find music. You type a musician's name, and an AI you connect (with your own key, or by signing in to OpenRouter) suggests other musicians. The suggestions come in three lanes, close, stretch and leap, and each has a reason. You open any of them in YouTube, YouTube Music or Spotify: in the browser on Windows, and in the app on Android and iOS.

The design, and the serendipity research it builds on, is in [.my/design/kofflard-design.md](.my/design/kofflard-design.md).

## Layout

| Path | What |
| --- | --- |
| `src/Kofflard.Core` | Models, rules (lane mix, prompt, validation, links), ports and view models. No MAUI, HTTP or SQL. |
| `src/Kofflard.Infrastructure` | AI providers (the OpenAI SDK through Microsoft.Extensions.AI), OpenRouter sign-in, MusicBrainz, SQLite stores, launch settings and dependency injection. |
| `src/Kofflard.App` | The MAUI app: Shell, pages, platform code. Targets Windows and Android, plus iOS on a Mac; `net10.0` is the headless test build. |
| `tests/Kofflard.UnitTests` | Rules and view models. |
| `tests/Kofflard.IntegrationTests` | The AI engine against a fake OpenAI-compatible server, the stores, MusicBrainz, and OpenRouter's key exchange. |
| `tests/Kofflard.TestSupport` | `FakeAi` (WireMock) and its canned answers. |
| `tests/Kofflard.UITests` | Bruinet page objects and journeys: headless, Windows (FlaUI), Android (Appium). |
| `tests/Kofflard.Uat` | Markdown scenarios (`Features/*.feature.md`) over the same page objects. |
| `external/test-tooling` | Bruinet, a git submodule (`git submodule update --init`). |

## Build and run

```bash
git submodule update --init
```

```bash
dotnet build src/Kofflard.App -f net10.0-windows10.0.19041.0
```

```bash
dotnet run --project src/Kofflard.App -f net10.0-windows10.0.19041.0
```

The first start opens **Connect an AI**. Azure OpenAI / Foundry is listed first. Enter its endpoint, the deployment name and a key, then press **Test** and **Done**. The key is kept in the device's secure storage.

## Test

```bash
dotnet test tests/Kofflard.UnitTests
```

```bash
dotnet test tests/Kofflard.IntegrationTests
```

```bash
BRUINET_HEAD=headless dotnet test tests/Kofflard.UITests
```

```bash
BRUINET_HEAD=headless dotnet test tests/Kofflard.Uat
```

Without `BRUINET_HEAD`, the UI and UAT suites drive the real Windows app (build it first). No test calls a real AI: the app is pre-connected to `FakeAi`, and every link it would open is written to a launch log instead.
