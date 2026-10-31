# UAT Config

## Runtime

| Field | Value |
| --- | --- |
| Target | MAUI |
| Fixture | KofflardUatFixture |
| Head | maui |
| AppPath | ../../src/Kofflard.App/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Kofflard.App.exe |
| WorkingDirectory | ../.. |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | bin/Debug/net10.0-windows/Kofflard.Uat.dll |
| Controls | ../../external/test-tooling/src/Bruinet.Maui/bin/Debug/net10.0/Bruinet.Maui.dll |
| Commands | ../../external/test-tooling/src/Bruinet.Uat/bin/Debug/net10.0/Bruinet.Uat.dll |

## Using

The subjects outside the screen every scenario may arrange or check (phrase namespaces).

| Namespace |
| --- |
| Ai |

## Discovery

| Field | Value |
| --- | --- |
| RequireExplicitUatAttributes | false |
| AllowNameInference | true |

## Reporting

| Field | Value |
| --- | --- |
| ScreenshotOnFailure | true |
| IncludeRuntimeTrace | true |

## Settings

| Field | Value |
| --- | --- |
| Root | TestSettings |
| DefaultFile | testsettings.json |
| LocalFile | testsettings.local.json |
| ScenarioConvention | scenarios/{ScenarioId}.json |
