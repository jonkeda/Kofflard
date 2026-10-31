# Feature: VID.01 What the video shows

Scenarios the narrated video records (`VideoScripts/kofflard-in-a-minute.video.md`) that no other feature
covers: walking the lanes, the confirmation after an open, and following a musician. Each is a test too.

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | VID.01 |
| Tags | maui, video |

@VID.01.1 @maui @video
## Scenario: VID.01.1 The lanes run from safe to wild with a bridge

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
When I look at "Vashti Bunyan"
And I look at "Bill Fay"
And I look at "Ali Farka Touré"
Then "Ali Farka Touré" should be a "Wild" suggestion
```

@VID.01.2 @maui @video
## Scenario: VID.01.2 Opening a musician says where it opened

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
When I open "Vashti Bunyan"
Then "Vashti Bunyan" should say "Opened in YouTube ✓"
When I open "Vashti Bunyan" in "Spotify"
Then "Vashti Bunyan" should say "Opened in Spotify ✓"
```

@VID.01.3 @maui @video
## Scenario: VID.01.3 More like this follows a musician

```gherkin
Given the AI answers "Bill Fay" with "bill-fay"
And I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
When I look at "Bill Fay"
And I explore "Bill Fay"
Then Heading should equal "Like Bill Fay"
And Suggestions should suggest "Judee Sill"
When I look at "Talk Talk"
Then "Talk Talk" should be a "Wild" suggestion
```
