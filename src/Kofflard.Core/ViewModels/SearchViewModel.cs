using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The search: a name and the recent searches (design §3.1). On the desktop design it is the search page,
/// which waits for a search's first card before it opens the results, and shows any other outcome itself
/// (.my/design/desktop-aurora-changes.md). On a phone it is the dock and Recent of the one page, and
/// <see cref="Flow"/> shows the search (.my/design/single-page-phone.md).
/// </summary>
public sealed class SearchViewModel : ParentViewModel
{
    /// <summary>The error when Go is pressed with nothing typed.</summary>
    public const string NameRequired = "Type a musician's name";

    /// <summary>Find musicians, while it waits for the first card.</summary>
    public const string SearchingText = "Searching…";

    /// <summary>Find musicians.</summary>
    public const string GoText = "Find musicians";

    private readonly DiscoveryService _discovery;
    private readonly AiConnections _connections;
    private readonly INavigator _navigator;
    private readonly SearchLauncher _launcher;

    private CancellationTokenSource? _pending;
    private string _name = string.Empty;
    private string? _nameError;
    private bool _isWaiting;
    private string _message = string.Empty;
    private bool _canFixConnection;
    private string _candidatesTitle = string.Empty;

    /// <summary>Creates the view model.</summary>
    public SearchViewModel(DiscoveryService discovery, AiConnections connections, INavigator navigator, SearchLauncher launcher)
    {
        _discovery = discovery;
        _connections = connections;
        _navigator = navigator;
        _launcher = launcher;

        SearchCommand = new TargetAsyncCommand(this, SearchAsync);
        OpenRecentCommand = new TargetAsyncCommand<RecentSearch>(this, OpenRecentAsync);
        RemoveRecentCommand = new TargetAsyncCommand<RecentSearch>(this, RemoveRecentAsync);
        ChooseCandidateCommand = new TargetAsyncCommand<Musician>(this, ChooseAsync);
        FixConnectionCommand = new TargetAsyncCommand(this, () => LeaveAsync(KofflardRoutes.Connect));
        SavedCommand = new TargetAsyncCommand(this, () => LeaveAsync(KofflardRoutes.Saved));
        SettingsCommand = new TargetAsyncCommand(this, () => LeaveAsync(KofflardRoutes.Settings));
    }

    /// <summary>Where a search is shown on a phone; null on the desktop design, which opens a results page.</summary>
    public ISearchFlow? Flow { get; set; }

