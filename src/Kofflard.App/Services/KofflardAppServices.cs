using Kofflard.App.Pages;
using Kofflard.Core.Services;
using Kofflard.Infrastructure;

namespace Kofflard.App.Services;

/// <summary>
/// The app's services, pages and Shell: what <see cref="MauiProgram"/> registers on a device, and what a
/// test registers when it renders the app headless (Bruinet's MauiHeadless head). Kept apart from the app
/// builder because that one needs a platform.
/// </summary>
public static class KofflardAppServices
{
    /// <summary>Registers Kofflard's services, the Shell's navigator and dialogs, the Shell and its pages.</summary>
    /// <param name="services">Where to register them.</param>
    /// <param name="settings">The launch settings: read from the launch on a device, given by a test headless.</param>
    /// <param name="platform">The device's services, or a test's.</param>
    public static IServiceCollection AddKofflardApp(
        this IServiceCollection services,
        Func<IServiceProvider, LaunchSettings> settings,
        KofflardPlatform platform)
    {
        services.AddKofflardServices(settings, platform);

        services.AddSingleton<INavigator, ShellNavigator>();
        services.AddSingleton<IDialogs, ShellDialogs>();

        services.AddSingleton<AppShell>();
        services.AddSingleton<DiscoverPage>();
        services.AddTransient<ConnectPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<SavedPage>();
        return services;
    }
}
