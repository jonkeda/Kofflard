# Feature: PHONE.01 One page on a phone

On a phone, search and results are one page: the list fills the screen and the search sits in a dock at
the bottom. The Safe, Curious and Wild toggles in the dock set both what a search asks for and which
sections show. A card's "More →" replaces the list, and a trail goes back (.my/design/single-page-phone.md).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Phone |
| Target | MAUI |
| Journey | PHONE.01 |
| Tags | maui, phone |

@PHONE.01.1 @phone @maui
## Scenario: PHONE.01.1 A search shows on the same page, every lane on

A new install has every lane on, Wild too.

```gherkin
Given I am on the Search page
When I search for "Nick Drake"
Then I should be on the Results page
And Heading should equal "Like Nick Drake"
And the lanes should become "Safe 6 · Curious 4 · Wild 2"
And Count should equal "12 of 12"
```

@PHONE.01.2 @phone @maui
## Scenario: PHONE.01.2 With Wild off a search asks without it, and turning it on asks for the wild picks alone

```gherkin
Given the AI answers "De Dijk" with "de-dijk"
And I am on the Search page
When I search for "Nick Drake"
And I toggle "Wild"
And I search for "De Dijk"
Then the lanes should become "Safe 6 · Curious 4"
And Count should equal "10 of 10"
When I toggle "Wild"
Then the lanes should become "Safe 6 · Curious 4 · Wild 1"
And "Solomon Burke" should be a "Wild" suggestion
```

@PHONE.01.3 @phone @maui
## Scenario: PHONE.01.3 Turning Safe off hides its section

```gherkin
Given I am on the Search page
When I search for "Nick Drake"
And I toggle "Safe"
Then the lanes should become "Curious 4 · Wild 2"
And Count should equal "6 of 12"
```

@PHONE.01.4 @phone @maui
## Scenario: PHONE.01.4 More follows a musician on the same page, and the trail goes back

```gherkin
Given the AI answers "Bill Fay" with "bill-fay"
And I am on the Search page
When I search for "Nick Drake"
And I follow "Bill Fay"
Then Heading should equal "Like Bill Fay"
And Trail should equal "‹ Nick Drake"
And Suggestions should suggest "Judee Sill"
When I go back along the trail
Then Heading should equal "Like Nick Drake"
And there should be no trail
And Suggestions should suggest "Vashti Bunyan"
```
