using Kofflard.TestSupport;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Tests;

/// <summary>
/// Journeys through the real app that the UAT scenarios (tests/Kofflard.Uat/Features) don't cover: the star
/// and More opening nothing, favourites, back from an explored musician, an unknown name, and the ✕ on
/// Recent. What a scenario covers is tested there only.
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
    [Trait("Journey", "DISC.04.2")]
    public Task AnUnknownName_StaysOnTheSearchPage_AndSaysSo()
    {
        _fixture.Ai.Answer("Xyzzy", FakeAi.Canned("unknown"));

        var search = _fixture.Search.SearchAndStay("Xyzzy");
        search.Message.AssertText("No musician called Xyzzy is known. Check the spelling and try again.", timeoutMs: TestConstants.PageTimeoutMs);
        search.AssertLoaded(true);
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.07.1")]
    public Task TheCross_OnARecentSearch_RemovesIt()
    {
        var search = _fixture.Search.SearchFor("Nick Drake").WaitForSuggestions().Back();
        search.RemoveRecentButton("Nick Drake").AssertExists(true, timeoutMs: TestConstants.PageTimeoutMs);

        search.RemoveRecent("Nick Drake");
        search.RemoveRecentButton("Nick Drake").AssertExists(false, "Nick Drake should be gone from Recent.", TestConstants.PageTimeoutMs);
        return Task.CompletedTask;
    }
}
