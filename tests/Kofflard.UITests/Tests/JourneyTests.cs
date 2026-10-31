using Kofflard.TestSupport;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Tests;

/// <summary>
/// The main journeys through the real app (design §10): search, lanes that fold, opening links with a tap
/// or a long press, settings, favourites, exploring, and asking which musician was meant.
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
        results.AssertLanes("Safe 6 · Curious 4 · Wild 2");
        results.Suggestions
            .AssertLane("Vashti Bunyan", "Safe")
            .AssertLane("Bill Fay", "Curious")
            .AssertLane("Ali Farka Touré", "Wild");
        results.Suggestions.Card("Ali Farka Touré").Why.AssertTextStartsWith("Bridge: ");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "RES.01.1")]
    public Task EveryLaneShows_SafeFirst()
    {
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        Assert.Equal(["Safe", "Curious", "Wild"], results.LaneHeaders.Totals().Select(lane => lane.Name));
        Assert.Equal("Vashti Bunyan", results.Suggestions.Names()[0]);
        results.Suggestions.Shown("Ali Farka Touré");
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
    public Task Open_ATapUsesYouTubeByDefault_AndALongPressOffersTheOthers()
    {
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        results.Suggestions.Open("Vashti Bunyan");
        Assert.Equal(
            "music YouTube https://www.youtube.com/results?search_query=Vashti%20Bunyan%20Just%20Another%20Diamond%20Day",
            _fixture.WaitForOpenedLink("music YouTube"));

        results.Suggestions.ShowOtherServices("Vashti Bunyan");
        var card = results.Suggestions.Card("Vashti Bunyan");
        card.SecondButton.AssertText("YouTube Music");
        card.ThirdButton.AssertText("Spotify");
        card.ThirdButton.Click();
        Assert.Equal("music Spotify https://open.spotify.com/search/Vashti%20Bunyan", _fixture.WaitForOpenedLink("music Spotify"));
        card.SecondButton.AssertExists(false, timeoutMs: TestConstants.PageTimeoutMs);
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "RES.01.2")]
    public Task TheStarAndMore_OnACard_OpenNothing()
    {
        _fixture.Ai.Answer("Vashti Bunyan", FakeAi.Canned("bill-fay"));
        var results = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions();

        results.Suggestions.Save("Vashti Bunyan");
        results.Suggestions.Explore("Vashti Bunyan");

        Assert.Empty(_fixture.OpenedLinks);
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "SET.01.1")]
    public Task OpenWith_Spotify_MakesSpotifyWhatATapOpens()
    {
        var settings = _fixture.Search.OpenSettings();
        settings.OpenWith.AssertSelectedText("YouTube");
        settings.CheckNames.AssertOff();
        settings.ChooseOpenWith("Spotify");

        var results = settings.Back().SearchFor("Nick Drake").WaitForSuggestions();
        results.Suggestions.AssertOpensFirstIn("Bert Jansch", "Spotify");
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
    public Task TheStar_KeepsTheMusician_InFavourites()
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
        results.Connection.AssertTextStartsWith("AI: Azure OpenAI / Foundry");
        return Task.CompletedTask;
    }
}
