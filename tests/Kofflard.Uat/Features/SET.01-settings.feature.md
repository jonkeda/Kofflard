# Feature: SET.01 Settings

The settings a new install starts with, and Open with changing which service a tap on a card opens
(design §3.3). How adventurous is on the results now (RES.01).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Settings |
| Target | MAUI |
| Journey | SET.01 |
| Tags | maui, settings |

@SET.01.1 @maui
## Scenario: SET.01.1 A new install opens YouTube and does not check names

```gherkin
Given I am on the Search page
When I tap Settings
Then I should be on the Settings page
And Open With should have selected "YouTube"
And Check Names should be unchecked
```

@SET.01.2 @maui
## Scenario: SET.01.2 Choosing Spotify makes it what a tap on every card opens

```gherkin
Given I am on the Search page
When I tap Settings
Then I should be on the Settings page
When I choose to open with "Spotify"
And I tap Back
Then I should be on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
And "Bert Jansch" should open in "Spotify" first
```


