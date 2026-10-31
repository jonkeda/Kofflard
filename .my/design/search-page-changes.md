# Change: a cleaner search page

Date: 2026-10-31. Status: agreed (answers under Decisions), not built.

The search page (the first page) has the words "Saved" and "Settings" in its title bar, a stepper for how adventurous, and a grey line naming the AI model. This change:
- swaps the two words for icons;
- makes the adventure level five buttons that work as radio buttons;
- drops the model line.

It changes design §3.1 (the search page) in [kofflard-design.md](kofflard-design.md). It goes with [results-list-changes.md](results-list-changes.md), which colours the title bar `Primary` with white text and starts Windows phone-sized. These icons are drawn for that bar.

## Before and after

```
Before                                   After
┌─────────────────────────────┐          ┌─────────────────────────┐
│ Kofflard     Saved Settings │          │▓ Kofflard         ★  ⚙ ▓│
├─────────────────────────────┤          ├─────────────────────────┤
│ Who do you like?            │          │ Who do you like?        │
│ [A musician or band       ] │          │ [A musician or band   ] │
│ How adventurous?  Curious [-|+]        │ How adventurous?        │
│ [      Find musicians     ] │          │  ⚓  [🔍]  🧭  ⛰  🚀   │
│ AI: Azure OpenAI · gpt-4.1… │          │ Safe Curious Open Bold Wild
│ Recent                      │          │ [    Find musicians   ] │
│ Nick Drake                  │          │ Recent                  │
└─────────────────────────────┘          │ Nick Drake              │
                                         └─────────────────────────┘
```

## The changes

