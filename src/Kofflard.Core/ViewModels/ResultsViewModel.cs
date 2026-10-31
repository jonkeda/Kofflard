using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The results: the suggestions in their lanes, Safe, Curious and Wild, or who the user may have meant
/// (design §3.1, .my/design/adventure-as-outcomes.md). Cards come in one by one as the AI writes them
/// (.my/design/streaming-answers.md). On the desktop design it is the results page; on a phone, the list of
/// the one page, filtered by the lane toggles (.my/design/single-page-phone.md).
/// </summary>
public sealed class ResultsViewModel : ParentViewModel
{
    /// <summary>The state while the AI answers.</summary>
    public const string LoadingState = "Loading";

    /// <summary>The state showing suggestions.</summary>
    public const string ContentState = "Content";

    /// <summary>The state asking which musician the user meant.</summary>
    public const string CandidatesState = "Candidates";

    /// <summary>The state when the AI suggested nobody usable.</summary>
    public const string EmptyState = "Empty";

    /// <summary>The state after a failure.</summary>
    public const string ErrorState = "Error";

    /// <summary>On a card while its "More →" waits for the first card.</summary>
    public const string Searching = "Searching…";

    private readonly DiscoveryService _discovery;
    private readonly ISettingsStore _settings;
    private readonly ISavedStore _saved;
    private readonly IArtistVerifier _verifier;
    private readonly SuggestionCards _cards;
    private readonly AiConnections _connections;
    private readonly INavigator _navigator;
    private readonly SearchLauncher _launcher;
    private readonly Dictionary<Lane, List<SuggestionViewModel>> _byLane = [];

    private CancellationTokenSource? _work;
    private CancellationTokenSource? _extra;
    private CancellationTokenSource? _exploring;
    private LaneSet _shown = LaneSet.All;
    private string _artist = string.Empty;
    private string _stateKey = LoadingState;
    private string _summary = string.Empty;
    private string _candidatesTitle = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _canFixConnection;
    private string _connectionLine = string.Empty;
    private bool _isStreaming;
    private string? _streamError;
    private SuggestionViewModel? _selectedCard;

