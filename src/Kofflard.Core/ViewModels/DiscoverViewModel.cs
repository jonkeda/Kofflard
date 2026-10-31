using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The phone's one page (.my/design/single-page-phone.md): the results fill it, the search sits in the
/// dock at the bottom, and a card's search replaces the list, with a trail back. The Safe, Curious and Wild
/// toggles set both what a search asks for and which sections show.
/// </summary>
public sealed class DiscoverViewModel : ParentViewModel, ISearchFlow
{
    private readonly Func<ResultsViewModel> _makeResults;
    private readonly ISettingsStore _settings;
    private readonly Stack<string> _trail = new();
    private readonly Explorer _explorer;

    private ResultsViewModel? _results;
    private bool _hasResults;
    private LaneSet _lanes = LaneSet.Default;
    private bool _isEditing;

    /// <summary>Creates the view model.</summary>
    public DiscoverViewModel(SearchViewModel search, Func<ResultsViewModel> makeResults, ISettingsStore settings)
    {
        Search = search;
        Search.Flow = this;
        _makeResults = makeResults;
        _settings = settings;
        _explorer = new Explorer(this);

        Toggles = [.. Enum.GetValues<Lane>().Select(lane => new LaneToggleViewModel(lane, this))];
        BackCommand = new TargetAsyncCommand(this, async () => await BackAsync());
    }

    /// <summary>The name, its error, and Recent.</summary>
    public SearchViewModel Search { get; }

    /// <summary>
    /// The results, null before the first search. One for the page, loaded again for each search: the list
    /// stays bound to it, so its state view never changes under it.
    /// </summary>
    public ResultsViewModel? Results
    {
        get => _results;
        private set => SetProperty(ref _results, value);
    }

    /// <summary>Whether a search is shown; false before the first one and after going back to Recent.</summary>
    public bool HasResults
    {
        get => _hasResults;
        private set
        {
            if (SetProperty(ref _hasResults, value))
            {
                OnPropertyChanged(nameof(ShowsRecent));
            }
        }
    }

    /// <summary>
    /// Whether Recent shows in the list's place: before the first search, and while the name field is focused
    /// and empty.
    /// </summary>
    public bool ShowsRecent => !_hasResults || (_isEditing && Search.Name.Length == 0);

    /// <summary>Whether the name field has the focus; the page sets it.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(ShowsRecent));
            }
        }
    }

    /// <summary>Safe, Curious and Wild.</summary>
    public IReadOnlyList<LaneToggleViewModel> Toggles { get; }

    /// <summary>The lanes that are on.</summary>
    public LaneSet Lanes => _lanes;

    /// <summary>The search this one came from: "Nick Drake". Empty when there is none.</summary>
    public string TrailText => _trail.TryPeek(out var artist) ? artist : string.Empty;

    /// <summary>Whether the trail shows.</summary>
    public bool HasTrail => _trail.Count > 0;

    /// <summary>Goes back one search: the trail, then Recent.</summary>
    public IAsyncRelayCommand BackCommand { get; }

    /// <summary>Reads the toggles' setting and the recent searches; with no AI connected, goes to Connect.</summary>
    public async Task LoadAsync()
    {
        _lanes = (await _settings.LoadAsync()).Lanes;
        RefreshToggles();
        await Search.LoadAsync();
    }

    /// <inheritdoc />
    /// <remarks>A search from the dock or Recent starts a new trail.</remarks>
    public Task ShowAsync(string artist, bool cached)
    {
        _trail.Clear();
        OnTrailChanged();
        return LoadAsync(artist, cached);
    }

    /// <summary>A card's search: the search shown, if it has cards, goes on the trail.</summary>
    public Task ExploreAsync(string artist)
    {
        if (_hasResults && _results is { HasContent: true } shown)
        {
            _trail.Push(shown.Artist);
            OnTrailChanged();
        }

        return LoadAsync(artist, cached: false);
    }

    /// <summary>
    /// Goes back one search: the last on the trail, from the cache, or Recent after the first search.
    /// False when there is nothing to go back to, so the system back leaves the app.
    /// </summary>
    public async Task<bool> BackAsync()
    {
        if (_trail.TryPop(out var artist))
        {
            OnTrailChanged();
            await LoadAsync(artist, cached: true);
            return true;
        }

        if (_hasResults)
        {
            _results?.Stop();
            HasResults = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Turns <paramref name="lane"/> on or off. The last lane on can't go off. Off hides its section at once;
    /// on shows it, and asks for that lane alone when it has no cards yet.
    /// </summary>
    public async Task ToggleAsync(Lane lane)
    {
        var on = !_lanes.Contains(lane);
        if (!on && _lanes.Count == 1)
        {
            return;
        }

        _lanes = _lanes.With(lane, on);
        RefreshToggles();
        await _settings.SaveAsync((await _settings.LoadAsync()) with { Lanes = _lanes });

        if (_hasResults && _results is { } results)
        {
            results.Shown = _lanes;
            results.Mix = LaneMix.For(_lanes);
            if (on)
            {
                await results.AskForAsync(lane);
            }
        }
    }

    // The toggles are read again first: the setting is theirs, but Settings or a test may have changed it.
    private async Task LoadAsync(string artist, bool cached)
    {
        _lanes = (await _settings.LoadAsync()).Lanes;
        RefreshToggles();
        var results = _results ?? _makeResults();
        results.Stop();
        results.Flow = _explorer;
        results.Shown = _lanes;
        results.Mix = LaneMix.For(_lanes);
        Results = results;
        HasResults = true;
        Search.Name = artist;
        await results.LoadAsync(artist, cached);
    }

    private void RefreshToggles()
    {
        foreach (var toggle in Toggles)
        {
            toggle.Refresh();
        }

        OnPropertyChanged(nameof(Lanes));
    }

    private void OnTrailChanged()
    {
        OnPropertyChanged(nameof(TrailText));
        OnPropertyChanged(nameof(HasTrail));
    }

    // Where a card's "More →" goes: on the trail, then in place.
    private sealed class Explorer(DiscoverViewModel owner) : ISearchFlow
    {
        public Task ShowAsync(string artist, bool cached) => owner.ExploreAsync(artist);
    }
}

/// <summary>One of the phone's Safe, Curious and Wild toggles: a button that shows whether it is on.</summary>
public sealed class LaneToggleViewModel : ParentViewModel
{
    private readonly DiscoverViewModel _owner;

    internal LaneToggleViewModel(Lane lane, DiscoverViewModel owner)
    {
        Lane = lane;
        _owner = owner;
        ToggleCommand = new TargetAsyncCommand(this, () => _owner.ToggleAsync(Lane));
    }

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>"Safe", "Curious" or "Wild".</summary>
    public string Name => Lane.ToString();

    /// <summary>Whether the lane is asked for and shown.</summary>
    public bool IsOn => _owner.Lanes.Contains(Lane);

    /// <summary>"Wild, shown" or "Wild, hidden", for screen readers and tests.</summary>
    public string Description => $"{Name}, {(IsOn ? "shown" : "hidden")}";

    /// <summary>Turns the lane on or off.</summary>
    public IAsyncRelayCommand ToggleCommand { get; }

    internal void Refresh()
    {
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Description));
    }
}
