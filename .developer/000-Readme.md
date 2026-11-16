# .developer: how Kofflard is changed, and the record of it

Kofflard is a development project, so its record folder is `.developer/`. A change is done as
small steps through a loop, and each step leaves a record here. This is the first product where
the folder is tried, so what it lacks or holds for nothing is itself a finding: write it in the
friction log.

| Path | What |
| --- | --- |
| [100-Loops.md](100-Loops.md) | The state of each slice, and what waits for a person. Read this first. |
| [200-Steps/](200-Steps/README.md) | One file per step: what, the check, the planted fault, the result. |
| [300-Measures.md](300-Measures.md) | The measures per step. |
| [400-FrictionLog.md](400-FrictionLog.md) | Every place the work was slow, manual or wrong, and the change it asks for. |
| [500-Benchmark/](500-Benchmark/README.md) | Steps kept so they can be run again after a tool changes. |
| [600-Retros/](600-Retros/README.md) | One per slice: the numbers, three proposed changes, what was picked. |
| [decisions/](decisions/README.md) | Numbered decisions: the why. |

`.my/` stays the owner's scratch space, with the designs.

## How to run a step

1. Write the step file in `200-Steps/` from its [template](200-Steps/README.md). A step file
   without a check is refused: no check, no step.
2. Write the check first and see it red. For a journey that is a scenario in
   `tests/Kofflard.Uat/Features/`; for a rule, a test in `tests/Kofflard.UnitTests`.
3. Build until the check is green.
4. Plant the step's fault and see the check go red, then take the fault out. A check that has
   never failed is not trusted.
5. Run the gates below. All green: commit, and fill in the step's result and its row in
   `300-Measures.md`. Red twice: split the step.
6. Anything slow, manual or wrong becomes a row in `400-FrictionLog.md`.

## The gates

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

No gate needs a device or a real AI: the app is connected to `FakeAi`.
