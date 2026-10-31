using Bruinet.Core.Scopes;
using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Containers;

/// <summary>
/// The results' cards (<c>Results_Items</c>, a grouped CollectionView). Cards are found by the repeating
/// <c>Suggestion</c> id and told apart by name.
/// </summary>
public sealed class SuggestionList : CollectionObjectBase<ResultsPage, SuggestionList, SuggestionCard>
{

    // A card gone mid-read costs this, not a full default wait.
    private const int VanishedCardReadMs = 250;

    /// <summary>Creates the collection within the results page.</summary>
    public SuggestionList(IMauiScope<ResultsPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("Suggestion"),
               (collection, itemRoot, index) => new SuggestionCard(collection, itemRoot, index))
    {
    }

    // A fresh page object each time, so it finds the page shown now: after "More →" the page this list was
    // made on may be covered by the next one (.my/rca/stacked-pages.md).
    private ResultsPage Results => new(Context);

    /// <summary>The names of the cards, in the order shown.</summary>
    public IReadOnlyList<string> Names() => [.. Items.Select(card => TryReadName(card) ?? string.Empty)];

    /// <summary>The card of <paramref name="musician"/>, once the AI has answered.</summary>
    /// <remarks>
    /// While the AI answers, the list is not on screen at all (the state container shows Loading), so the
    /// wait is on the page's state first, which also lets a scenario go straight from "I should be on the
    /// Results page" to a card.
    /// </remarks>
    public SuggestionCard Card(string musician)
    {
        Results.WaitForSuggestions();
        return Shown(musician);
    }

    /// <summary>The card of <paramref name="musician"/>, without waiting for the answer first.</summary>
    public SuggestionCard Shown(string musician)
    {
        var budget = TestConstants.ListSearchMs(Context.Platform);
        if (!RunWait(() => FindItem(card => TryReadName(card) == musician, budget) is not null, budget))
        {
            throw new AssertionException(
                $"Expected a card for '{musician}'. Cards shown: {string.Join(", ", Names().Select(name => $"'{name}'"))}.");
        }

        return ItemWhere(card => TryReadName(card) == musician);
    }

    /// <summary>Asserts a card is shown for <paramref name="musician"/>.</summary>
    [BddThen("{control} should suggest {value}")]
    public SuggestionList AssertCard(string musician)
    {
        Card(musician);
        return Self;
    }

    /// <summary>
    /// Asserts <paramref name="musician"/>'s card is in <paramref name="lane"/>: Safe, Curious or Wild. The cards
    /// come in lane order, so a card's place and the lanes' totals say which lane it is under.
    /// </summary>
    /// <remarks>Reads the cards realised now: every card headless; a long list on a device may need scrolling first.</remarks>
    [BddThen("{item} should be a {value} suggestion")]
    public SuggestionList AssertLane(string musician, string lane)
    {
        var results = Results.WaitForSuggestions();
        Shown(musician);
        var place = Names().ToList().IndexOf(musician);
        var end = 0;
        foreach (var (name, total) in results.LaneHeaders.Totals())
        {
            end += total;
            if (place < end)
            {
                Assert.Equal(lane, name);
                return Self;
            }
        }

        throw new AssertionException($"'{musician}' is card {place + 1}, past every lane: {results.LaneHeaders.Summary()}.");
    }

    /// <summary>Asserts a tap on <paramref name="musician"/> opens <paramref name="service"/>: the Open with service.</summary>
    [BddThen("{item} should open in {value} first")]
    public SuggestionList AssertOpensFirstIn(string musician, string service)
    {
        Select(musician);
        Card(musician).Opened.AssertText($"Opened in {service}", timeoutMs: TestConstants.PageTimeoutMs);
        return Self;
    }

    /// <summary>Scrolls <paramref name="musician"/>'s card into view: the leaps are below the fold.</summary>
    [BddWhen("I look at {item}")]
    public SuggestionList LookAt(string musician)
    {
        Card(musician).NameLabel.ScrollIntoView();
        return Self;
    }

    /// <summary>Asserts <paramref name="musician"/>'s card says <paramref name="text"/> after an open: "Opened in YouTube".</summary>
    [BddThen("{item} should say {value}")]
    public SuggestionList AssertSays(string musician, string text)
    {
        Card(musician).Opened.AssertText(text, timeoutMs: TestConstants.PageTimeoutMs);
        return Self;
    }

    /// <summary>Opens <paramref name="musician"/> with a tap: the Open with service.</summary>
    [BddWhen("I open {item}")]
    public SuggestionList Open(string musician)
    {
        Select(musician);
        return Self;
    }

