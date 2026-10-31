using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Services;
using Kofflard.Infrastructure;
using Kofflard.Infrastructure.Ai;

namespace Kofflard.App.Services;

/// <summary><see cref="INavigator"/> over Shell.</summary>
public sealed class ShellNavigator : INavigator
{
    /// <inheritdoc />
    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
        => parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, new Dictionary<string, object>(parameters));

    /// <inheritdoc />
    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}

/// <summary><see cref="IDialogs"/> over the current page's alerts.</summary>
public sealed class ShellDialogs : IDialogs
{
    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
        => CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

    /// <inheritdoc />
    public Task AlertAsync(string title, string message, string ok = "OK")
        => CurrentPage.DisplayAlertAsync(title, message, ok);

    private static Page CurrentPage => Shell.Current.CurrentPage ?? Shell.Current;
}

/// <summary>
/// Opens music links (design §8.2): the browser on Windows; on Android and iOS the service's app when it
/// is installed, else the browser, whose https link is often an App Link / Universal Link into the app.
/// </summary>
/// <remarks>
/// <c>CanOpenAsync</c> needs the schemes declared: <c>&lt;queries&gt;</c> in the Android manifest,
/// <c>LSApplicationQueriesSchemes</c> in the iOS Info.plist.
/// </remarks>
public sealed class MauiMusicLauncher : IMusicLauncher
{
    /// <inheritdoc />
    public async Task<LaunchOutcome> OpenAsync(MusicLink link, CancellationToken cancellationToken = default)
    {
        try
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                return await Launcher.Default.OpenAsync(link.WebUrl) ? LaunchOutcome.OpenedBrowser : LaunchOutcome.Failed;
            }

            if (link.AppUri is { } app && await Launcher.Default.CanOpenAsync(app) && await Launcher.Default.TryOpenAsync(app))
            {
                return LaunchOutcome.OpenedApp;
            }

            return await Browser.Default.OpenAsync(link.WebUrl, BrowserLaunchMode.External)
                ? LaunchOutcome.OpenedBrowser
                : LaunchOutcome.Failed;
        }
        catch (Exception failure) when (failure is FeatureNotSupportedException or InvalidOperationException or UriFormatException)
        {
            return LaunchOutcome.Failed;
        }
    }
}

/// <summary>Opens web pages in the system browser.</summary>
public sealed class MauiWebBrowser : IWebBrowser
{
    /// <inheritdoc />
    public Task OpenAsync(Uri url) => Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
}

/// <summary>Secrets in the platform's secure storage: Keychain, Android Keystore, DPAPI on Windows (design §6.2).</summary>
public sealed class MauiSecretStore : ISecretStore
{
    /// <inheritdoc />
    public Task<string?> GetAsync(string name) => SecureStorage.Default.GetAsync(name);

    /// <inheritdoc />
    public Task SetAsync(string name, string value) => SecureStorage.Default.SetAsync(name, value);

    /// <inheritdoc />
    public Task RemoveAsync(string name)
    {
        SecureStorage.Default.Remove(name);
        return Task.CompletedTask;
    }
}

/// <summary>
/// OAuth on a phone (design §6.3): <c>WebAuthenticator</c> (Custom Tabs, ASWebAuthenticationSession)
/// with the redirect <c>kofflard://oauth/callback</c>.
/// </summary>
public sealed class MauiOAuthBroker : IOAuthBroker
{
    /// <summary>The redirect the app registers for.</summary>
    public static readonly Uri Callback = new("kofflard://oauth/callback");

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>?> AuthenticateAsync(Func<Uri, Uri> authorizeUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = authorizeUrl(Callback),
                CallbackUrl = Callback,
                PrefersEphemeralWebBrowserSession = true,
            });
            return result.Properties;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}

/// <summary>The device's services for <see cref="KofflardServiceCollectionExtensions.AddKofflardServices"/>.</summary>
public static class KofflardDevice
{
    /// <summary>Secure storage, the launcher, the browser, and the OAuth broker this platform supports.</summary>
    public static KofflardPlatform Create() => new(
        _ => new MauiSecretStore(),
        _ => new MauiMusicLauncher(),
        _ => new MauiWebBrowser(),
        services => DeviceInfo.Current.Platform == DevicePlatform.WinUI
            ? new LoopbackOAuthBroker(services.GetRequiredService<IWebBrowser>())
            : new MauiOAuthBroker());
}
