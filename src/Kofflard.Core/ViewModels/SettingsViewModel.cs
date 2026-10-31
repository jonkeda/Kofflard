using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// The settings (design §3.3). Each change is kept at once; there is no Save.
/// </summary>
public sealed class SettingsViewModel : ParentViewModel
{
    /// <summary>The clear-history confirmation's title.</summary>
    public const string ClearTitle = "Clear history?";

    /// <summary>The disconnect confirmation's title.</summary>
    public const string DisconnectTitle = "Disconnect the AI?";

    private readonly ISettingsStore _settings;
    private readonly AiConnections _connections;
    private readonly IHistoryStore _history;
    private readonly IDialogs _dialogs;
    private readonly INavigator _navigator;

    private AppSettings _current = AppSettings.Default;
    private string _connectionLine = string.Empty;
    private bool _loaded;

    /// <summary>Creates the view model.</summary>
    public SettingsViewModel(ISettingsStore settings, AiConnections connections, IHistoryStore history, IDialogs dialogs, INavigator navigator)
    {
        _settings = settings;
        _connections = connections;
        _history = history;
        _dialogs = dialogs;
        _navigator = navigator;

        ChangeAiCommand = new TargetAsyncCommand(this, () => _navigator.GoToAsync(KofflardRoutes.Connect));
        DisconnectCommand = new TargetAsyncCommand(this, DisconnectAsync);
        ClearHistoryCommand = new TargetAsyncCommand(this, ClearHistoryAsync);
    }

    /// <summary>"Azure OpenAI / Foundry · gpt-4.1-mini", or "No AI connected".</summary>
    public string ConnectionLine
    {
        get => _connectionLine;
        private set => SetProperty(ref _connectionLine, value);
    }

    /// <summary>The services, in Open with's order: YouTube, YouTube Music, Spotify.</summary>
    public IReadOnlyList<string> ServiceNames { get; } = [.. Enum.GetValues<MusicService>().Select(MusicLinks.DisplayName)];

    /// <summary>The Open with service's index in <see cref="ServiceNames"/>.</summary>
    public int OpenWithIndex
    {
        get => (int)_current.OpenWith;
        set
        {
            var service = (MusicService)Math.Clamp(value, 0, ServiceNames.Count - 1);
            if (service != _current.OpenWith)
            {
                _ = ChangeAsync(_current with { OpenWith = service }, nameof(OpenWithIndex));
            }
        }
    }

    /// <summary>Whether names are checked against MusicBrainz. Off by default.</summary>
    public bool CheckNames
    {
        get => _current.CheckNames;
        set
        {
            if (value != _current.CheckNames)
            {
                _ = ChangeAsync(_current with { CheckNames = value }, nameof(CheckNames));
            }
        }
    }

    /// <summary>The adventurousness level, 1-5.</summary>
    public int AdventureLevel
    {
        get => _current.Adventure;
        set
        {
            var level = new Adventure(value).Level;
            if (level != _current.Adventure)
            {
                _ = ChangeAsync(_current with { Adventure = level }, nameof(AdventureLevel), nameof(AdventureStep), nameof(AdventureName));
            }
        }
    }

    /// <summary><see cref="AdventureLevel"/> as the Stepper binds it.</summary>
    public double AdventureStep
    {
        get => AdventureLevel;
        set => AdventureLevel = (int)Math.Round(value);
    }

    /// <summary>"Safe" to "Wild".</summary>
    public string AdventureName => new Adventure(_current.Adventure).Name;

    /// <summary>Opens Connect.</summary>
    public IAsyncRelayCommand ChangeAiCommand { get; }

    /// <summary>Forgets the AI connection and its secret, after asking.</summary>
    public IAsyncRelayCommand DisconnectCommand { get; }

    /// <summary>Forgets recent searches, what was shown and the cache, after asking.</summary>
    public IAsyncRelayCommand ClearHistoryCommand { get; }

    /// <summary>Loads the settings.</summary>
    public async Task LoadAsync()
    {
        _current = await _settings.LoadAsync();
        _loaded = true;
        ConnectionLine = await _connections.GetWithSecretAsync() is { } connected
            ? connected.Connection.Describe()
            : "No AI connected";
        OnPropertyChanged(string.Empty);
    }

    private async Task ChangeAsync(AppSettings changed, params string[] properties)
    {
        _current = changed;
        foreach (var property in properties)
        {
            OnPropertyChanged(property);
        }

        if (_loaded)
        {
            await _settings.SaveAsync(changed);
        }
    }

    private async Task DisconnectAsync()
    {
        if (!await _dialogs.ConfirmAsync(DisconnectTitle, "Kofflard forgets the AI and its key. You can connect again at any time.", "Disconnect", "Cancel"))
        {
            return;
        }

        await _connections.DisconnectAsync();
        await LoadAsync();
    }

    private async Task ClearHistoryAsync()
    {
        if (!await _dialogs.ConfirmAsync(ClearTitle, "Recent searches and what was shown are forgotten. Saved musicians stay.", "Clear", "Cancel"))
        {
            return;
        }

        await _history.ClearAsync();
        await _dialogs.AlertAsync("History cleared", "Recent searches and what was shown are forgotten.");
    }
}
