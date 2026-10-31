# Feature: RES.01 The lanes

The results come in three lanes, Safe, Curious and Wild, each under a header with the lane's total, Safe
first (.my/design/kofflard-design.md §3.2).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | RES.01 |
| Tags | maui, results |

@RES.01.1 @maui
## Scenario: RES.01.1 Every lane shows, Safe first

```gherkin
Given I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
And the lanes should be "Safe 6 · Curious 4 · Wild 2"
And "Vashti Bunyan" should be a "Safe" suggestion
And "Ali Farka Touré" should be a "Wild" suggestion
```

@RES.01.2 @maui
## Scenario: RES.01.2 A long press offers the other two services

```gherkin
Given I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
When I open "Vashti Bunyan" in "Spotify"
Then "Vashti Bunyan" should say "Opened in Spotify"
```