| # | Asked for | What changes |
| --- | --- | --- |
| 1 | Saved as a star icon (favourites) | The `Search_Saved` toolbar item shows a star icon. It still opens the Saved page. Screen readers say "Favourites". |
| 2 | Settings as the settings icon | The `Search_Settings` toolbar item shows a gear icon. It still opens Settings. Screen readers say "Settings". |
| 3 | "How adventurous" as radio buttons | The stepper and the "Curious" label give way to five icon buttons, each with its name small underneath. Their icons are in [Icons for the adventure levels](#icons-for-the-adventure-levels).<br>• Exactly one is chosen: filled `Primary`, a white icon and white text. The others are `Secondary` with a `Tertiary` icon and text, as the second buttons on a card are today.<br>• Tapping one chooses it. It is saved straight away, as now. |
| 4 | Remove the line with the model details | `Search_Connection` ("AI: Azure OpenAI / Foundry · gpt-4.1-mini") goes from the search page.<br>• Settings still shows the connection.<br>• When an AI answer fails, the results page's error shows it too: see [Model line](#model-line).<br>• When no AI is connected, Find musicians already sends the user to Connect. |

## How

### Icons (`src/Kofflard.App/Resources/Images`, new)
- `star.svg` and `settings.svg`, as `MauiImage` items (the default `Resources\Images\*` glob in the csproj). MAUI turns them into PNGs at each density.
- The images are white, for the `Primary` title bar.
- SVG over a glyph font: it looks the same on Windows, Android and iOS, and adds no font.
- `SearchPage.xaml`: each `ToolbarItem` gets an `IconImageSource`.

  `Text` stays ("Favourites", "Settings"), because a toolbar item's text is its accessible name, and Bruinet's `ToolbarButton` finds it by `AutomationId`, which does not change.

  Windows shows a toolbar item's text under its icon in some layouts, so check it. If the text shows, set `Text` to empty and give the item `SemanticProperties.Description` instead.
- The Saved page is renamed **Favourites** to match the star: its title, its heading and its empty-state text.
  - The card's star says "Add to favourites" or "Remove from favourites" (results list change #7).
  - The route, the ids (`Search_Saved`, `saved`), the database table and the class names stay, so tests and links don't break. Only the words change.

### Icons for the adventure levels

Set A, "journey", chosen 2026-10-31: the five levels go from staying in harbour to leaving the planet. The two other sets considered are listed below.

| Level | A · Journey (chosen) | B · Weather | C · Signal |
| --- | --- | --- | --- |
| 1 Safe | anchor | sun | 1 bar |
| 2 Curious | magnifying glass | cloud | 2 bars |
| 3 Open | compass | wind | 3 bars |
| 4 Bold | mountain | storm cloud | 4 bars |
| 5 Wild | rocket | lightning | 5 bars |

- A tells a story that matches the words, and each icon reads on its own.
- B is pretty, but "sun" for Safe and "storm" for Bold say good and bad, which the levels are not.
- C shows the order best, but it says "how much", not "how far", and looks like phone signal.

The icons:
- Outline SVGs in `Resources/Images` (`adventure_safe.svg` … `adventure_wild.svg`), in the Tabler style, which is MIT-licensed. Credit it in the README.
- Two files per level, drawn in their colours: `Tertiary` (`adventure_bold.svg`) and white for the chosen one (`adventure_bold_on.svg`). Built: this replaced tinting at run time, so no tint behaviour has to work on the headless and Windows heads.
- 24 px icon, the name below it at 11 pt. Each choice is at least 48 px tall, the touch-target minimum.
- The name stays visible under the icon. It is also the radio button's accessible name, so screen readers say "Curious, radio button, 2 of 5".

### Adventure (`SearchPage.xaml`, `SearchViewModel`)
- Five MAUI `RadioButton`s in one `RadioButtonGroup` (`GroupName="Adventure"`), laid out by a `BindableLayout` over `AdventureOptions`.
- One `ControlTemplate` (in `Styles.xaml`, `AdventureChoice`) makes each look like an icon button:
  - a vertical stack of the icon `Image` and the name `Label`;
  - a `VisualStateManager` switches the background and the tint on `Checked`/`Unchecked`.
- Real radio buttons rather than five `Button`s, because:
  - a screen reader then says "Curious, radio button, 2 of 5, selected";
  - on Windows each one supports UI Automation's SelectionItem, which Bruinet's `RadioButton` control object uses.
- Layout: a five-column `Grid`, one row. With the name small under the icon, five fit at phone width: about 75 px each at 420.
- Ids: `Search_Adventure` on the group, and `Adventure_Safe` … `Adventure_Wild` on the choices.
- `SearchViewModel`:
  - `AdventureStep` and `AdventureName` go.
  - New `AdventureOptions` (Level, Name, Icon), and `SelectedAdventure` bound to `RadioButtonGroup.SelectedValue`, which sets `AdventureLevel`.
  - `AdventureLevel` and its save stay as they are.
- `Adventure` names stay: Safe, Curious, Open, Bold, Wild (`Adventure.cs`). A list of all five comes from a new `Adventure.All`.

### Model line
- `SearchPage.xaml`: remove the `Search_Connection` label.
- `SearchViewModel`: drop `ConnectionLine` and the work that fills it on load. `SettingsViewModel.ConnectionLine` stays.
- `ResultsPage.xaml`, Error state: under the message and above "Try again", a small grey `Results_Connection` label, "AI: Azure OpenAI / Foundry · gpt-4.1-mini". When an answer fails, it is the first thing you want to know.
  - `ResultsViewModel` gets `ConnectionLine`, read from `AiConnections` when it shows the error.
  - It shows only in the Error state, not on good results.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `ViewModelTests`:<br>• `search.AdventureStep = 4` becomes `search.SelectedAdventure = Adventure.All[3]`, checking the level is saved;<br>• `AdventureOptions` lists five, Safe first, each with its icon;<br>• a failed search sets the results' `ConnectionLine`. |
| UI (`Kofflard.UITests`) | `SearchPage`:<br>• `Adventure` (a `Stepper`) becomes `Adventure(name)`, a `RadioButton`;<br>• `AdventureName` becomes `ChosenAdventure`, reading the checked one;<br>• `Connection` goes.<br>`SavedButton` and `SettingsButton` keep their ids. Check that they still find the items once they have icons, on Windows especially. |
| UAT | A phrase "I choose {adventure} adventure", and a scenario in SET.01: choose Bold, go back to Search, Bold is still chosen.<br>A DISC scenario where the AI fails: the error shows the connection line.<br>Wherever a feature says "Saved", it says "Favourites". |
| Video | The search scene shows the five choices. If the script names "Saved", it says "Favourites". Rerecord with the results list change. |

## Decisions (answered 2026-10-31)

1. **Favourites.** The page and the card's star use "Favourites". The code keeps `saved`.
2. **Icons, with the name small underneath**, on one row. Set A, "journey": anchor, magnifying glass, compass, mountain, rocket.
3. **The model line also shows on the results page's error**, as well as on Settings.

## Not in this change
- The name box, Find musicians, and Recent stay as they are.
- The adventure levels and what they do to the lane mix (`LaneMix`) stay.
