using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Ai;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// Connecting an AI: pick a provider, then sign in or paste a key, test, done (design §6).
/// </summary>
public sealed class ConnectViewModel : ParentViewModel
{
    /// <summary>The status after a good test.</summary>
    public const string Working = "✓ The connection works";

    private readonly AiConnections _connections;
    private readonly IDiscoveryEngine _engine;
    private readonly IOpenRouterSignIn _openRouter;
    private readonly IWebBrowser _browser;
    private readonly INavigator _navigator;

    private int _providerIndex;
    private string _endpoint = string.Empty;
    private string _model = AiProviders.Azure.DefaultModel;
    private string _apiKey = string.Empty;
    private string _status = string.Empty;
    private bool _statusIsError;

    /// <summary>Creates the view model, on the first provider: Azure.</summary>
    public ConnectViewModel(
        AiConnections connections,
        IDiscoveryEngine engine,
        IOpenRouterSignIn openRouter,
        IWebBrowser browser,
        INavigator navigator)
    {
        _connections = connections;
        _engine = engine;
        _openRouter = openRouter;
        _browser = browser;
        _navigator = navigator;

        TestCommand = new TargetAsyncCommand(this, () => TestAsync());
        DoneCommand = new TargetAsyncCommand(this, DoneAsync);
        SignInCommand = new TargetAsyncCommand(this, SignInAsync, () => Provider.CanSignIn);
        GetKeyCommand = new TargetAsyncCommand(this, GetKeyAsync, () => Provider.KeyPage is not null);
    }

    /// <summary>The providers' names, Azure first.</summary>
    public IReadOnlyList<string> ProviderNames { get; } = [.. AiProviders.All.Select(provider => provider.Name)];

    /// <summary>The chosen provider's index. Choosing one resets the model to its default.</summary>
    public int ProviderIndex
    {
        get => _providerIndex;
        set
        {
            var index = Math.Clamp(value, 0, AiProviders.All.Count - 1);
            if (SetProperty(ref _providerIndex, index))
            {
                Model = Provider.DefaultModel;
                ClearStatus();
                OnProviderChanged();
            }
        }
    }

    /// <summary>The chosen provider.</summary>
    public AiProvider Provider => AiProviders.All[_providerIndex];

    /// <summary>Whether an endpoint is asked for (Azure, a local server).</summary>
    public bool NeedsEndpoint => Provider.NeedsEndpoint;

    /// <summary>"https://&lt;resource&gt;.openai.azure.com".</summary>
    public string EndpointHint => Provider.EndpointHint ?? string.Empty;

    /// <summary>Whether a key is asked for.</summary>
    public bool NeedsKey => Provider.NeedsSecret;

    /// <summary>Whether Sign in is offered (OpenRouter).</summary>
    public bool CanSignIn => Provider.CanSignIn;

    /// <summary>"Model" or, for Azure, "Deployment".</summary>
    public string ModelLabel => Provider.Id == AiProviders.Azure.Id ? "Deployment" : "Model";

    /// <summary>The endpoint as typed.</summary>
    public string Endpoint
    {
        get => _endpoint;
        set
        {
            if (SetProperty(ref _endpoint, value ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    /// <summary>The model or deployment as typed.</summary>
    public string Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    /// <summary>The key as typed or pasted. Never shown again once saved.</summary>
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    /// <summary>"✓ The connection works", or what is wrong.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>Whether <see cref="Status"/> is shown.</summary>
    public bool HasStatus => _status.Length > 0;

    /// <summary>Whether <see cref="Status"/> is a problem.</summary>
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    /// <summary>Makes one tiny request and says whether it worked.</summary>
    public IAsyncRelayCommand TestCommand { get; }

    /// <summary>Tests, keeps the connection, and goes to Search.</summary>
    public IAsyncRelayCommand DoneCommand { get; }

    /// <summary>Signs in to OpenRouter, which hands over a key.</summary>
    public IAsyncRelayCommand SignInCommand { get; }

    /// <summary>Opens the provider's key page in the browser.</summary>
    public IAsyncRelayCommand GetKeyCommand { get; }

    /// <summary>Starts from the current connection, if there is one; the key is not shown again.</summary>
    public async Task LoadAsync()
    {
        if (await _connections.GetAsync() is not { } current)
        {
            return;
        }

        var index = AiProviders.All.ToList().FindIndex(provider => provider.Id == current.ProviderId);
        _providerIndex = Math.Max(0, index);
        _endpoint = current.Endpoint ?? string.Empty;
        _model = current.Model;
        _apiKey = string.Empty;
        OnPropertyChanged(string.Empty);
        OnProviderChanged();
    }

    private AiConnection Connection()
        => new(Provider.Id, NeedsEndpoint ? Endpoint.Trim() : null, Model.Trim());

    // A connection the form can describe, or the problem in words.
    private string? Problem()
    {
        if (NeedsEndpoint && !Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out _))
        {
            return $"Enter the endpoint, like {EndpointHint}";
        }

        if (Model.Trim().Length == 0)
        {
            return $"Enter the {ModelLabel.ToLowerInvariant()}";
        }

        if (NeedsKey && ApiKey.Trim().Length == 0)
        {
            return CanSignIn ? "Sign in, or paste an API key" : "Paste an API key";
        }

        return null;
    }

    private async Task<bool> TestAsync()
    {
        if (Problem() is { } problem)
        {
            ShowStatus(problem, isError: true);
            return false;
        }

        ShowStatus("Testing…", isError: false);
        try
        {
            await _engine.TestAsync(Connection(), NeedsKey ? ApiKey.Trim() : null);
            ShowStatus(Working, isError: false);
            return true;
        }
        catch (AiException failure)
        {
            ShowStatus(failure.Message, isError: true);
            return false;
        }
    }

    private async Task DoneAsync()
    {
        if (!await TestAsync())
        {
            return;
        }

        await _connections.SaveAsync(Connection(), NeedsKey ? ApiKey.Trim() : null);
        ApiKey = string.Empty;
        await _navigator.GoToAsync("//" + KofflardRoutes.Search);
    }

    private async Task SignInAsync()
    {
        ShowStatus("Signing in…", isError: false);
        try
        {
            if (await _openRouter.SignInAsync() is not { Length: > 0 } key)
            {
                ShowStatus("Sign-in was cancelled", isError: true);
                return;
            }

            ApiKey = key;
            await DoneAsync();
        }
        catch (AiException failure)
        {
            ShowStatus(failure.Message, isError: true);
        }
    }

    private Task GetKeyAsync()
        => Provider.KeyPage is { } page ? _browser.OpenAsync(new Uri(page)) : Task.CompletedTask;

    private void OnProviderChanged()
    {
        OnPropertyChanged(nameof(Provider));
        OnPropertyChanged(nameof(NeedsEndpoint));
        OnPropertyChanged(nameof(EndpointHint));
        OnPropertyChanged(nameof(NeedsKey));
        OnPropertyChanged(nameof(CanSignIn));
        OnPropertyChanged(nameof(ModelLabel));
        SignInCommand.NotifyCanExecuteChanged();
        GetKeyCommand.NotifyCanExecuteChanged();
    }

    private void ShowStatus(string text, bool isError)
    {
        StatusIsError = isError;
        Status = text;
    }

    private void ClearStatus() => ShowStatus(string.Empty, isError: false);
}
