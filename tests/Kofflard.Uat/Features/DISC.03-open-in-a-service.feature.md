# Feature: DISC.03 Open a musician in YouTube, YouTube Music or Spotify

A tap on a card opens the service chosen in Open with (YouTube unless changed); a long press (a
right-click on Windows) offers the other two. On Windows the browser opens; on Android and iOS the service's app when it
is installed (design §8). Under test, the app writes the link instead of opening it.

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Results |
| Target | MAUI |
| Journey | DISC.03 |
| Tags | maui, links |

@DISC.03.1 @smoke @maui @windows
## Scenario: DISC.03.1 A tap opens YouTube, with what the musician is known for

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
And "Vashti Bunyan" should open in "YouTube" first
When I open "Vashti Bunyan"
Then the app should have opened "https://www.youtube.com/results?search_query=Vashti%20Bunyan%20Just%20Another%20Diamond%20Day"
```

@DISC.03.2 @maui @windows
## Scenario: DISC.03.2 Spotify opens from a long press, as a search

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
When I open "Bert Jansch" in "Spotify"
Then the app should have opened "https://open.spotify.com/search/Bert%20Jansch"
```

@DISC.03.3 @maui @windows
## Scenario: DISC.03.3 YouTube Music opens from a long press

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
When I open "Linda Perhacs" in "YouTube Music"
Then the app should have opened "https://music.youtube.com/search?q=Linda%20Perhacs"
```
