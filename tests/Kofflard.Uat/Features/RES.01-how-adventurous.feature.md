# Feature: RES.01 How adventurous

The results come in three lanes, Safe, Curious and Wild, each under a header with the lane's total. The
slider above them shows the lanes up to the one chosen: a new install shows only Safe, and the slider
stays where it was for the next results (.my/design/adventure-as-outcomes.md).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | RES.01 |
| Tags | maui, results |

@RES.01.1 @maui
## Scenario: RES.01.1 Only Safe shows at first, and the slider stays where it was

```gherkin
Given the AI answers "Bill Fay" with "bill-fay"
And I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
And the lanes should be "Safe 6 · Curious 4 · Wild 2"
And "Safe" adventure should be chosen
And "Vashti Bunyan" should be shown
And "Bill Fay" should not be shown
When I choose "Curious" adventure
Then "Bill Fay" should be shown
And "Ali Farka Touré" should not be shown
When I tap Back
Then I should be on the Search page
When I search for "Bill Fay"
Then I should be on the Results page
And "Curious" adventure should be chosen
```

@RES.01.2 @maui
## Scenario: RES.01.2 A long press offers the other two services

```gherkin
Given I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
When I open "Vashti Bunyan" in "Spotify"
Then "Vashti Bunyan" should say "Opened in Spotify ✓"
```
