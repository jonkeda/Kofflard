using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One card: a suggestion on the results page, or a saved musician (design §3.1).
/// </summary>
/// <remarks>
/// A tap opens the service chosen in Open with; a long press offers the other two (design §8.3,
/// .my/design/results-list-changes.md). Explore searches again from this musician ("chain").
/// </remarks>
public sealed class SuggestionViewModel : ParentViewModel
{
    private readonly IMusicLauncher _launcher;
    private readonly IHistoryStore _history;
    private readonly ISavedStore _saved;
    private readonly TimeProvider _time;
    private readonly Func<SuggestionViewModel, Task> _explore;
    private readonly MusicService[] _services;

    private Verification _verification = Verification.Unverified;
    private bool _isSaved;
    private string _openedText = string.Empty;
    private int _opens;
    private bool _showsOtherServices;

    /// <summary>Creates a card.</summary>
    /// <param name="suggestion">What the card shows.</param>
    /// <param name="openWith">The service of the main button.</param>
    /// <param name="isSaved">Whether the musician is saved already.</param>
    /// <param name="launcher">Opens the links.</param>
    /// <param name="history">Remembers what was opened.</param>
    /// <param name="saved">Saves and unsaves.</param>
    /// <param name="time">The clock.</param>
    /// <param name="explore">Searches from this musician.</param>
    public SuggestionViewModel(
        Suggestion suggestion,
        MusicService openWith,
        bool isSaved,
        IMusicLauncher launcher,
        IHistoryStore history,
        ISavedStore saved,
        TimeProvider time,
        Func<SuggestionViewModel, Task> explore)
    {
        Suggestion = suggestion;
        _isSaved = isSaved;
        _launcher = launcher;
        _history = history;
        _saved = saved;
        _time = time;
        _explore = explore;
        _services = [openWith, .. Enum.GetValues<MusicService>().Where(service => service != openWith)];

        OpenCommand = new TargetAsyncCommand(this, () => OpenAsync(_services[0]));
        OpenSecondCommand = new TargetAsyncCommand(this, () => OpenAsync(_services[1]));
        OpenThirdCommand = new TargetAsyncCommand(this, () => OpenAsync(_services[2]));
        ShowOtherServicesCommand = new RelayCommand(() => ShowsOtherServices = !ShowsOtherServices);
        SaveCommand = new TargetAsyncCommand(this, ToggleSavedAsync);
        ExploreCommand = new TargetAsyncCommand(this, () => _explore(this));
    }

    /// <summary>How long "Opened in YouTube" stays on the card.</summary>
    public static readonly TimeSpan OpenedShownFor = TimeSpan.FromSeconds(3);

    /// <summary>The suggestion.</summary>
    public Suggestion Suggestion { get; }

    /// <summary>The musician's name.</summary>
    public string Name => Suggestion.Name;

    /// <summary>"Close", "Stretch" or "Leap".</summary>
    public string LaneName => Suggestion.Lane.ToString();

    /// <summary>The lane.</summary>
    public Lane Lane => Suggestion.Lane;

    /// <summary>"VB": the desktop's photo, until there are photos.</summary>
    public string Initials => ArtistMap.Initials(Name);

    /// <summary>Why it fits; for a leap, "Bridge: …".</summary>
    public string Why => Suggestion.Bridge is { } bridge ? $"Bridge: {bridge}" : Suggestion.Why;

    /// <summary>"folk · 1970s · UK".</summary>
    public string TagsText => string.Join(" · ", Suggestion.Tags);

    /// <summary>"Known for: Just Another Diamond Day", or empty.</summary>
    public string KnownForText => Suggestion.KnownFor is { } title ? $"Known for: {title}" : string.Empty;

    /// <summary>Whether <see cref="KnownForText"/> is shown.</summary>
    public bool HasKnownFor => Suggestion.KnownFor is not null;

    /// <summary>"Open Camel in YouTube": what a tap on the card does, for screen readers.</summary>
    public string OpenDescription => $"Open {Name} in {MusicLinks.DisplayName(_services[0])}";

    /// <summary>The second service's name.</summary>
    public string SecondText => MusicLinks.DisplayName(_services[1]);

    /// <summary>The third service's name.</summary>
    public string ThirdText => MusicLinks.DisplayName(_services[2]);

