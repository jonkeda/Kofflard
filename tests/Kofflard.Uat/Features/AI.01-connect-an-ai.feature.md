# Feature: AI.01 Connect an AI

There is no Kofflard server: the user connects an AI of their own. Azure is listed first (design §6).
The tests start connected to Azure (the fake AI answers on its path), so no step opens the provider
dropdown: on Windows, Bruinet left that WinUI dropdown open (2026-10-31).

## Metadata

| Field | Value |
| --- | --- |
| App | Kofflard.App |
| Area | Connect |
| Target | MAUI |
| Journey | AI.01 |
| Tags | maui, connect |

@AI.01.1 @maui @video
## Scenario: AI.01.1 Connect shows the Azure connection, with its endpoint and deployment

```gherkin
Given I am on the Search page
When I tap Settings
Then I should be on the Settings page
When I tap Change
Then I should be on the Connect page
And Provider should have selected "Azure OpenAI / Foundry"
And Endpoint should be visible
And Model should be visible
And Api Key should be visible
```
