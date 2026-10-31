using Bruinet.Uat;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the search page, the Shell root.
/// </summary>
/// <remarks>Saved and Settings are toolbar items, so they are declared on <see cref="AppRoot"/>.</remarks>
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

    /// <summary>The adventurousness, 1-5.</summary>
    public Stepper<SearchPage> Adventure => new(this, "Search_Adventure");

    /// <summary>"Safe" to "Wild".</summary>
    [UatName("Adventure Name")]
    public Label<SearchPage> AdventureName => new(this, "Search_AdventureName");

    /// <summary>Searches.</summary>
    /// <remarks>Not marked as navigating: with no name typed it stays here and says so.</remarks>
    [UatName("Go")]
    public Button<SearchPage> GoButton => new(this, "Search_Go");

    /// <summary>"AI: Local (Ollama / LM Studio) · kofflard-test".</summary>
    public Label<SearchPage> Connection => new(this, "Search_Connection");

    /// <summary>Opens the saved musicians.</summary>
    [UatNavigatesTo(typeof(SavedPage))]
    public ToolbarButton<AppRoot> SavedButton => new(_appRoot, Locator.ByAccessibilityId("Search_Saved"));

    /// <summary>Opens the settings.</summary>
    [UatNavigatesTo(typeof(SettingsPage))]
    public ToolbarButton<AppRoot> SettingsButton => new(_appRoot, Locator.ByAccessibilityId("Search_Settings"));

    /// <summary>Searches for <paramref name="musician"/> at the current level.</summary>
    [BddWhen("I search for {musician}")]
    public ResultsPage SearchFor(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        return ResultsPage.Arrived(Context);
    }

    /// <summary>Opens the settings.</summary>
    public SettingsPage OpenSettings()
    {
        SettingsButton.Click();
        return SettingsPage.Arrived(Context);
    }

    /// <summary>Opens the saved musicians.</summary>
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