    /// <summary>The verification outcome.</summary>
    public Verification Verification
    {
        get => _verification;
        set
        {
            if (SetProperty(ref _verification, value))
            {
                OnPropertyChanged(nameof(IsVerified));
                OnPropertyChanged(nameof(IsNotFound));
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    /// <summary>Whether MusicBrainz confirmed the name.</summary>
    public bool IsVerified => _verification.State == VerificationState.Verified;

    /// <summary>Whether MusicBrainz did not know the name.</summary>
    public bool IsNotFound => _verification.State == VerificationState.NotFound;

    /// <summary>A card MusicBrainz did not know is dimmed.</summary>
    public double Opacity => IsNotFound ? 0.5 : 1.0;

    /// <summary>Whether the musician is saved.</summary>
    public bool IsSaved
    {
        get => _isSaved;
        private set
        {
            if (SetProperty(ref _isSaved, value))
            {
                OnPropertyChanged(nameof(StarText));
                OnPropertyChanged(nameof(StarDescription));
            }
        }
    }

    /// <summary>"★" in favourites, else "☆".</summary>
    public string StarText => _isSaved ? "★" : "☆";

    /// <summary>"Add Camel to favourites" or "Remove Camel from favourites", for screen readers.</summary>
    public string StarDescription => _isSaved ? $"Remove {Name} from favourites" : $"Add {Name} to favourites";

    /// <summary>Whether the long press's "Open in" row is shown.</summary>
    public bool ShowsOtherServices
    {
        get => _showsOtherServices;
        private set => SetProperty(ref _showsOtherServices, value);
    }

    /// <summary>The link the main button opens.</summary>
    public MusicLink Link(MusicService service)
        => MusicLinks.For(service, Name, Suggestion.KnownFor, _verification.SpotifyArtistId);

    /// <summary>Opens the musician in the Open with service: a tap on the card.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>Opens the musician in the second service, from the long press's row.</summary>
    public IAsyncRelayCommand OpenSecondCommand { get; }

    /// <summary>Opens the musician in the third service, from the long press's row.</summary>
    public IAsyncRelayCommand OpenThirdCommand { get; }

    /// <summary>Shows or hides "Open in" with the other two services: a long press, or a right-click on Windows.</summary>
    public IRelayCommand ShowOtherServicesCommand { get; }

    /// <summary>Adds the musician to favourites, or removes them.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Searches from this musician.</summary>
    public IAsyncRelayCommand ExploreCommand { get; }

    /// <summary>
    /// "Opened in YouTube" for a moment after an open, or "Couldn't open YouTube": the hand-off can be slow,
    /// and on Windows the browser opens behind the app.
    /// </summary>
    public string OpenedText
    {
        get => _openedText;
        private set
        {
            if (SetProperty(ref _openedText, value))
            {
                OnPropertyChanged(nameof(HasOpenedText));
            }
        }
    }

    /// <summary>Whether <see cref="OpenedText"/> is shown.</summary>
    public bool HasOpenedText => _openedText.Length > 0;

    /// <summary>Opens the musician in <paramref name="service"/>, and remembers it was opened.</summary>
    public async Task<LaunchOutcome> OpenAsync(MusicService service)
    {
        ShowsOtherServices = false;
        var outcome = await _launcher.OpenAsync(Link(service));
        await _history.MarkOpenedAsync(Name, _time.GetUtcNow());

        var name = MusicLinks.DisplayName(service);
        OpenedText = outcome == LaunchOutcome.Failed ? $"Couldn't open {name}" : $"Opened in {name}";
        var open = ++_opens;
        _ = ClearOpenedAsync(open);
        return outcome;
    }

    // Clears the line, unless another open has replaced it since.
    private async Task ClearOpenedAsync(int open)
    {
        await Task.Delay(OpenedShownFor, _time);
        if (open == _opens)
        {
            OpenedText = string.Empty;
        }
    }

    private async Task ToggleSavedAsync()
    {
        if (IsSaved)
        {
            await _saved.RemoveAsync(Name);
            IsSaved = false;
            return;
        }

        await _saved.SaveAsync(new SavedMusician(
            Name,
            Why,
            Suggestion.Tags,
            Suggestion.KnownFor,
            _verification.SpotifyArtistId,
            _time.GetUtcNow()));
        IsSaved = true;
    }
}
