using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The results page: the suggestions in their lanes, or who the user may have meant (design §3.1). The
/// slider shows the lanes up to how adventurous the user wants to go (.my/design/adventure-as-outcomes.md).
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

    private readonly DiscoveryService _discovery;
    private readonly ISettingsStore _settings;
    private readonly ISavedStore _saved;
    private readonly IArtistVerifier _verifier;
    private readonly SuggestionCards _cards;
    private readonly AiConnections _connections;
    private readonly INavigator _navigator;

    private CancellationTokenSource? _work;
    private string _artist = string.Empty;
    private Lane _adventure = AppSettings.Default.Adventure;
    private Lane _lean = LaneMix.Even;
    private string _stateKey = LoadingState;
    private string _summary = string.Empty;
    private string _candidatesTitle = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _canFixConnection;
    private string _connectionLine = string.Empty;

    /// <summary>Creates the view model.</summary>
    public ResultsViewModel(
        DiscoveryService discovery,
        ISettingsStore settings,
        ISavedStore saved,
        IArtistVerifier verifier,
        SuggestionCards cards,
        AiConnections connections,
        INavigator navigator)
    {
        _discovery = discovery;
        _settings = settings;
        _saved = saved;
        _verifier = verifier;
        _cards = cards;
        _connections = connections;
        _navigator = navigator;

        RetryCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false));
        MoreCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false, lean: _adventure));
        ChooseCandidateCommand = new TargetAsyncCommand<Musician>(this, ChooseAsync);
        FixConnectionCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Connect));
        AdventureOptions = [.. Enum.GetValues<Lane>().Select(lane => new AdventureOptionViewModel(lane, chosen => Adventure = chosen))];
        ShowChosenAdventure();
    }

    /// <summary>
    /// How adventurous: the lanes up to this one show. Changing it shows or hides lanes at once, with no new
    /// request, and is kept for the next search.
    /// </summary>
    public Lane Adventure
    {
        get => _adventure;
        set
        {
            if (SetProperty(ref _adventure, value))
            {
                OnPropertyChanged(nameof(AdventureName));
                ShowChosenAdventure();
                ShowLanes();
                Remembering = RememberAsync(Remembering, value);
            }
        }
    }

    /// <summary>"Safe", "Curious" or "Wild".</summary>
    public string AdventureName => _adventure.ToString();

    /// <summary>The slider's three stops, Safe first; exactly one is chosen.</summary>
    public IReadOnlyList<AdventureOptionViewModel> AdventureOptions { get; }

    /// <summary>"Like Nick Drake".</summary>
    public string Heading => $"Like {_artist}";

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
            }
        }
    }

    /// <summary>
    /// <see cref="State"/> as the StateContainer binds it: null for the suggestions, which are its default
    /// content (as in Todo's list).
    /// </summary>
    public string? StateKey => _stateKey == ContentState ? null : _stateKey;

    /// <summary>Whether suggestions are shown, so "More like this" is offered.</summary>
    public bool HasContent => _stateKey == ContentState;

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

    /// <summary>The lanes that have suggestions, in order; those past <see cref="Adventure"/> are empty.</summary>
    public ObservableCollection<LaneGroupViewModel> Lanes { get; } = [];

    /// <summary>Every card, in lane order, whether its lane is shown or not.</summary>
    public IEnumerable<SuggestionViewModel> Cards => Lanes.SelectMany(lane => lane.Cards);

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

    /// <summary>
    /// Asks for more musicians like this one, leaning to <see cref="Adventure"/>; those already shown are excluded.
    /// </summary>
    public IAsyncRelayCommand MoreCommand { get; }

    /// <summary>Searches the candidate the user picked.</summary>
    public IAsyncRelayCommand<Musician> ChooseCandidateCommand { get; }

    /// <summary>Opens Connect.</summary>
    public IAsyncRelayCommand FixConnectionCommand { get; }

    /// <summary>The save of <see cref="Adventure"/> still running, if any; tests wait on it.</summary>
    public Task Remembering { get; private set; } = Task.CompletedTask;

    /// <summary>The verification still running, if any; tests wait on it.</summary>
    public Task Verifying { get; private set; } = Task.CompletedTask;

    /// <summary>Searches for <paramref name="artist"/>.</summary>
    public async Task LoadAsync(string artist, bool cached)
    {
        _artist = artist;
        OnPropertyChanged(nameof(Heading));
        _adventure = (await _settings.LoadAsync()).Adventure;
        OnPropertyChanged(nameof(Adventure));
        OnPropertyChanged(nameof(AdventureName));
        ShowChosenAdventure();
        await SearchAsync(cached, LaneMix.Even);
    }

    /// <summary>Stops the verification when the page goes away for good.</summary>
    public void Stop() => _work?.Cancel();

    // A search asks for the even mix; "More like this…" leans to the slider. Retry asks as the last one did.
    private async Task SearchAsync(bool useCache, Lane? lean = null)
    {
        _lean = lean ?? _lean;
        _work?.Cancel();
        _work = new CancellationTokenSource();
        var token = _work.Token;

        State = LoadingState;
        Lanes.Clear();
        Candidates.Clear();
        Summary = string.Empty;

        DiscoveryResult result;
        try
        {
            result = await _discovery.DiscoverAsync(_artist, _lean, useCache, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (AiException failure)
        {
            await ShowErrorAsync(failure.Message, failure.Failure is AiFailure.NotConnected or AiFailure.InvalidKey or AiFailure.ModelNotFound or AiFailure.NoCredit);
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        Summary = result.Artist?.Summary ?? string.Empty;

        if (result.Status != DiscoveryStatus.Ok)
        {
            ShowCandidates(result);
            return;
        }

        if (result.Suggestions.Count == 0)
        {
            State = EmptyState;
            return;
        }

        var settings = await _settings.LoadAsync();
        var saved = await _saved.NamesAsync();
        foreach (var lane in Enum.GetValues<Lane>().Where(lane => result.Count(lane) > 0))
        {
            var cards = result.Suggestions
                .Where(suggestion => suggestion.Lane == lane)
                .Select(suggestion => _cards.Create(suggestion, settings.OpenWith, saved.Contains(Names.Normalise(suggestion.Name)), ExploreAsync))
                .ToList();
            Lanes.Add(new LaneGroupViewModel(lane, cards, lane <= _adventure));
        }

        State = ContentState;

        if (settings.CheckNames)
        {
            Verifying = VerifyAsync([.. Cards], token);
        }
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

    private void ShowChosenAdventure()
    {
        foreach (var option in AdventureOptions)
        {
            option.Show(option.Lane == _adventure);
        }
    }

    private void ShowLanes()
    {
        foreach (var lane in Lanes)
        {
            lane.IsShown = lane.Lane <= _adventure;
        }
    }

    // The slider's place is where the next results start, on any search, after a restart too.
    // One save at a time, in order, so quick slides end with the last one.
    private async Task RememberAsync(Task previous, Lane adventure)
    {
        await previous;
        var settings = await _settings.LoadAsync();
        await _settings.SaveAsync(settings with { Adventure = adventure });
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

    private Task ExploreAsync(SuggestionViewModel card)
        => _navigator.GoToAsync(KofflardRoutes.Results, KofflardRoutes.ForSearch(card.Name));
}
