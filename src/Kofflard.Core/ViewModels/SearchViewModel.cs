using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The search page, the Shell root: a name and the recent searches (design §3.1). How adventurous is
/// chosen on the results (.my/design/adventure-as-outcomes.md).
/// </summary>
public sealed class SearchViewModel : ParentViewModel
{
    /// <summary>The error when Go is pressed with nothing typed.</summary>
    public const string NameRequired = "Type a musician's name";

    private readonly DiscoveryService _discovery;
    private readonly AiConnections _connections;
    private readonly INavigator _navigator;

    private string _name = string.Empty;
    private string? _nameError;

    /// <summary>Creates the view model.</summary>
    public SearchViewModel(DiscoveryService discovery, AiConnections connections, INavigator navigator)
    {
        _discovery = discovery;
        _connections = connections;
        _navigator = navigator;

        SearchCommand = new TargetAsyncCommand(this, SearchAsync);
        OpenRecentCommand = new TargetAsyncCommand<RecentSearch>(this, OpenRecentAsync);
        SavedCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Saved));
        SettingsCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Settings));
    }

    /// <summary>The name as typed. Typing clears a shown error.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                NameError = null;
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

    /// <summary>The recent searches, newest first.</summary>
    public ObservableCollection<RecentSearch> Recent { get; } = [];

    /// <summary>Whether there are recent searches to show.</summary>
    public bool HasRecent => Recent.Count > 0;

    /// <summary>Searches for the typed name.</summary>
    public IAsyncRelayCommand SearchCommand { get; }

    /// <summary>Opens a recent search again; a result from the last day is shown from the cache.</summary>
    public IAsyncRelayCommand<RecentSearch> OpenRecentCommand { get; }

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

        Recent.Clear();
        foreach (var search in await _discovery.RecentAsync())
        {
            Recent.Add(search);
        }

        OnPropertyChanged(nameof(HasRecent));

        if (connected is null)
        {
            await _navigator.GoToAsync(KofflardRoutes.Connect);
        }
    }

    private async Task SearchAsync()
    {
        var name = Names.Clean(Name);
        if (name.Length == 0)
        {
            NameError = NameRequired;
            return;
        }

        await _navigator.GoToAsync(KofflardRoutes.Results, KofflardRoutes.ForSearch(name));
    }

    private Task OpenRecentAsync(RecentSearch? search)
        => search is null
            ? Task.CompletedTask
            : _navigator.GoToAsync(KofflardRoutes.Results, KofflardRoutes.ForSearch(search.Artist, cached: true));
}
