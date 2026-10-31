using Kofflard.TestSupport;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Tests;

/// <summary>
/// The main journeys through the real app (design §10): search, lanes, opening links, settings, saving,
/// exploring, and asking which musician was meant.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
public sealed class JourneyTests
{
    private readonly KofflardAppFixture _fixture;

    public JourneyTests(KofflardAppFixture fixture)
    {
        _fixture = fixture;
        fixture.StartTest();
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.01.1")]
    public Task Search_ShowsTwelveSuggestions_InThreeLanes()
    {
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        results.Heading.AssertText("Like Nick Drake");
        results.Lanes.AssertText("Close 6 · Stretch 4 · Leap 2");
        results.Suggestions
            .AssertLane("Vashti Bunyan", "Close")
            .AssertLane("Bill Fay", "Stretch")
            .AssertLane("Ali Farka Touré", "Leap");
        results.Suggestions.Card("Ali Farka Touré").Why.AssertTextStartsWith("Bridge: ");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.01.2")]
    public Task Search_WithNoName_SaysSo()
    {
        var search = _fixture.Search;
        search.NameEntry.Clear();
        search.GoButton.Click();

        search.NameError.AssertText("Type a musician's name");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.03.1")]
    public Task Open_UsesYouTubeByDefault_AndTheOtherServicesFromTheirButtons()
    {
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        results.Suggestions.Card("Vashti Bunyan").OpenButton.AssertText("▶ YouTube");
        results.Suggestions.Open("Vashti Bunyan");
        Assert.Equal(
            "music YouTube https://www.youtube.com/results?search_query=Vashti%20Bunyan%20Just%20Another%20Diamond%20Day",
            _fixture.WaitForOpenedLink("music YouTube"));

        results.Suggestions.OpenIn("Vashti Bunyan", "Spotify");
        Assert.Equal("music Spotify https://open.spotify.com/search/Vashti%20Bunyan", _fixture.WaitForOpenedLink("music Spotify"));
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "SET.01.1")]
    public Task OpenWith_Spotify_MakesSpotifyTheMainButton()
    {
        var settings = _fixture.Search.OpenSettings();
        settings.OpenWith.AssertSelectedText("YouTube");
        settings.CheckNames.AssertOff();
        settings.ChooseOpenWith("Spotify");

        var results = settings.Back().SearchFor("Nick Drake").WaitForSuggestions();
        results.Suggestions.Card("Bert Jansch").OpenButton.AssertText("▶ Spotify");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.04.1")]
    public Task AnAmbiguousName_AsksWhichOne_ThenSearchesThePick()
    {
        _fixture.Ai
            .Answer("Genesis", FakeAi.Canned("genesis-ambiguous"))
            .Answer("Genesis Owusu", FakeAi.Canned("genesis-owusu"));

        var results = _fixture.Search.SearchFor("Genesis");
        results.State.AssertShowing(ResultsState.Candidates, timeoutMs: TestConstants.PageTimeoutMs);
        results.CandidatesTitle.AssertText("Which Genesis do you mean?");

        results.Candidates.Choose("Genesis Owusu").WaitForSuggestions();
        results.Heading.AssertText("Like Genesis Owusu");
        results.Suggestions.AssertCard("Little Simz");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.05.1")]
    public Task Saving_KeepsTheMusician_OnTheSavedPage()
    {
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();
        results.Suggestions.Save("Bill Fay");

        results.Back().OpenSaved().Items.AssertSaved("Bill Fay");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.06.1")]
    public Task BackFromAnExploredMusician_KeepsTheEarlierSuggestions()
    {
        _fixture.Ai.Answer("Bill Fay", FakeAi.Canned("bill-fay"));
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        results.Suggestions.Explore("Bill Fay").Suggestions.AssertCard("Judee Sill");
        results.BackButton.Click();

        var earlier = new Pages.ResultsPage(_fixture.Context);
        earlier.Heading.AssertText("Like Nick Drake", timeoutMs: TestConstants.PageTimeoutMs);
        earlier.WaitForSuggestions().Suggestions.AssertCard("Vashti Bunyan");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.07.1")]
    public Task ARefusedKey_IsExplained()
    {
        _fixture.Ai.FailWith(401);

        var results = _fixture.Search.SearchFor("Nick Drake");
        results.State.AssertShowing(ResultsState.Error, timeoutMs: TestConstants.PageTimeoutMs);
        results.ErrorMessage.AssertText("This key was not accepted by Azure OpenAI / Foundry.");
        return Task.CompletedTask;
    }
}
