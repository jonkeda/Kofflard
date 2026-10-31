# Feature: DISC.01 Find musicians

Type a musician's name and get other musicians: some close to their sound, some a stretch, and a
few leaps into another genre, each with the bridge back (design §3.2).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Search |
| Target | MAUI |
| Journey | DISC.01 |
| Tags | maui, search |

@DISC.01.1 @smoke @maui
## Scenario: DISC.01.1 Typing a name shows twelve suggestions in three lanes

```gherkin
Given I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then I should be on the Results page
And Heading should equal "Like Nick Drake"
And the lanes should be "Safe 6 · Curious 4 · Wild 2"
And Suggestions should suggest "Vashti Bunyan"
And "Vashti Bunyan" should be a "Safe" suggestion
And "Bill Fay" should be a "Curious" suggestion
And "Ali Farka Touré" should be a "Wild" suggestion
```

@DISC.01.2 @maui
## Scenario: DISC.01.2 Searching with no name asks for one

```gherkin
Given I am on the Search page
When I clear Name
And I tap Go
Then Name Error should equal "Type a musician's name"
```

@DISC.01.3 @maui
## Scenario: DISC.01.3 A name that fits several musicians asks which one is meant

```gherkin
Given the AI answers "Genesis" with "genesis-ambiguous"
And the AI answers "Genesis Owusu" with "genesis-owusu"
And I am on the Search page
When I enter "Genesis" into Name
And I tap Go
Then Candidates Title should equal "Which Genesis do you mean?"
When I choose "Genesis Owusu"
Then I should be on the Results page
And Heading should equal "Like Genesis Owusu"
And Suggestions should suggest "Little Simz"
```

@DISC.01.4 @maui
## Scenario: DISC.01.4 A refused key is explained

```gherkin
Given the AI refuses the key
And I am on the Search page
When I enter "Nick Drake" into Name
And I tap Go
Then Message should equal "This key was not accepted by Azure OpenAI / Foundry."
And Fix Connection should be visible
And I should be on the Search page
```
