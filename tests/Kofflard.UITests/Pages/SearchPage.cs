using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the search part of the one page: the name, ➤ or Find musicians, Recent
/// (.my/design/one-page-desktop.md). The same ids in the phone's dock and the desktop's search pane. The
/// results on the same page are <see cref="ResultsPage"/>.
/// </summary>
/// <remarks>Favourites (★) and Settings (⚙) are toolbar items, so they are declared on <see cref="AppRoot"/>.</remarks>
public class SearchPage(IMauiTestContext context) : PageObjectBase<SearchPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "DiscoverPage";

    /// <summary>The musician's name.</summary>
    [UatName("Name")]
    public Entry<SearchPage> NameEntry => new(this, "Search_Name");

    /// <summary>"Type a musician's name", after Go with nothing typed.</summary>
    [UatName("Name Error")]
    public Label<SearchPage> NameError => new(this, "Search_NameError");

    /// <summary>Searches: ➤ on a phone, Find musicians on the desktop.</summary>
    [UatName("Go")]
    public Button<SearchPage> GoButton => new(this, "Search_Go");

    /// <summary>Opens the favourites: the star.</summary>
    [UatName("Favourites")]
    [UatNavigatesTo(typeof(SavedPage))]
    public ToolbarButton<AppRoot> SavedButton => new(_appRoot, Locator.ByAccessibilityId("Search_Saved"));

    /// <summary>Opens the settings.</summary>
    [UatNavigatesTo(typeof(SettingsPage))]
    public ToolbarButton<AppRoot> SettingsButton => new(_appRoot, Locator.ByAccessibilityId("Search_Settings"));

    /// <summary>The ✕ on a recent search's chip.</summary>
    public Button<SearchPage> RemoveRecentButton(string artist) => new(this, $"RecentRow_Remove_{artist}");

    /// <summary>The trail's "‹ Nick Drake".</summary>
    public TextButton<SearchPage> TrailButton => new(this, "Discover_Back");

    /// <summary>Searches for <paramref name="musician"/>; the results show on the same page.</summary>
    [BddWhen("I search for {musician}")]
    public ResultsPage SearchFor(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        var results = new ResultsPage(Context);
        results.Heading.AssertText($"Like {musician}", timeoutMs: TestConstants.PageTimeoutMs);
        return results;
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

    /// <summary>Walks the trail back to its start, so a test's searches make the only trail.</summary>
    public SearchPage WalkTrailBack()
    {
        for (var step = 0; step < 20 && TrailButton.IsExists(); step++)
        {
            TrailButton.Click();
            TrailButton.WaitExists(false, TestConstants.PageTimeoutMs / 10);
        }

        return this;
    }

    /// <summary>The one page, once it is on screen.</summary>
    public static SearchPage Arrived(IMauiTestContext context)
    {
        var page = new SearchPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
