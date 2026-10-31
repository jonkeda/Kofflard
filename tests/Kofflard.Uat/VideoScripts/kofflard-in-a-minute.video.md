# Video: Kofflard in a minute and a half

| Field | Value |
| --- | --- |
| Voice | azure-speech-service:en-GB-Emily:MAI-Voice-2.1 |
| Beat | 600 |
| Pause | 700 |
| TouchColor | #0F766E |

## Scene: Who do you like?

Scenario: Features/DISC.01-find-musicians.feature.md#DISC.01.1-Typing-a-name-shows-twelve-suggestions-in-three-lanes

Kofflard starts with one question: who do you like?

| Step | Say |
| --- | --- |
| When I enter "Nick Drake" into Name | Say, Nick Drake. |
| And I tap Go | Kofflard asks an AI for musicians you may not know yet. |
| Then I should be on the Results page | Twelve of them, in three lanes. |

Close ones first. Then stretches. Then a few leaps.

## Scene: Three lanes

Scenario: Features/VID.01-the-video.feature.md#VID.01.1-The-lanes-run-from-close-to-a-leap-with-its-bridge

| Step | Say |
| --- | --- |
| When I look at "Vashti Bunyan" | Close: the same quiet folk, well chosen. |
| And I look at "Bill Fay" | A stretch: less known, another year, the same feeling. |
| And I look at "Ali Farka Touré" | And a leap: Ali Farka Touré, from Mali. |
| Then "Ali Farka Touré" should be a "Leap" suggestion | Every leap says why: the same soft, looping fingerpicking. |

Relevance first. Surprise, but never out of the blue.

## Scene: Open it anywhere

Scenario: Features/VID.01-the-video.feature.md#VID.01.2-Opening-a-musician-says-where-it-opened

| Step | Say |
| --- | --- |
| When I open "Vashti Bunyan" | One tap opens YouTube. |
| When I open "Vashti Bunyan" in "Spotify" | Or Spotify, or YouTube Music. |

On a phone, in the app. On Windows, in the browser.

## Scene: Follow the thread

Scenario: Features/VID.01-the-video.feature.md#VID.01.3-More-like-this-follows-a-musician

| Step | Say |
| --- | --- |
| When I look at "Bill Fay" | Like Bill Fay? |
| And I explore "Bill Fay" | Follow him, and the next twelve start from there. |
| When I look at "Talk Talk" | Even a leap to Talk Talk, by way of the same hymn-like piano. |

## Scene: Which Genesis?

Scenario: Features/DISC.01-find-musicians.feature.md#DISC.01.3-A-name-that-fits-several-musicians-asks-which-one-is-meant

| Step | Say |
| --- | --- |
| When I enter "Genesis" into Name | Some names are shared. |
| Then I should be on the Results page | Kofflard asks which one you mean. |
| When I choose "Genesis Owusu" | Genesis Owusu, then. |

## Scene: Make it yours

Scenario: Features/SET.01-settings.feature.md#SET.01.2-Choosing-Spotify-makes-it-every-card-s-main-button

| Step | Say |
| --- | --- |
| When I choose to open with "Spotify" | Spotify more your thing? |
| And "Bert Jansch" should open in "Spotify" first | Make it the main button, everywhere. |

## Scene: Your AI, your answers

Scenario: Features/AI.01-connect-an-ai.feature.md#AI.01.1-Connect-shows-the-Azure-connection-with-its-endpoint-and-deployment

There is no Kofflard server.

| Step | Say |
| --- | --- |
| Then I should be on the Connect page | Bring your own AI. |
| And Provider should have selected "Azure OpenAI / Foundry" | Azure first; or OpenAI, Claude, Gemini, or a sign-in with OpenRouter. |
| And Api Key should be visible | Your key stays on your device. |

Kofflard. Find the music you didn't know you were looking for.
