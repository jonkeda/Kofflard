using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>Page object for the results: the cards in lanes that fold, or who the user may have meant.</summary>
public class ResultsPage(IMauiTestContext context) : PageObjectBase<ResultsPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "ResultsPage";

    /// <summary>"Like Nick Drake": the title bar's text, the page has no heading of its own.</summary>
    public Label<AppRoot> Heading => new(_appRoot, "title");

    /// <summary>The searched musician in one line.</summary>
    public Label<ResultsPage> Summary => new(this, "Results_Summary");

    /// <summary>The list itself, for what the platform says about it: visible, scrollable, scrolled how far.</summary>
    public Label<ResultsPage> ItemsView => new(this, "Results_Items");

    /// <summary>The lanes' headers: "▸ Close (6)".</summary>
    public LaneHeaderList LaneHeaders => new(this, "Results_Items");

    /// <summary>Loading, Content, Candidates, Empty or Error.</summary>
    [UatName("Results")]
    public ResultsState State => new(this, "Results_State");

    /// <summary>The cards.</summary>
    public SuggestionList Suggestions => new(this, "Results_Items");

    /// <summary>Who the user may have meant.</summary>
    public CandidateList Candidates => new(this, "Results_CandidateList");

    /// <summary>"Which Genesis do you mean?".</summary>
    [UatName("Candidates Title")]
    public Label<ResultsPage> CandidatesTitle => new(this, "Results_CandidatesTitle");

    /// <summary>What went wrong.</summary>
    [UatName("Error Message")]
    public Label<ResultsPage> ErrorMessage => new(this, "Results_ErrorLabel");

    /// <summary>"AI: Azure OpenAI / Foundry · kofflard-test", under an error.</summary>
    [UatName("Connection")]
    public Label<ResultsPage> Connection => new(this, "Results_Connection");

    /// <summary>Asks for more musicians like this one.</summary>
    [UatName("More")]
    public Button<ResultsPage> MoreButton => new(this, "Results_More");

    /// <summary>
    /// Shell's back arrow: WinUI's <c>NavigationViewBackButton</c> on Windows, "Navigate up" on Android
    /// (as Todo's detail page).
    /// </summary>
    [UatNavigatesTo(typeof(SearchPage))]
    public Button<AppRoot> BackButton => Context.Platform == Bruinet.Maui.Enums.MauiPlatform.Android
        ? new(_appRoot, Locator.ByAccessibilityId("Navigate up"))
        : new(_appRoot, "NavigationViewBackButton");

    /// <summary>Asserts each lane and its total, in order: "Close 6 · Stretch 4 · Leap 2".</summary>
    [BddThen("the lanes should be {lanes}")]
    public ResultsPage AssertLanes(string lanes)
    {
        WaitForSuggestions();
        Assert.Equal(lanes, LaneHeaders.Summary());
        return this;
    }

    /// <summary>Opens <paramref name="lane"/>: Close, Stretch or Leap.</summary>
    [BddWhen("I open the {lane} lane")]
    public ResultsPage OpenLane(string lane)
    {
        WaitForSuggestions().LaneHeaders.Open(lane);
        return this;
    }

    /// <summary>Closes <paramref name="lane"/>.</summary>
    [BddWhen("I close the {lane} lane")]
    public ResultsPage CloseLane(string lane)
    {
        WaitForSuggestions().LaneHeaders.Close(lane);
        return this;
    }

    /// <summary>Asserts <paramref name="lane"/> is open.</summary>
    [BddThen("the {lane} lane should be open")]
    public ResultsPage AssertLaneOpen(string lane)
    {
        WaitForSuggestions().LaneHeaders.AssertOpen(lane);
        return this;
    }

    /// <summary>Asserts <paramref name="lane"/> is closed.</summary>
    [BddThen("the {lane} lane should be closed")]
    public ResultsPage AssertLaneClosed(string lane)
    {
        WaitForSuggestions().LaneHeaders.AssertClosed(lane);
        return this;
    }

    /// <summary>Waits until the AI has answered and the suggestions are shown.</summary>
    public ResultsPage WaitForSuggestions()
    {
        State.AssertShowing(ResultsState.Content, timeoutMs: TestConstants.PageTimeoutMs);
        return this;
    }

    /// <summary>Returns to the search page.</summary>
    public SearchPage Back()
    {
        BackButton.Click();
        return SearchPage.Arrived(Context);
    }

    /// <summary>The results page, once it is on screen.</summary>
    public static ResultsPage Arrived(IMauiTestContext context)
    {
        var page = new ResultsPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
