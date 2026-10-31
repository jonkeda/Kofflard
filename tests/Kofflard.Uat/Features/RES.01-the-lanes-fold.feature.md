# Feature: RES.01 The lanes fold

The results fold by lane: Close, Stretch and Leap each under a header with the lane's total. A new
install starts with every lane closed, and a lane opened stays open on the next results
(.my/design/results-list-changes.md).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | RES.01 |
| Tags | maui, results |

@RES.01.1 @maui
## Scenario: RES.01.1 Lanes start closed, and an opened lane stays open

```gherkin
Given the AI answers "Bill Fay" with "bill-fay"
And I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
And the lanes should be "Close 6 · Stretch 4 · Leap 2"
And the "Close" lane should be closed
And the "Stretch" lane should be closed
And the "Leap" lane should be closed
When I open the "Stretch" lane
Then the "Stretch" lane should be open
And the "Close" lane should be closed
When I tap Back
Then I should be on the Search page
When I search for "Bill Fay"
Then I should be on the Results page
And the "Stretch" lane should be open
And the "Close" lane should be closed
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
