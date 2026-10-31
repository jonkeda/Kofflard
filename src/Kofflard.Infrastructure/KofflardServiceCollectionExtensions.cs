using Kofflard.Core.Ai;
using Kofflard.Core.Services;
using Kofflard.Core.ViewModels;
using Kofflard.Infrastructure.Ai;
using Kofflard.Infrastructure.Data;
using Kofflard.Infrastructure.Music;
using Kofflard.Infrastructure.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Kofflard.Infrastructure;

/// <summary>
/// What the platform gives the app: where secrets are kept, how links open, how OAuth runs.
/// The app passes the device's; a headless test passes its own.
/// </summary>
/// <param name="SecretStore">SecureStorage on a device.</param>
/// <param name="Launcher">Opens music links: the app on Android and iOS, the browser on Windows.</param>
/// <param name="Browser">Opens web pages.</param>
/// <param name="OAuthBroker">Runs a sign-in in the browser: WebAuthenticator on a phone, a loopback listener on Windows.</param>
public sealed record KofflardPlatform(
    Func<IServiceProvider, ISecretStore> SecretStore,
    Func<IServiceProvider, IMusicLauncher> Launcher,
    Func<IServiceProvider, IWebBrowser> Browser,
    Func<IServiceProvider, IOAuthBroker> OAuthBroker);

/// <summary>Kofflard's composition root, shared by the app, the headless head and the integration tests.</summary>
public static class KofflardServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, the AI, the verifier, the platform's services and the view models.
    /// </summary>
    /// <remarks>
    /// Under test (<see cref="LaunchSettings.AiBaseUrl"/> set) the app starts connected to the test's fake
    /// AI, with its key in memory rather than in the device's secure storage; with
    /// <see cref="LaunchSettings.LaunchLog"/> set, links are written to that file instead of opened.
    /// </remarks>
    /// <param name="services">Where to register them.</param>
    /// <param name="settings">The launch settings, read on first use.</param>
    /// <param name="platform">The platform's services.</param>
    public static IServiceCollection AddKofflardServices(
        this IServiceCollection services,
        Func<IServiceProvider, LaunchSettings> settings,
        KofflardPlatform platform)
    {
        services.AddSingleton(settings);
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<LaunchSettings>().FixedNow is { } now
            ? new FixedTimeProvider(now)
            : TimeProvider.System);

        services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

        services.AddSingleton(provider => new KofflardDatabase(provider.GetRequiredService<LaunchSettings>().DatabasePath));
        services.AddSingleton<ISettingsStore>(provider =>
        {
            var launch = provider.GetRequiredService<LaunchSettings>();
            return new SqliteSettingsStore(
                provider.GetRequiredService<KofflardDatabase>(),
                launch.IsPreConnected ? new AiConnection(launch.AiProvider, launch.AiBaseUrl, launch.AiModel) : null);
        });
        services.AddSingleton<IHistoryStore, SqliteHistoryStore>();
        services.AddSingleton<ISavedStore, SqliteSavedStore>();
        services.AddSingleton<SqliteVerificationCache>();

        services.AddSingleton(provider =>
        {
            var launch = provider.GetRequiredService<LaunchSettings>();
            if (!launch.IsPreConnected)
            {
                return platform.SecretStore(provider);
            }

            var memory = new InMemorySecretStore();
            if (launch.AiKey is { } key)
            {
                memory.SetAsync(AiConnections.SecretName, key).GetAwaiter().GetResult();
            }

            return (ISecretStore)memory;
        });

        services.AddSingleton(provider => new LinkRecording(
            provider.GetRequiredService<LaunchSettings>().LaunchLog is { } log ? new RecordingLinks(log) : null));
        services.AddSingleton(provider => provider.GetRequiredService<LinkRecording>().Links is { } recording
            ? recording
            : platform.Launcher(provider));
        services.AddSingleton(provider => provider.GetRequiredService<LinkRecording>().Links is { } recording
            ? (IWebBrowser)recording
            : platform.Browser(provider));
        services.AddSingleton(platform.OAuthBroker);

        services.AddSingleton(provider => new ChatClientFactory(
            provider.GetRequiredService<HttpClient>(),
            provider.GetRequiredService<LaunchSettings>().AiTimeout));
        services.AddSingleton<IDiscoveryEngine, DiscoveryEngine>();
        services.AddSingleton<IOpenRouterSignIn>(provider => new OpenRouterSignIn(
            provider.GetRequiredService<IOAuthBroker>(),
            provider.GetRequiredService<HttpClient>(),
            new Uri(provider.GetRequiredService<LaunchSettings>().OpenRouterBaseUrl)));
        services.AddSingleton<IArtistVerifier>(provider => new MusicBrainzVerifier(
            provider.GetRequiredService<HttpClient>(),
            new Uri(provider.GetRequiredService<LaunchSettings>().MusicBrainzBaseUrl),
            provider.GetRequiredService<SqliteVerificationCache>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddSingleton<AiConnections>();
        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<SuggestionCards>();

        services.AddSingleton<SearchViewModel>();
        services.AddTransient<ResultsViewModel>();
        services.AddSingleton<Func<ResultsViewModel>>(provider => provider.GetRequiredService<ResultsViewModel>);
        services.AddSingleton<SearchLauncher>();
        services.AddSingleton<RunningSearch>();
        services.AddSingleton<DiscoverViewModel>();
        services.AddTransient<ConnectViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SavedViewModel>();
        return services;
    }

    // The launch log's recorder, when the launch asked for one.
    private sealed record LinkRecording(RecordingLinks? Links);
}
