using Bruinet.Core.Scopes;
using Bruinet.Uat;

namespace Kofflard.UITests.Pages;

/// <summary>Page object for the settings (design §3.3).</summary>
public class SettingsPage(IMauiTestContext context) : PageObjectBase<SettingsPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "SettingsPage";

    /// <summary>The AI connection in one line.</summary>
    public Label<SettingsPage> Connection => new(this, "Settings_Connection");

    /// <summary>YouTube, YouTube Music or Spotify.</summary>
    [UatName("Open With")]
    public Picker<SettingsPage> OpenWith => new(this, "Settings_OpenWith");

    /// <summary>Whether names are checked against MusicBrainz.</summary>
    [UatName("Check Names")]
    public Switch<SettingsPage> CheckNames => new(this, "Settings_CheckNames");

    /// <summary>The adventurousness, 1-5.</summary>
    public Stepper<SettingsPage> Adventure => new(this, "Settings_Adventure");

    /// <summary>Shell's back arrow.</summary>
    [UatNavigatesTo(typeof(SearchPage))]
    public Button<AppRoot> BackButton => Context.Platform == Bruinet.Maui.Enums.MauiPlatform.Android
        ? new(_appRoot, Locator.ByAccessibilityId("Navigate up"))
        : new(_appRoot, "NavigationViewBackButton");

    /// <summary>Chooses the service a card's main button opens.</summary>
    [BddWhen("I choose to open with {service}")]
    public SettingsPage ChooseOpenWith(string service)
    {
        OpenWith.SelectByText(service);
        OpenWith.AssertSelectedText(service);
        return this;
    }

    /// <summary>Returns to the search page.</summary>
    public SearchPage Back()
    {
        BackButton.Click();
        return SearchPage.Arrived(Context);
    }

    /// <summary>The settings, once on screen.</summary>
    public static SettingsPage Arrived(IMauiTestContext context)
    {
        var page = new SettingsPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}

/// <summary>Page object for the saved musicians.</summary>
public class SavedPage(IMauiTestContext context) : PageObjectBase<SavedPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "SavedPage";

    /// <summary>"Nothing saved yet…".</summary>
    [UatName("Empty Message")]
    public Label<SavedPage> EmptyMessage => new(this, "Saved_Empty");

    /// <summary>The saved musicians.</summary>
    [UatName("Saved")]
    public SavedList Items => new(this, "Saved_Items");

    /// <summary>Shell's back arrow.</summary>
    [UatNavigatesTo(typeof(SearchPage))]
    public Button<AppRoot> BackButton => Context.Platform == Bruinet.Maui.Enums.MauiPlatform.Android
        ? new(_appRoot, Locator.ByAccessibilityId("Navigate up"))
        : new(_appRoot, "NavigationViewBackButton");

    /// <summary>The saved page, once on screen.</summary>
    public static SavedPage Arrived(IMauiTestContext context)
    {
        var page = new SavedPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}

/// <summary>The saved musicians' cards.</summary>
public sealed class SavedList : CollectionObjectBase<SavedPage, SavedList, SavedCard>
{
    /// <summary>Creates the collection within the saved page.</summary>
    public SavedList(IMauiScope<SavedPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("Suggestion"),
               (collection, itemRoot, index) => new SavedCard(collection, itemRoot, index))
    {
    }

    /// <summary>Asserts <paramref name="musician"/> is saved.</summary>
    [BddThen("{control} should include {value}")]
    public SavedList AssertSaved(string musician)
    {
        if (!RunWait(() => FindItem(card => card.NameLabel.GetText() == musician) is not null, TestConstants.PageTimeoutMs))
        {
            throw new AssertionException($"Expected '{musician}' among the saved musicians.");
        }

        return Self;
    }
}

/// <summary>One saved musician.</summary>
public sealed class SavedCard : ItemObjectBase<SavedList, SavedCard>
{
    /// <summary>Creates a card from the root the collection found.</summary>
    public SavedCard(SavedList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The musician's name.</summary>
    public Label<SavedCard> NameLabel => new(this, "Suggestion_Name");
}

/// <summary>Page object for connecting an AI.</summary>
public class ConnectPage(IMauiTestContext context) : PageObjectBase<ConnectPage>(context)
{
    /// <inheritdoc />
    public override string Name => "ConnectPage";

    /// <summary>The provider, Azure first.</summary>
    public Picker<ConnectPage> Provider => new(this, "Connect_Provider");

    /// <summary>The endpoint, for Azure and a local server.</summary>
    public Entry<ConnectPage> Endpoint => new(this, "Connect_Endpoint");

    /// <summary>The model, or Azure's deployment.</summary>
    public Entry<ConnectPage> Model => new(this, "Connect_Model");

    /// <summary>The API key.</summary>
    [UatName("Api Key")]
    public Entry<ConnectPage> ApiKey => new(this, "Connect_ApiKey");

    /// <summary>"✓ The connection works", or what is wrong.</summary>
    public Label<ConnectPage> Status => new(this, "Connect_Status");

    /// <summary>Tests the connection.</summary>
    public Button<ConnectPage> TestButton => new(this, "Connect_Test");

    /// <summary>Tests, keeps the connection, and goes to Search.</summary>
    [UatName("Done")]
    public Button<ConnectPage> DoneButton => new(this, "Connect_Done");

    /// <summary>The connect page, once on screen.</summary>
    public static ConnectPage Arrived(IMauiTestContext context)
    {
        var page = new ConnectPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