    /// <summary>
    /// Selects <paramref name="musician"/>'s list item, which is what a click or tap on the card does, as in
    /// Bruinet's Todo sample (.my/rca/card-click.md).
    /// </summary>
    public SuggestionList Select(string musician)
        => SelectItem(Card(musician).Index, TestConstants.PageTimeoutMs);

    /// <summary>
    /// Opens <paramref name="musician"/> in <paramref name="service"/>: a tap for the Open with service, else a
    /// long press and the service's link in "Open in".
    /// </summary>
    [BddWhen("I open {item} in {value}")]
    public SuggestionList OpenIn(string musician, string service)
    {
        var card = Card(musician);
        ShowOtherServices(musician);
        if (new[] { card.SecondButton, card.ThirdButton }.FirstOrDefault(candidate => candidate.GetText() == service) is { } link)
        {
            link.Click();
            return Self;
        }

        // Not one of the other two, so it is the Open with service: hide them again and tap.
        card.Surface.LongPress();
        return Select(musician);
    }

    /// <summary>Long-presses <paramref name="musician"/>'s card, so it offers the other two services.</summary>
    [BddWhen("I long press {item}")]
    public SuggestionList ShowOtherServices(string musician)
    {
        var card = Card(musician);
        card.Surface.LongPress();
        card.SecondButton.AssertVisible(true, timeoutMs: TestConstants.PageTimeoutMs);
        return Self;
    }

    /// <summary>Adds <paramref name="musician"/> to favourites with the star in front of the name.</summary>
    [BddWhen("I add {item} to favourites")]
    public SuggestionList Save(string musician)
    {
        Card(musician).Star.Click();

        // Headless reads the button's text; Windows its accessible name, which the star's description
        // replaces (.my/rca/windows-vs-headless.md, B10).
        var star = Card(musician).Star;
        string[] saved = ["★", $"Remove {musician} from favourites"];
        if (!RunWait(() => saved.Contains(star.GetText()), TestConstants.PageTimeoutMs))
        {
            throw new AssertionException($"Expected '{musician}' in favourites; the star says '{star.GetText()}'.");
        }

        return Self;
    }

    /// <summary>Searches from <paramref name="musician"/>: "More →".</summary>
    [BddWhen("I explore {item}")]
    public ResultsPage Explore(string musician)
    {
        Card(musician).ExploreButton.Click();
        var next = new ResultsPage(Context);
        next.Heading.AssertText($"Like {musician}", timeoutMs: TestConstants.PageTimeoutMs);

        // Until the AI has answered: an answer that lands in the next test would mark its names as shown there.
        next.State.WaitShowing(ResultsState.Loading, false, TestConstants.PageTimeoutMs);
        return next;
    }

    // A new answer replaces the cards, so one listed a moment ago can be gone before its name is read.
    private static string? TryReadName(SuggestionCard card)
    {
        try
        {
            return card.NameLabel.GetText(VanishedCardReadMs);
        }
        catch (ScopeNotReadyException vanished) when (vanished.Readiness.State == ScopeReadinessState.MissingRoot)
        {
            return null;
        }
    }

}

/// <summary>One card, scoped to its own item root. Every id repeats unchanged on every card.</summary>
public sealed class SuggestionCard : ItemObjectBase<SuggestionList, SuggestionCard>
{
    /// <summary>Creates a card from the root the collection found.</summary>
    public SuggestionCard(SuggestionList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The musician's name.</summary>
    public Label<SuggestionCard> NameLabel => new(this, "Suggestion_Name");

    /// <summary>Why it fits, or "Bridge: …" for a leap.</summary>
    public Label<SuggestionCard> Why => new(this, "Suggestion_Why");

    /// <summary>"Opened in YouTube" for a moment after an open.</summary>
    public Label<SuggestionCard> Opened => new(this, "Suggestion_Opened");

    /// <summary>"Searching…" while "More →" waits for its first card, then "Nothing found like …".</summary>
    public Label<SuggestionCard> ExploreNote => new(this, "Suggestion_ExploreNote");

    /// <summary>The card itself: a tap opens the Open with service, a long press offers the others.</summary>
    public CardSurface<SuggestionCard> Surface => new(this, "Suggestion_Card");

    /// <summary>The second service, in "Open in".</summary>
    public TextButton<SuggestionCard> SecondButton => new(this, "Suggestion_Second");

    /// <summary>The third service, in "Open in".</summary>
    public TextButton<SuggestionCard> ThirdButton => new(this, "Suggestion_Third");

    /// <summary>The star in front of the name: "☆", or "★" in favourites.</summary>
    public TextButton<SuggestionCard> Star => new(this, "Suggestion_Save");

    /// <summary>"More →" at the end of the name.</summary>
    public Button<SuggestionCard> ExploreButton => new(this, "Suggestion_Explore");
}

/// <summary>The lanes' headers in the results (<c>LaneHeader</c> rows in <c>Results_Items</c>): "Safe (6)".</summary>
public sealed class LaneHeaderList : CollectionObjectBase<ResultsPage, LaneHeaderList, LaneHeader>
{
    /// <summary>Creates the collection within the results page.</summary>
    public LaneHeaderList(IMauiScope<ResultsPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("LaneHeader"),
               (collection, itemRoot, index) => new LaneHeader(collection, itemRoot, index))
    {
    }