    /// <summary>The name as typed. Typing clears a shown error and stops a search still waiting.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                NameError = null;
                Cancel();
            }
        }
    }

    /// <summary>Tapping a recent search selects it, which opens it; the selection is then cleared.</summary>
    public RecentSearch? SelectedRecent
    {
        get => null;
        set
        {
            if (value is not null)
            {
                OnPropertyChanged();
                OpenRecentCommand.Execute(value);
            }
        }
    }

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

    /// <summary>The error under the name, or null.</summary>
    public string? NameError
    {
        get => _nameError;
        private set
        {
            if (SetProperty(ref _nameError, value))
            {
                OnPropertyChanged(nameof(HasNameError));
            }
        }
    }

    /// <summary>Whether <see cref="NameError"/> is shown.</summary>
    public bool HasNameError => _nameError is not null;

    /// <summary>Whether a search waits for its first card: Find musicians reads "Searching…" and is off.</summary>
    public bool IsWaiting
    {
        get => _isWaiting;
        private set
        {
            if (SetProperty(ref _isWaiting, value))
            {
                OnPropertyChanged(nameof(GoButtonText));
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    /// <summary>Whether Find musicians can be pressed.</summary>
    public bool CanSearch => !_isWaiting;

    /// <summary>"Find musicians", or "Searching…" while it waits.</summary>
    public string GoButtonText => _isWaiting ? SearchingText : GoText;

    /// <summary>"No musicians found like Xyzzy. Try another name.", or an error, when a search opened no page.</summary>
    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    /// <summary>Whether <see cref="Message"/> is shown.</summary>
    public bool HasMessage => _message.Length > 0;

    /// <summary>Whether the message is about the connection, so "Fix connection" is offered.</summary>
    public bool CanFixConnection
    {
        get => _canFixConnection;
        private set => SetProperty(ref _canFixConnection, value);
    }

    /// <summary>Who the user may have meant, when the name was unclear.</summary>
    public ObservableCollection<Musician> Candidates { get; } = [];

    /// <summary>Whether there are candidates to show.</summary>
    public bool HasCandidates => Candidates.Count > 0;

    /// <summary>"Which Genesis do you mean?" or "Did you mean…".</summary>
    public string CandidatesTitle
    {
        get => _candidatesTitle;
        private set => SetProperty(ref _candidatesTitle, value);
    }

    /// <summary>The recent searches, newest first.</summary>
    public ObservableCollection<RecentSearch> Recent { get; } = [];

    /// <summary>Whether there are recent searches to show.</summary>
    public bool HasRecent => Recent.Count > 0;

    /// <summary>The search still waiting for its first card, if any; tests wait on it.</summary>
    public Task Searching { get; private set; } = Task.CompletedTask;

    /// <summary>Searches for the typed name.</summary>
    public IAsyncRelayCommand SearchCommand { get; }

    /// <summary>Opens a recent search again; a result from the last day is shown from the cache.</summary>
    public IAsyncRelayCommand<RecentSearch> OpenRecentCommand { get; }

    /// <summary>Forgets a recent search: the ✕ on its chip.</summary>
    public IAsyncRelayCommand<RecentSearch> RemoveRecentCommand { get; }

    /// <summary>Searches the candidate the user picked.</summary>
    public IAsyncRelayCommand<Musician> ChooseCandidateCommand { get; }

    /// <summary>Opens Connect.</summary>
    public IAsyncRelayCommand FixConnectionCommand { get; }

    /// <summary>Opens the favourites.</summary>
    public IAsyncRelayCommand SavedCommand { get; }

    /// <summary>Opens the settings.</summary>
    public IAsyncRelayCommand SettingsCommand { get; }

    /// <summary>
    /// Loads the recent searches. With no AI connected, goes to Connect (design §4.2).
    /// </summary>
    public async Task LoadAsync()
    {
        var connected = await _connections.GetWithSecretAsync();
        await LoadRecentAsync();

        if (connected is null)
        {
            await _navigator.GoToAsync(KofflardRoutes.Connect);
        }
    }

    /// <summary>Stops a search still waiting for its first card.</summary>
    public void Cancel()
    {
        _pending?.Cancel();
        _pending = null;
        IsWaiting = false;
    }

    private Task SearchAsync()
    {
        var name = Names.Clean(Name);
        if (name.Length == 0)
        {
            NameError = NameRequired;
            return Task.CompletedTask;
        }

        return ShowAsync(name, cached: false);
    }

    private Task OpenRecentAsync(RecentSearch? search)
        => search is null ? Task.CompletedTask : ShowAsync(search.Artist, cached: true);

    private Task ChooseAsync(Musician? candidate)
    {
        if (candidate is null)
        {
            return Task.CompletedTask;
        }

        Name = candidate.Name;
        return ShowAsync(candidate.Name, cached: false);
    }

    private async Task RemoveRecentAsync(RecentSearch? search)
    {
        if (search is null)
        {
            return;
        }

        await _discovery.ForgetSearchAsync(search.Artist);
        foreach (var gone in Recent.Where(recent => Names.Normalise(recent.Artist) == Names.Normalise(search.Artist)).ToList())
        {
            Recent.Remove(gone);
        }

        OnPropertyChanged(nameof(HasRecent));
    }

    private Task ShowAsync(string artist, bool cached)
    {
        ClearOutcome();
        if (Flow is { } flow)
        {
            return flow.ShowAsync(artist, cached);
        }

        Cancel();
        _pending = new CancellationTokenSource();
        Searching = WaitAsync(artist, cached, _pending.Token);
        return Searching;
    }

    // Waits for the first card, which opens the results page; anything else shows here.
    private async Task WaitAsync(string artist, bool cached, CancellationToken token)
    {
        IsWaiting = true;
        try
        {
            var search = await _launcher.StartAsync(artist, cached, token);
            if (token.IsCancellationRequested || search is null)
            {
                return;
            }

            switch (search.State)
            {
                case ResultsViewModel.CandidatesState when search.Candidates.Count == 0:
                    Message = search.CandidatesTitle;
                    break;
                case ResultsViewModel.CandidatesState:
                    foreach (var candidate in search.Candidates)
                    {
                        Candidates.Add(candidate);
                    }

                    CandidatesTitle = search.CandidatesTitle;
                    OnPropertyChanged(nameof(HasCandidates));
                    break;
                case ResultsViewModel.ErrorState:
                    Message = search.ErrorMessage;
                    CanFixConnection = search.CanFixConnection;
                    break;
                default:
                    Message = $"No musicians found like {artist}. Try another name.";
                    break;
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsWaiting = false;
                await LoadRecentAsync();
            }
        }
    }

    private void ClearOutcome()
    {
        Message = string.Empty;
        CanFixConnection = false;
        CandidatesTitle = string.Empty;
        Candidates.Clear();
        OnPropertyChanged(nameof(HasCandidates));
    }

    private async Task LoadRecentAsync()
    {
        Recent.Clear();
        foreach (var search in await _discovery.RecentAsync())
        {
            Recent.Add(search);
        }

        OnPropertyChanged(nameof(HasRecent));
    }

    private Task LeaveAsync(string route)
    {
        Cancel();
        return _navigator.GoToAsync(route);
    }
}
