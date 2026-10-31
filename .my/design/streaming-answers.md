# Change: cards appear as the AI writes them

Date: 2026-10-31. Status: agreed (answers under Decided), not built.

After Find musicians the results page says "Asking the AI…" until the whole answer is in: 12 picks, each with a name, a reason, tags and an album, which takes a small model several seconds and a reasoning model much longer. This change streams the answer: the summary shows as soon as the AI has written it, and each card the moment its pick is complete. The full wait stays the same; the first thing to read comes much sooner.

It changes design §7 (the AI request) and §11's latency note ("Streaming lane by lane is a later option") in [kofflard-design.md](kofflard-design.md), and the results' Loading state. It builds on [adventure-as-outcomes.md](adventure-as-outcomes.md): the results show Safe first, so the Safe picks are the ones worth having first.

## Before and after

```
Before                                   After
0 s   Asking the AI…                     0 s   Asking the AI…
                                         1 s   Like Nick Drake
                                               English folk singer-songwriter
                                         2 s   SAFE
                                               [Vashti Bunyan]
                                         3 s   [Bert Jansch] [Linda Perhacs] …
                                               Curious and Wild fill in below
                                               as they arrive
8 s   all 12 cards at once               8 s   the last card; "More like this…"
```

The times are guesses for a small model; measuring them is part of the work (see Tests).

## How it works

1. **The request streams.** `DiscoveryEngine` calls `GetStreamingResponseAsync` instead of `GetResponseAsync`. Every provider goes through the same OpenAI-compatible client (`ChatClientFactory`), which streams chat completions, with the JSON schema where the provider takes one.
2. **The answer is read as it grows.** A new `DiscoveryAnswerReader` (Core, pure) takes the text so far and reports what has completed since last time:
   - the `status`, as soon as it is written;
   - the `artist` and its summary;
   - each object in `suggestions`, once its closing brace arrives;
   - the `candidates`, at the end.
   It scans the text for complete JSON values rather than parsing the whole, so half a pick is never shown.
3. **The schema's order does the work.** `status` and `artist` come first, then `suggestions`, then `candidates`. So:
   - an ambiguous or unknown name is known from the first few tokens, and the page goes straight to "Which Genesis do you mean?" once the candidates are in;
   - the summary line shows before any card.
   The prompt adds one rule: **write the close picks first, then stretch, then leap.** Safe is the top of the list, so the cards the user sees first arrive first.
4. **Each pick is checked as it arrives**, with the same rules as now (`SuggestionValidator`): a name and a known lane, not the searched musician, not excluded, not a duplicate, and a Wild pick needs a concrete bridge. A pick that fails is skipped silently.
5. **No card ever disappears.** Today the validator trims each lane to its share and tops up a short lane after the whole answer is in. Streaming:
   - a lane takes picks until it reaches its share (4 each); extra picks for that lane are held back;
   - at the end, if the total is under 12, held-back picks fill it, in the order they came, as they would now. They are added, never swapped in.
   So the final 12 are the same as today's, and every card shown stays.
6. **The results page fills in.** `ResultsViewModel` gets the picks one at a time:
   - The lanes are created empty when the status is `ok`, and each card is added to its lane as it comes. The headers count up: "Safe (1)", "Safe (2)" …
   - The page leaves Loading for the content as soon as the first card is ready. A small "More on the way…" line with an activity indicator sits under the list until the answer is done.
   - "More like this…" is hidden until the answer is done, so two requests never run at once.
   - Card order within a lane is arrival order.
7. **The end is as today.** Once the stream is done, the result is validated as a whole, and what was shown is remembered as shown and cached. With "Check musician names" on, the MusicBrainz check then starts, as now, in lane order.

### When streaming can't be used

