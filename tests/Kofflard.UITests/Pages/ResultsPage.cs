using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the results part of the one page: the cards in lanes, Safe, Curious and Wild, or who the
/// user may have meant, with the lane toggles, the count and the trail (.my/design/one-page-desktop.md). On a
/// phone the lanes are groups in one list; on the desktop, columns side by side. Both are read as one
/// <see cref="Suggestions"/> collection.
/// </summary>
public class ResultsPage(IMauiTestContext context) : PageObjectBase<ResultsPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "DiscoverPage";

    /// <summary>"Like Nick Drake".</summary>
    public Label<ResultsPage> Heading => new(this, "Discover_Heading");

    /// <summary>"8 of 12": the cards shown of those found.</summary>
    [UatName("Count")]
    public Label<ResultsPage> Count => new(this, "Discover_Count");

    /// <summary>"‹ Nick Drake": back to the search this one came from.</summary>
    [UatName("Trail")]
    public TextButton<ResultsPage> Trail => new(this, "Discover_Back");

    /// <summary>The Safe, Curious or Wild toggle: in the phone's dock, the column header on the desktop.</summary>
    public Button<ResultsPage> LaneToggle(string lane) => new(this, $"Lane_{lane}");

    /// <summary>Turns a lane on or off.</summary>
    [BddWhen("I toggle {lane}")]
    public ResultsPage Toggle(string lane)
    {
        LaneToggle(lane).Click();
        return this;
    }

    /// <summary>Asserts the lanes shown and their totals, once they are so: a lane turned on arrives as the AI writes.</summary>
    [BddThen("the lanes should become {lanes}")]
    public ResultsPage AssertLanesBecome(string lanes)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(TestConstants.PageTimeoutMs);
        while (LaneHeaders.Summary() != lanes && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Assert.Equal(lanes, LaneHeaders.Summary());
        return this;
    }

    /// <summary>A card's "More →": its search replaces the results, and the trail grows.</summary>
    [BddWhen("I follow {musician}")]
    public ResultsPage Follow(string musician)
    {
        Suggestions.LookAt(musician);
        Suggestions.Card(musician).ExploreButton.Click();
        Heading.AssertText($"Like {musician}", timeoutMs: TestConstants.PageTimeoutMs);
        WaitForSuggestions();
        return this;
    }

    /// <summary>Goes back one search with the trail.</summary>
    [BddWhen("I go back along the trail")]
    public ResultsPage BackAlongTrail()
    {
        var from = Heading.GetText();
        Trail.Click();
        var deadline = DateTime.UtcNow.AddMilliseconds(TestConstants.PageTimeoutMs);
        while (Heading.GetText() == from && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        WaitForSuggestions();
        return this;
    }

    /// <summary>Asserts the trail is gone: the first search of the trail is shown.</summary>
    [BddThen("there should be no trail")]
    public ResultsPage AssertNoTrail()
    {
        Trail.AssertExists(false, "The trail should be gone at the first search.", TestConstants.PageTimeoutMs);
        return this;
    }

    /// <summary>The searched musician in one line.</summary>
    public Label<ResultsPage> Summary => new(this, "Results_Summary");

    /// <summary>The list itself, for what the platform says about it: visible, scrollable, scrolled how far.</summary>
    public Label<ResultsPage> ItemsView => new(this, "Results_Items");

    /// <summary>The lanes' headers: "Safe (6)".</summary>
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

    /// <summary>The lanes, in the order they show.</summary>
    public static readonly IReadOnlyList<string> Lanes = ["Safe", "Curious", "Wild"];

    /// <summary>Asserts each lane and its total, in order: "Safe 6 · Curious 4 · Wild 2".</summary>
    [BddThen("the lanes should be {lanes}")]
    public ResultsPage AssertLanes(string lanes)
    {
        WaitForSuggestions();
        Assert.Equal(lanes, LaneHeaders.Summary());
        return this;
    }

    /// <summary>Waits until the AI has answered and the suggestions are shown.</summary>
    public ResultsPage WaitForSuggestions()
    {
        State.AssertShowing(ResultsState.Content, timeoutMs: TestConstants.PageTimeoutMs);
        return this;
    }

    /// <summary>The results page, once it is on screen.</summary>
    public static ResultsPage Arrived(IMauiTestContext context)
    {
        var page = new ResultsPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
