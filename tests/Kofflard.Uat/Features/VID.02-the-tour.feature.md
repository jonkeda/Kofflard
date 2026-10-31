# Feature: VID.02 The tour

The scenarios the narrated videos record, on the desktop and on a phone
(`VideoScripts/kofflard-desktop.video.md`, `VideoScripts/kofflard-phone.video.md`): Dutch rock with De Dijk,
following it to Doe Maar, Eminem with the lanes turned up and down, and a full list of favourites to end
on. Each is a test too.

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | VID.02 |
| Tags | maui, video |

@VID.02.1 @maui @video
## Scenario: VID.02.1 De Dijk, from safe to wild

```gherkin
Given the AI answers "De Dijk" with "de-dijk"
And I am on the Search page
When I search for "De Dijk"
Then the lanes should become "Safe 6 · Curious 4 · Wild 1"
When I look at "Doe Maar"
And I look at "Boudewijn de Groot"
And I look at "Solomon Burke"
Then "Solomon Burke" should be a "Wild" suggestion
```

@VID.02.2 @maui @video
## Scenario: VID.02.2 Following De Dijk to Doe Maar, and back

```gherkin
Given the AI answers "De Dijk" with "de-dijk"
And the AI answers "Doe Maar" with "doe-maar"
And I am on the Search page
When I search for "De Dijk"
And I follow "Doe Maar"
Then Heading should equal "Like Doe Maar"
And Trail should equal "‹ De Dijk"
When I look at "The Specials"
Then "The Specials" should be a "Wild" suggestion
When I go back along the trail
Then Heading should equal "Like De Dijk"
```

@VID.02.3 @maui @video
## Scenario: VID.02.3 Eminem, as wild as you like

```gherkin
Given the AI answers "Eminem" with "eminem"
And I am on the Search page
When I search for "Eminem"
Then the lanes should become "Safe 5 · Curious 4 · Wild 3"
When I look at "Dido"
Then "Dido" should be a "Wild" suggestion
When I toggle "Wild"
Then the lanes should become "Safe 5 · Curious 4"
When I toggle "Wild"
Then the lanes should become "Safe 5 · Curious 4 · Wild 3"
When I open "Kendrick Lamar"
Then "Kendrick Lamar" should say "Opened in YouTube"
```

@VID.02.4 @maui @video
## Scenario: VID.02.4 Keep what you like, and find it in Favourites

```gherkin
Given the AI answers "De Dijk" with "de-dijk"
And the AI answers "Eminem" with "eminem"
And I am on the Search page
When I search for "De Dijk"
And I add "Doe Maar" to favourites
And I add "Solomon Burke" to favourites
And I add "Ramses Shaffy" to favourites
When I search for "Eminem"
And I add "Kendrick Lamar" to favourites
And I add "Danny Brown" to favourites
And I add "Dido" to favourites
And I tap Favourites
Then I should be on the Saved page
And Saved should include "Doe Maar"
And Saved should include "Solomon Burke"
And Saved should include "Dido"
```
