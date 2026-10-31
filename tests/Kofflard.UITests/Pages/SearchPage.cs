using Bruinet.Uat;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the search page, the Shell root.
/// </summary>
/// <remarks>Favourites (★) and Settings (⚙) are toolbar items, so they are declared on <see cref="AppRoot"/>.</remarks>
public class SearchPage(IMauiTestContext context) : PageObjectBase<SearchPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "SearchPage";

    /// <summary>The musician's name.</summary>
    [UatName("Name")]
    public Entry<SearchPage> NameEntry => new(this, "Search_Name");

    /// <summary>"Type a musician's name", after Go with nothing typed.</summary>
    [UatName("Name Error")]
    public Label<SearchPage> NameError => new(this, "Search_NameError");

    /// <summary>Searches.</summary>
    /// <remarks>Not marked as navigating: with no name typed it stays here and says so.</remarks>
    [UatName("Go")]
    public Button<SearchPage> GoButton => new(this, "Search_Go");

    /// <summary>"No musicians found like …" or an error: what came of a search that opened no page.</summary>
    [UatName("Message")]
    public Label<SearchPage> Message => new(this, "Search_Message");

    /// <summary>Offered under the message when the connection is the cause.</summary>
    [UatName("Fix Connection")]
    [UatNavigatesTo(typeof(ConnectPage))]
    public Button<SearchPage> FixConnection => new(this, "Search_FixConnection");

    /// <summary>"Which Genesis do you mean?".</summary>
    [UatName("Candidates Title")]
    public Label<SearchPage> CandidatesTitle => new(this, "Search_CandidatesTitle");

    /// <summary>One of the "Did you mean" candidates.</summary>
    public Button<SearchPage> Candidate(string name) => new(this, $"Candidate_{name}");

    /// <summary>The ✕ on a recent search's chip.</summary>
    public Button<SearchPage> RemoveRecentButton(string artist) => new(this, $"RecentRow_Remove_{artist}");

    /// <summary>Opens the favourites: the star.</summary>
    [UatName("Favourites")]
    [UatNavigatesTo(typeof(SavedPage))]
    public ToolbarButton<AppRoot> SavedButton => new(_appRoot, Locator.ByAccessibilityId("Search_Saved"));

    /// <summary>Opens the settings.</summary>
    [UatNavigatesTo(typeof(SettingsPage))]
    public ToolbarButton<AppRoot> SettingsButton => new(_appRoot, Locator.ByAccessibilityId("Search_Settings"));

    /// <summary>Searches for <paramref name="musician"/>.</summary>
    [BddWhen("I search for {musician}")]
    public ResultsPage SearchFor(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        return ResultsPage.Arrived(Context);
    }

    /// <summary>Searches for <paramref name="musician"/> where no results page opens: nobody found, an error, or "Did you mean".</summary>
    [BddWhen("I search for {musician} and stay")]
    public SearchPage SearchAndStay(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        return this;
    }

    /// <summary>Picks a "Did you mean" candidate, which searches it.</summary>
    [BddWhen("I choose {name}")]
    public ResultsPage ChooseCandidate(string name)
    {
        Candidate(name).Click();
        return ResultsPage.Arrived(Context);
    }

    /// <summary>Forgets a recent search with the ✕ on its chip.</summary>
    [BddWhen("I remove {artist} from recent")]
    public SearchPage RemoveRecent(string artist)
    {
        RemoveRecentButton(artist).Click();
        return this;
    }

    /// <summary>Opens the settings.</summary>
    public SettingsPage OpenSettings()
    {
        SettingsButton.Click();
        return SettingsPage.Arrived(Context);
    }

    /// <summary>Opens the favourites.</summary>
    public SavedPage OpenSaved()
    {
        SavedButton.Click();
        return SavedPage.Arrived(Context);
    }

    /// <summary>The search page, once it is on screen.</summary>
    public static SearchPage Arrived(IMauiTestContext context)
    {
        var page = new SearchPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
