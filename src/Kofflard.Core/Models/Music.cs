namespace Kofflard.Core.Models;

/// <summary>Where a suggestion can be opened (design §8).</summary>
public enum MusicService
{
    /// <summary>YouTube: works without any subscription. The default.</summary>
    YouTube,

    /// <summary>YouTube Music.</summary>
    YouTubeMusic,

    /// <summary>Spotify.</summary>
    Spotify,
}

/// <summary>A link to one musician in one service: the app's own URI and the web URL.</summary>
/// <param name="Service">The service.</param>
/// <param name="AppUri">The URI the service's app handles, or null when the web URL is the only link.</param>
/// <param name="WebUrl">The https URL, which always works.</param>
public sealed record MusicLink(MusicService Service, Uri? AppUri, Uri WebUrl);

/// <summary>What opening a link did.</summary>
public enum LaunchOutcome
{
    /// <summary>The service's app opened.</summary>
    OpenedApp,

    /// <summary>The browser opened (or an App Link / Universal Link took it from there).</summary>
    OpenedBrowser,

    /// <summary>Nothing could open it.</summary>
    Failed,
}
