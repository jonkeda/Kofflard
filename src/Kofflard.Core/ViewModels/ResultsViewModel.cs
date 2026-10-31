using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The results page: the suggestions in their lanes, or who the user may have meant (design §3.1).
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
    private readonly INavigator _navigator;

    private CancellationTokenSource? _work;
    private string _artist = string.Empty;
    private Adventure _adventure = Adventure.Default;
    private string _stateKey = LoadingState;
    private string _summary = string.Empty;
    private string _laneSummary = string.Empty;
    private string _candidatesTitle = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _canFixConnection;

    /// <summary>Creates the view model.</summary>
    public ResultsViewModel(
        DiscoveryService discovery,
        ISettingsStore settings,
        ISavedStore saved,
        IArtistVerifier verifier,
        SuggestionCards cards,
        INavigator navigator)
    {
        _discovery = discovery;
        _settings = settings;
        _saved = saved;
        _verifier = verifier;
        _cards = cards;
        _navigator = navigator;

        RetryCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false));
        MoreCommand = new TargetAsyncCommand(this, () => SearchAsync(useCache: false));
        ChooseCandidateCommand = new TargetAsyncCommand<Musician>(this, ChooseAsync);
        FixConnectionCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Connect));
    }

    /// <summary>"Like Nick Drake".</summary>
    public string Heading => $"Like {_artist}";

    /// <summary>The musician searched for, in one line: "English folk singer-songwriter, 1969–74".</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>"Adventure: Curious".</summary>
    public string AdventureText => $"Adventure: {_adventure.Name}";

    /// <summary>"Close 6 · Stretch 4 · Leap 2".</summary>
    public string LaneSummary
    {
        get => _laneSummary;
        private set => SetProperty(ref _laneSummary, value);
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

    /// <summary>The cards, in lane order.</summary>
    public ObservableCollection<SuggestionViewModel> Items { get; } = [];

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
    public Task LoadAsync(string artist, Adventure adventure, bool cached)
    {
        _artist = artist;
        _adventure = adventure;
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(AdventureText));
        return SearchAsync(cached);
    }

    /// <summary>Stops the verification when the page goes away for good.</summary>
    public void Stop() => _work?.Cancel();

    private async Task SearchAsync(bool useCache)
    {
        _work?.Cancel();
        _work = new CancellationTokenSource();
        var token = _work.Token;

        State = LoadingState;
        Items.Clear();
        Candidates.Clear();
        LaneSummary = string.Empty;
        Summary = string.Empty;

        DiscoveryResult result;
        try
        {
            result = await _discovery.DiscoverAsync(_artist, _adventure, useCache, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (AiException failure)
        {
            ShowError(failure.Message, failure.Failure is AiFailure.NotConnected or AiFailure.InvalidKey or AiFailure.ModelNotFound or AiFailure.NoCredit);
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
        foreach (var suggestion in result.Suggestions)
        {
            Items.Add(_cards.Create(suggestion, settings.OpenWith, saved.Contains(Names.Normalise(suggestion.Name)), ExploreAsync));
        }

        LaneSummary = string.Join(" · ", Enum.GetValues<Lane>()
            .Where(lane => result.Count(lane) > 0)
            .Select(lane => $"{lane} {result.Count(lane)}"));
        State = ContentState;

        if (settings.CheckNames)
        {
            Verifying = VerifyAsync([.. Items], token);
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

    private void ShowError(string message, bool connection)
    {
        ErrorMessage = message;
        CanFixConnection = connection;
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

    private Task ExploreAsync(SuggestionViewModel card)
        => _navigator.GoToAsync(KofflardRoutes.Results, KofflardRoutes.ForSearch(card.Name, _adventure));
}
