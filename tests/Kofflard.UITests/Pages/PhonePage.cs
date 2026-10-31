using Bruinet.Uat;
using Kofflard.UITests.Containers;

namespace Kofflard.UITests.Pages;

/// <summary>
/// Page object for the phone's one page (.my/design/single-page-phone.md): the dock with the lane toggles
/// and the name, the trail back. The list is <see cref="PhoneResults"/>, the results page object rooted here.
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

    /// <summary>"‹ Nick Drake": back to the search this one came from.</summary>
    [UatName("Back")]
    public TextButton<PhonePage> BackButton => new(this, "Discover_Back");

    /// <summary>"8 of 12": the cards shown of those found.</summary>
    [UatName("Count")]
    public Label<PhonePage> Count => new(this, "Discover_Count");

    /// <summary>The Safe, Curious or Wild toggle.</summary>
    public Button<PhonePage> LaneToggle(string lane) => new(this, $"Lane_{lane}");

    /// <summary>The list: the results page object, rooted at this page.</summary>
    public PhoneResults Results => new(Context);

    /// <summary>Searches for <paramref name="musician"/> from the dock; the list shows it on this page.</summary>
    [BddWhen("I search for {musician} on the phone")]
    public PhoneResults SearchFor(string musician)
    {
        NameEntry.SetText(musician);
        GoButton.Click();
        return (PhoneResults)Results.WaitForSuggestions();
    }

    /// <summary>Turns a lane on or off.</summary>
    [BddWhen("I toggle {lane}")]
    public PhonePage Toggle(string lane)
    {
        LaneToggle(lane).Click();
        return this;
    }

    /// <summary>Goes back one search with the trail.</summary>
    public PhoneResults Back()
    {
        BackButton.Click();
        return (PhoneResults)Results.WaitForSuggestions();
    }

    /// <summary>The one page, once it is on screen.</summary>
    public static PhonePage Arrived(IMauiTestContext context)
    {
        var page = new PhonePage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}

/// <summary>The results as the phone's one page shows them: the same list and ids, under its own heading.</summary>
public sealed class PhoneResults(IMauiTestContext context) : ResultsPage(context)
{
    /// <inheritdoc />
    public override string Name => "DiscoverPage";

    /// <summary>"Like Nick Drake".</summary>
    public override Label<ResultsPage> PageHeading => new(this, "Discover_Heading");
}
