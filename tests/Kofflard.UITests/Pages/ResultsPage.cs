using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>Page object for the results: the cards in their lanes, or who the user may have meant.</summary>
public class ResultsPage(IMauiTestContext context) : PageObjectBase<ResultsPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "ResultsPage";

    /// <summary>"Like Nick Drake".</summary>
    public Label<ResultsPage> Heading => new(this, "Results_Heading");

    /// <summary>The searched musician in one line.</summary>
    public Label<ResultsPage> Summary => new(this, "Results_Summary");

    /// <summary>"Close 6 · Stretch 4 · Leap 2".</summary>
    public Label<ResultsPage> Lanes => new(this, "Results_Lanes");

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
