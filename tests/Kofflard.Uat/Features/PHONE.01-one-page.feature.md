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
## Scenario: PHONE.01.1 A search shows on the same page, asking only for the lanes that are on

A new install has Safe and Curious on, so nothing wild is asked for.

```gherkin
Given I am on the Phone page
When I search for "Nick Drake" on the phone
Then I should be on the Results page
And Page Heading should equal "Like Nick Drake"
And the lanes should become "Safe 6 · Curious 4"
And Count should equal "10 of 10"
```

@PHONE.01.2 @phone @maui
## Scenario: PHONE.01.2 Turning Wild on asks for the wild picks alone

```gherkin
Given I am on the Phone page
When I search for "Nick Drake" on the phone
And I toggle "Wild"
Then the lanes should become "Safe 6 · Curious 4 · Wild 2"
And Count should equal "12 of 12"
And "Ali Farka Touré" should be a "Wild" suggestion
```

@PHONE.01.3 @phone @maui
## Scenario: PHONE.01.3 Turning Safe off hides its section

```gherkin
Given I am on the Phone page
When I search for "Nick Drake" on the phone
And I toggle "Safe"
Then the lanes should become "Curious 4"
And Count should equal "4 of 10"
```

@PHONE.01.4 @phone @maui
## Scenario: PHONE.01.4 More follows a musician on the same page, and the trail goes back

```gherkin
Given the AI answers "Bill Fay" with "bill-fay"
And I am on the Phone page
When I search for "Nick Drake" on the phone
And I follow "Bill Fay"
Then Page Heading should equal "Like Bill Fay"
And Trail should equal "‹ Nick Drake"
And Suggestions should suggest "Judee Sill"
When I go back along the trail
Then Page Heading should equal "Like Nick Drake"
And there should be no trail
And Suggestions should suggest "Vashti Bunyan"
```