    /// <summary>"Safe 6 · Curious 4 · Wild 2": each lane and its total, in order.</summary>
    public string Summary() => string.Join(" · ", Totals().Select(lane => $"{lane.Name} {lane.Total}"));

    /// <summary>Each lane and its total, in order.</summary>
    /// <remarks>
    /// Each lane is looked up by name, not read from the headers realised now: below a lane's cards the next
    /// header can be off screen, and on Windows and Android it is then not in the tree.
    /// </remarks>
    public IReadOnlyList<(string Name, int Total)> Totals()
    {
        var budget = TestConstants.ListSearchMs(Context.Platform);
        return [.. ResultsPage.Lanes
            .Select(lane => FindItem(header => header.Name == lane, budget))
            .OfType<LaneHeader>()
            .Select(header => (header.Name, header.Total))];
    }

    /// <summary>The header of <paramref name="lane"/>.</summary>
    public LaneHeader Lane(string lane)
    {
        var budget = TestConstants.ListSearchMs(Context.Platform);
        if (!RunWait(() => FindItem(header => header.Name == lane, budget) is not null, budget))
        {
            throw new AssertionException($"Expected a lane '{lane}'. Lanes shown: {Summary()}.");
        }

        return ItemWhere(header => header.Name == lane);
    }
}

/// <summary>One lane's header: "Safe (6)".</summary>
public sealed class LaneHeader : ItemObjectBase<LaneHeaderList, LaneHeader>
{
    /// <summary>Creates a header from the root the collection found.</summary>
    public LaneHeader(LaneHeaderList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The header's text.</summary>
    public Label<LaneHeader> NameLabel => new(this, "LaneHeader_Name");

    private string Text => NameLabel.GetText() ?? string.Empty;

    /// <summary>"Safe".</summary>
    public string Name => Text.Split(" (")[0];

    /// <summary>The lane's suggestions.</summary>
    public int Total => int.Parse(Text.Split(" (")[1].TrimEnd(')'), System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Who the user may have meant (<c>Results_CandidateList</c>).</summary>
public sealed class CandidateList : CollectionObjectBase<ResultsPage, CandidateList, CandidateRow>
{
    /// <summary>Creates the collection within the results page.</summary>
    public CandidateList(IMauiScope<ResultsPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("CandidateRow"),
               (collection, itemRoot, index) => new CandidateRow(collection, itemRoot, index))
    {
    }

    /// <summary>The candidates' names, in the order shown.</summary>
    public IReadOnlyList<string> Names() => [.. Items.Select(row => row.NameLabel.GetText() ?? string.Empty)];

    /// <summary>Searches the candidate called <paramref name="musician"/>.</summary>
    [BddWhen("I choose {item}")]
    public ResultsPage Choose(string musician)
    {
        if (!RunWait(() => FindItem(row => row.NameLabel.GetText() == musician) is not null, TestConstants.PageTimeoutMs))
        {
            throw new AssertionException($"Expected a candidate '{musician}'. Shown: {string.Join(", ", Names())}.");
        }

        SelectItem(ItemWhere(row => row.NameLabel.GetText() == musician).Index, TestConstants.PageTimeoutMs);
        return new ResultsPage(Context);
    }
}

/// <summary>One candidate.</summary>
public sealed class CandidateRow : ItemObjectBase<CandidateList, CandidateRow>
{
    /// <summary>Creates a row from the root the collection found.</summary>
    public CandidateRow(CandidateList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The candidate's name.</summary>
    public Label<CandidateRow> NameLabel => new(this, "CandidateRow_Name");
}
