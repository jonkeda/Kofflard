using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the phone's one page before a search (.my/design/single-page-phone.md): the name in the
/// dock and ➤. Once a search shows, the page is <see cref="ResultsPage"/>, which knows the phone's design:
/// the list, the lane toggles, the count and the trail.
/// </summary>
public class PhonePage(IMauiTestContext context) : PageObjectBase<PhonePage>(context)
{
    /// <inheritdoc />
    public override string Name => "DiscoverPage";

    /// <summary>The musician's name, in the dock.</summary>
    [UatName("Name")]
    public Entry<PhonePage> NameEntry => new(this, "Search_Name");

    /// <summary>➤.</summary>
    [UatName("Go")]
    public Button<PhonePage> GoButton => new(this, "Search_Go");

    /// <summary>Searches for <paramref name="musician"/> from the dock; the list shows it on this page.</summary>
    [BddWhen("I search for {musician} on the phone")]
    public ResultsPage SearchFor(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        var results = new ResultsPage(Context);
        results.PageHeading.AssertText($"Like {musician}", timeoutMs: TestConstants.PageTimeoutMs);
        results.WaitForSuggestions();
        return results;
    }

    /// <summary>Walks the trail back to its start, so a test's searches make the only trail.</summary>
    public PhonePage WalkTrailBack()
    {
        var back = new TextButton<PhonePage>(this, "Discover_Back");
        for (var step = 0; step < 20 && back.IsExists(); step++)
        {
            back.Click();
            back.WaitExists(false, TestConstants.PageTimeoutMs / 10);
        }

        return this;
    }

    /// <summary>The one page, once it is on screen.</summary>
    public static PhonePage Arrived(IMauiTestContext context)
    {
        var page = new PhonePage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