    /// <summary>Creates the view model.</summary>
    public ResultsViewModel(
        DiscoveryService discovery,
        ISettingsStore settings,
        ISavedStore saved,
        IArtistVerifier verifier,
        SuggestionCards cards,
        AiConnections connections,
        INavigator navigator,
        SearchLauncher launcher)
    {
        _discovery = discovery;
        _settings = settings;
        _saved = saved;
        _verifier = verifier;
        _cards = cards;
        _connections = connections;
        _navigator = navigator;
        _launcher = launcher;

        RetryCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false));
        MoreCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false));
        ChooseCandidateCommand = new TargetAsyncCommand<Musician>(this, ChooseAsync);
        FixConnectionCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Connect));
    }

    /// <summary>"Like Nick Drake".</summary>
    public string Heading => $"Like {_artist}";

    /// <summary>The musician searched for.</summary>
    public string Artist => _artist;

    /// <summary>How many of each lane a search and "More like this" ask for: 4 + 4 + 4 unless a phone's toggles say otherwise.</summary>
    public LaneMix Mix { get; set; } = LaneMix.Even;

    /// <summary>
    /// Where a card's "More →" goes: on a phone the one page loads it in place. Null on the desktop design,
    /// where it opens a results page on the first card.
    /// </summary>
    public ISearchFlow? Flow { get; set; }

    /// <summary>
    /// The lanes shown; the cards of the others are kept, hidden. Every lane on the desktop design; on a
    /// phone, the toggles that are on.
    /// </summary>
    public LaneSet Shown
    {
        get => _shown;
        set
        {
            if (_shown == value)
            {
                return;
            }

            _shown = value;
            Lanes.Clear();
            foreach (var lane in Enum.GetValues<Lane>().Where(lane => value.Contains(lane) && _byLane.TryGetValue(lane, out var cards) && cards.Count > 0))
            {
                Lanes.Add(new LaneGroupViewModel(lane, _byLane[lane]));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CountText));
        }
    }

    /// <summary>"8 of 12": the cards shown of those found.</summary>
    public string CountText => $"{Lanes.Sum(lane => lane.Count)} of {_byLane.Values.Sum(cards => cards.Count)}";

    /// <summary>Whether <paramref name="lane"/> has cards, shown or not.</summary>
    public bool Has(Lane lane) => _byLane.TryGetValue(lane, out var cards) && cards.Count > 0;

    /// <summary>The musician searched for, in one line: "English folk singer-songwriter, 1969–74".</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>Loading, Content, Candidates, Empty or Error.</summary>
    public string State
    {
        get => _stateKey;
        private set
        {
            if (SetProperty(ref _stateKey, value))
            {
                OnPropertyChanged(nameof(StateKey));
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(CanAskMore));
            }
        }
    }

    /// <summary>
    /// <see cref="State"/> as the StateContainer binds it: null for the suggestions, which are its default
    /// content (as in Todo's list).
    /// </summary>
    public string? StateKey => _stateKey == ContentState ? null : _stateKey;

    /// <summary>Whether suggestions are shown.</summary>
    public bool HasContent => _stateKey == ContentState;

    /// <summary>Whether cards are shown while the AI is still writing more: "More on the way…".</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                OnPropertyChanged(nameof(CanAskMore));
            }
        }
    }

    /// <summary>Whether "More like this" is offered: once the answer is complete, so two never run at once.</summary>
    public bool CanAskMore => HasContent && !_isStreaming;

    /// <summary>What went wrong after some cards were shown; they stay, and this shows under them.</summary>
    public string? StreamError
    {
        get => _streamError;
        private set
        {
            if (SetProperty(ref _streamError, value))
            {
                OnPropertyChanged(nameof(HasStreamError));
            }
        }
    }

    /// <summary>Whether <see cref="StreamError"/> is shown.</summary>
    public bool HasStreamError => _streamError is not null;

    /// <summary>Tapping a candidate selects it, which searches it; the selection is then cleared.</summary>
    public Musician? SelectedCandidate
    {
        get => null;
        set
        {
            if (value is not null)
            {
                OnPropertyChanged();
                ChooseCandidateCommand.Execute(value);
            }
        }
    }

    /// <summary>
    /// Clicking or tapping a card selects it, which opens it in the Open with service; the selection is then
    /// cleared, as Bruinet's Todo sample does (.my/rca/card-click.md).
    /// </summary>
    public SuggestionViewModel? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (!SetProperty(ref _selectedCard, value) || value is null)
            {
                return;
            }

            _ = value.OpenCommand.ExecuteAsync(null);
            SetProperty(ref _selectedCard, null);
        }
    }

    /// <summary>The lanes that have suggestions, in order.</summary>
    public ObservableCollection<LaneGroupViewModel> Lanes { get; } = [];

    /// <summary>Every card, in lane order, shown or not.</summary>
    public IEnumerable<SuggestionViewModel> Cards => Enum.GetValues<Lane>().Where(_byLane.ContainsKey).SelectMany(lane => _byLane[lane]);

    /// <summary>Who the user may have meant.</summary>
    public ObservableCollection<Musician> Candidates { get; } = [];

    /// <summary>"Which Genesis do you mean?" or "Did you mean…".</summary>
    public string CandidatesTitle
    {
        get => _candidatesTitle;
        private set => SetProperty(ref _candidatesTitle, value);
    }

    /// <summary>What went wrong, in the Error state.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>"AI: Azure OpenAI / Foundry · gpt-4.1-mini" under an error: the first thing to know when an answer fails.</summary>
    public string ConnectionLine
    {
        get => _connectionLine;
        private set => SetProperty(ref _connectionLine, value);
    }

    /// <summary>Whether the error is about the connection, so "Fix connection" is offered.</summary>
    public bool CanFixConnection
    {
        get => _canFixConnection;
        private set => SetProperty(ref _canFixConnection, value);
    }

    /// <summary>Tries the search again.</summary>
    public IAsyncRelayCommand RetryCommand { get; }

    /// <summary>Asks for more musicians like this one; those already shown are excluded.</summary>
    public IAsyncRelayCommand MoreCommand { get; }

    /// <summary>Searches the candidate the user picked.</summary>
    public IAsyncRelayCommand<Musician> ChooseCandidateCommand { get; }

    /// <summary>Opens Connect.</summary>
    public IAsyncRelayCommand FixConnectionCommand { get; }

    /// <summary>The verification still running, if any; tests wait on it.</summary>
    public Task Verifying { get; private set; } = Task.CompletedTask;

    /// <summary>Searches for <paramref name="artist"/>.</summary>
    public Task LoadAsync(string artist, bool cached)
    {
        _artist = artist;
        OnPropertyChanged(nameof(Heading));
        return SearchAsync(cached);
    }

    /// <summary>Stops the search and the verification when the page goes away for good.</summary>
    public void Stop()
    {
        _work?.Cancel();
        _extra?.Cancel();
        _exploring?.Cancel();
    }

    /// <summary>
    /// Asks for <see cref="LaneMix.OneLane"/> picks of <paramref name="lane"/> alone, for a phone toggle turned
    /// on with no cards in that lane yet; they stream into its section and the other cards stay
    /// (.my/design/single-page-phone.md). Nothing happens without results, or when the lane has cards.
    /// </summary>
    public async Task AskForAsync(Lane lane)
    {
        if (!HasContent || Has(lane))
        {
            return;
        }

        _extra?.Cancel();
        _extra = new CancellationTokenSource();
        var token = _extra.Token;

        var settings = await _settings.LoadAsync();
        var saved = await _saved.NamesAsync();
        var mix = LaneMix.Only(lane);
        var shown = Cards.Select(card => Names.Normalise(card.Name)).ToHashSet();
        var added = 0;
        void Show(Suggestion suggestion)
        {
            if (suggestion.Lane == lane && added < mix.Sum && shown.Add(Names.Normalise(suggestion.Name)))
            {
                added++;
                AddCard(_cards.Create(suggestion, settings.OpenWith, saved.Contains(Names.Normalise(suggestion.Name)), ExploreAsync));
            }
        }

        IsStreaming = true;
        StreamError = null;
        try
        {
            await foreach (var update in _discovery.DiscoverAsync(_artist, useCache: false, mix, token))
            {
                switch (update)
                {
                    case DiscoveryUpdate.PickFound pick:
                        Show(pick.Suggestion);
                        break;
                    case DiscoveryUpdate.Done { Result.Status: DiscoveryStatus.Ok } done:
                        done.Result.Suggestions.ToList().ForEach(Show);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (AiException failure)
        {
            StreamError = failure.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsStreaming = false;
            }
        }
    }

    private async Task SearchAsync(bool useCache)
    {
        _work?.Cancel();
        _extra?.Cancel();
        _work = new CancellationTokenSource();
        var token = _work.Token;

        State = LoadingState;
        Lanes.Clear();
        _byLane.Clear();
        OnPropertyChanged(nameof(CountText));
        Candidates.Clear();
        Summary = string.Empty;
        IsStreaming = false;
        StreamError = null;

        var settings = await _settings.LoadAsync();
        var saved = await _saved.NamesAsync();
        void Show(Suggestion suggestion)
            => AddCard(_cards.Create(suggestion, settings.OpenWith, saved.Contains(Names.Normalise(suggestion.Name)), ExploreAsync));

        DiscoveryResult? result = null;
        try
        {
            await foreach (var update in _discovery.DiscoverAsync(_artist, useCache, Mix, token))
            {
                switch (update)
                {
                    case DiscoveryUpdate.ArtistFound found:
                        Summary = found.Artist.Summary ?? string.Empty;
                        break;
                    case DiscoveryUpdate.PickFound pick:
                        Show(pick.Suggestion);
                        IsStreaming = true;
                        State = ContentState;
                        break;
                    case DiscoveryUpdate.Done done:
                        result = done.Result;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (AiException failure)
        {
            IsStreaming = false;
            if (Lanes.Count > 0)
            {
                StreamError = failure.Message;
                return;
            }

            await ShowErrorAsync(failure.Message, failure.Failure is AiFailure.NotConnected or AiFailure.InvalidKey or AiFailure.ModelNotFound or AiFailure.NoCredit);
            return;
        }

        IsStreaming = false;
        if (token.IsCancellationRequested || result is null)
        {
            return;
        }

        Summary = result.Artist?.Summary ?? string.Empty;

        if (result.Status != DiscoveryStatus.Ok)
        {
            Lanes.Clear();
            _byLane.Clear();
            ShowCandidates(result);
            return;
        }

        // The whole answer holds every card shown, and may top up a short lane; a card shown stays.
        var shown = Cards.Select(card => Names.Normalise(card.Name)).ToHashSet();
        foreach (var suggestion in result.Suggestions.Where(suggestion => !shown.Contains(Names.Normalise(suggestion.Name))))
        {
            if (shown.Count >= Mix.Sum)
            {
                break;
            }

            shown.Add(Names.Normalise(suggestion.Name));
            Show(suggestion);
        }

        if (_byLane.Count == 0)
        {
            State = EmptyState;
            return;
        }

        State = ContentState;

        if (settings.CheckNames)
        {
            Verifying = VerifyAsync([.. Cards], token);
        }
    }

    // Into its lane, after the cards already there; a shown lane is added, in lane order, with its first card.
    private void AddCard(SuggestionViewModel card)
    {
        var lane = card.Lane;
        if (!_byLane.TryGetValue(lane, out var cards))
        {
            _byLane[lane] = cards = [];
        }

        cards.Add(card);
        if (_shown.Contains(lane))
        {
            if (Lanes.FirstOrDefault(group => group.Lane == lane) is { } group)
            {
                group.Add(card);
            }
            else
            {
                Lanes.Insert(Lanes.Count(other => other.Lane < lane), new LaneGroupViewModel(lane, [card]));
            }
        }

        OnPropertyChanged(nameof(CountText));
    }

    private void ShowCandidates(DiscoveryResult result)
    {
        foreach (var candidate in result.Candidates)
        {
            Candidates.Add(candidate);
        }

        CandidatesTitle = result.Status == DiscoveryStatus.Ambiguous
            ? $"Which {_artist} do you mean?"
            : result.Candidates.Count > 0
                ? $"No musician called {_artist} is known. Did you mean…"
                : $"No musician called {_artist} is known. Check the spelling and try again.";
        State = CandidatesState;
    }

    private async Task ShowErrorAsync(string message, bool connection)
    {
        ErrorMessage = message;
        CanFixConnection = connection;
        ConnectionLine = await _connections.GetAsync() is { } current ? $"AI: {current.Describe()}" : "No AI connected";
        State = ErrorState;
    }

    // One name at a time, in lane order: MusicBrainz allows about one request a second (design §8.4).
    private async Task VerifyAsync(IReadOnlyList<SuggestionViewModel> cards, CancellationToken token)
    {
        foreach (var card in cards)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                card.Verification = await _verifier.VerifyAsync(card.Name, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpRequestException)
            {
                // MusicBrainz unreachable: the cards stay unverified, which is what they are.
                return;
            }
        }
    }

    private Task ChooseAsync(Musician? candidate)
    {
        if (candidate is null)
        {
            return Task.CompletedTask;
        }

        _artist = candidate.Name;
        OnPropertyChanged(nameof(Heading));
        return SearchAsync(useCache: false);
    }

    // On a phone the page loads it in place. On the desktop design the card says "Searching…" until the
    // first card opens the next results page, or says what came of it (.my/design/desktop-aurora-changes.md).
    private async Task ExploreAsync(SuggestionViewModel card)
    {
        if (Flow is { } flow)
        {
            await flow.ShowAsync(card.Name, cached: false);
            return;
        }

        _exploring?.Cancel();
        _exploring = new CancellationTokenSource();
        var token = _exploring.Token;

        card.ExploreNote = Searching;
        var search = await _launcher.StartAsync(card.Name, cached: false, token);
        if (token.IsCancellationRequested)
        {
            card.ExploreNote = string.Empty;
            return;
        }

        card.ExploreNote = search is null ? string.Empty : SearchLauncher.NothingFound(search);
    }
}