- **The provider refuses to stream, or refuses streaming with a schema.** The existing 400 handling already drops a refused temperature or schema and asks again. A refusal that names `stream` asks once more without streaming, and that connection is not streamed again until the app restarts.
- **The answer turns out unusable** (it isn't JSON, or breaks off): the cards shown so far stay; the engine asks once more without streaming, as it retries now, and adds the picks it didn't have yet. If that fails too, the error shows under the cards rather than replacing them.
- **The user leaves the page**: the stream is cancelled, as the request is now. Nothing is cached or marked shown for a half answer.
- **A cached result** (a recent search opened again) shows at once, as now; there is nothing to stream.

## Code

### Core
- `DiscoveryAnswerReader`: incremental reading of the answer text. Pure, unit-tested on answers cut at every character.
- `SuggestionValidator`:
  - `Check(pick, artist, exclude, seen)`, the per-pick rules, split out of `Validate`;
  - `LaneFill`, which takes picks up to each lane's share and holds back the rest; `Validate` uses it, so the final result is computed the same way whether streamed or not.
- `IDiscoveryEngine.DiscoverAsync` returns `IAsyncEnumerable<DiscoveryUpdate>`, where an update is one of: `Status`, `Artist`, `Pick`, `Done(DiscoveryResult)`. The non-streaming path yields the same updates, all at the end, so the view model has one way in.
- `DiscoveryService` passes the updates through; caching and "mark shown" happen on `Done`. A cached result is yielded as one `Done`.
- `ResultsViewModel`: builds lanes and cards from the updates; `IsStreaming` for the "More on the way…" line and to hide "More like this…".
- `DiscoveryPrompt.System`: the rule "write close picks first, then stretch, then leap".

### Infrastructure
- `DiscoveryEngine`: `GetStreamingResponseAsync`, feeding the text to the reader; the fallbacks above.

### App
- `ResultsPage.xaml`: the "More on the way…" row under the list (`Results_Streaming`), bound to `IsStreaming`.

## Tests

| Suite | Change |
| --- | --- |
| Unit | `DiscoveryAnswerReader`: every canned answer, fed one character at a time and in random chunks, gives the same picks in the same order as parsing it whole; nothing is reported from a half-written pick. `LaneFill`: a lane stops at its share; held-back picks top up a short total; a shown pick is never dropped. `ResultsViewModel`: cards arrive one by one into their lanes; leaves Loading at the first card; "More like this…" hidden while streaming; a failed retry keeps the cards and shows the error. |
| Integration | `FakeAi` answers as a stream (server-sent events, the canned answer cut into chunks, with an optional delay between chunks). `DiscoveryEngineTests`: a streamed answer gives the same result as today; a provider that refuses `stream` gets asked without it. |
| UI / UAT | A scenario with a slow stream: the first Safe card shows while the AI is still answering ("More on the way…" visible), then the rest. Existing scenarios keep passing unchanged, since they wait for the content state and for cards by name. |
| Timing | A live check (`LiveAiTests`, not in CI) that logs time to first card and to the last, for the connected model, so the doc's guesses can be replaced by numbers. |

## Decided (2026-10-31)

1. **Each card shows as soon as its pick is complete**, without waiting for its lane.
2. **The prompt asks for close picks first**, then stretch, then leap.
3. **The MusicBrainz name check stays for now**, as it is: off by default, and when on it starts once the answer is complete. Removing it was considered and set aside, because it is also where a card's Spotify artist link comes from (see [Later](#later)).
4. **A "More on the way…" line with a spinner** under the list while the AI is still answering; no placeholder cards.

## Not in this change
- The prompt's content, the lanes and the mix stay as they are.
- Choosing a faster model: that's in Settings already, and a separate question.

## Later

- **Look names up only when opening in Spotify.** Instead of checking every card up front, look the name up on MusicBrainz when the user opens a card in Spotify, for the artist's Spotify link; without one, Spotify opens a search as now. That would let "Check musician names", the ✓ and the dimming go, and keep the direct Spotify link. Spotify's own API was weighed and left out: it needs a registered app and either a secret (which can't ship without a server) or a Spotify sign-in for every user.
