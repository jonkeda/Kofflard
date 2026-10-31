using Bruinet.Core.Scopes;
using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Containers;

/// <summary>
/// The results' cards (<c>Results_Items</c>, a CollectionView). Cards are found by the repeating
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

    /// <summary>The names of the cards, in the order shown.</summary>
    public IReadOnlyList<string> Names() => [.. Items.Select(card => TryReadName(card) ?? string.Empty)];

    /// <summary>The card of <paramref name="musician"/>, once the AI has answered and it is shown.</summary>
    /// <remarks>
    /// While the AI answers, the list is not on screen at all (the state container shows Loading), so the
    /// wait is on the page's state first, which also lets a scenario go straight from "I should be on the
    /// Results page" to a card.
    /// </remarks>
    public SuggestionCard Card(string musician)
    {
        new ResultsPage(Context).WaitForSuggestions();
        if (!RunWait(() => FindItem(card => TryReadName(card) == musician) is not null, TestConstants.PageTimeoutMs))
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

    /// <summary>Asserts <paramref name="musician"/>'s card is in <paramref name="lane"/>: Close, Stretch or Leap.</summary>
    [BddThen("{item} should be a {value} suggestion")]
    public SuggestionList AssertLane(string musician, string lane)
    {
        Card(musician).Lane.AssertText(lane);
        return Self;
    }

    /// <summary>Asserts <paramref name="musician"/>'s main button opens <paramref name="service"/>.</summary>
    [BddThen("{item} should open in {value} first")]
    public SuggestionList AssertOpensFirstIn(string musician, string service)
    {
        Card(musician).OpenButton.AssertText($"▶ {service}");
        return Self;
    }

    /// <summary>Opens <paramref name="musician"/> with the main button: the Open with service.</summary>
    [BddWhen("I open {item}")]
    public SuggestionList Open(string musician)
    {
        Card(musician).OpenButton.Click();
        return Self;
    }

    /// <summary>Opens <paramref name="musician"/> in <paramref name="service"/>, whichever button it is on.</summary>
    [BddWhen("I open {item} in {value}")]
    public SuggestionList OpenIn(string musician, string service)
    {
        var card = Card(musician);
        var button = new[] { card.OpenButton, card.SecondButton, card.ThirdButton }
            .FirstOrDefault(candidate => (candidate.GetText() ?? string.Empty).TrimStart('▶', ' ') == service)
            ?? throw new AssertionException($"'{musician}' has no button for '{service}'.");
        button.Click();
        return Self;
    }

    /// <summary>Saves <paramref name="musician"/>.</summary>
    [BddWhen("I save {item}")]
    public SuggestionList Save(string musician)
    {
        Card(musician).SaveButton.Click();
        Card(musician).SaveButton.AssertText("★ Saved");
        return Self;
    }

    /// <summary>Searches from <paramref name="musician"/>: "More like this".</summary>
    [BddWhen("I explore {item}")]
    public ResultsPage Explore(string musician)
    {
        Card(musician).ExploreButton.Click();
        var next = new ResultsPage(Context);
        next.Heading.AssertText($"Like {musician}", timeoutMs: TestConstants.PageTimeoutMs);
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

    /// <summary>"Close", "Stretch" or "Leap".</summary>
    public Label<SuggestionCard> Lane => new(this, "Suggestion_Lane");

    /// <summary>The musician's name.</summary>
    public Label<SuggestionCard> NameLabel => new(this, "Suggestion_Name");

    /// <summary>Why it fits, or "Bridge: …" for a leap.</summary>
    public Label<SuggestionCard> Why => new(this, "Suggestion_Why");

    /// <summary>"✓" once MusicBrainz confirmed the name.</summary>
    public Label<SuggestionCard> Verified => new(this, "Suggestion_Verified");

    /// <summary>The main button: "▶ YouTube".</summary>
    public TextButton<SuggestionCard> OpenButton => new(this, "Suggestion_Open");

    /// <summary>The second service.</summary>
    public TextButton<SuggestionCard> SecondButton => new(this, "Suggestion_Second");

    /// <summary>The third service.</summary>
    public TextButton<SuggestionCard> ThirdButton => new(this, "Suggestion_Third");

    /// <summary>"☆ Save" or "★ Saved".</summary>
    public TextButton<SuggestionCard> SaveButton => new(this, "Suggestion_Save");

    /// <summary>"More like this →".</summary>
    public Button<SuggestionCard> ExploreButton => new(this, "Suggestion_Explore");
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
