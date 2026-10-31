using Kofflard.Core.Ai;
using Kofflard.Core.Models;

namespace Kofflard.Core.Services;

/// <summary>
/// The user's settings (design §3.3). Everything but the AI's secret, which is in <see cref="ISecretStore"/>.
/// </summary>
/// <param name="Connection">The AI connection, or null before one is set up.</param>
/// <param name="OpenWith">The service a card's main button opens. YouTube by default.</param>
/// <param name="CheckNames">Whether names are checked against MusicBrainz. Off by default: about a second per name.</param>
/// <param name="Adventure">The adventurousness level, 1-5.</param>
public sealed record AppSettings(
    AiConnection? Connection,
    MusicService OpenWith,
    bool CheckNames,
    int Adventure)
{
    /// <summary>A new install's settings.</summary>
    public static AppSettings Default { get; } = new(null, MusicService.YouTube, false, Models.Adventure.Default.Level);

    /// <summary>The result lanes left open; a new install has every lane closed. Remembered, not set.</summary>
    public IReadOnlySet<Lane> OpenLanes { get; init; } = new HashSet<Lane>();

    /// <summary>Equal when every setting is, the open lanes as a set.</summary>
    public bool Equals(AppSettings? other)
        => other is not null
           && Connection == other.Connection
           && OpenWith == other.OpenWith
           && CheckNames == other.CheckNames
           && Adventure == other.Adventure
           && OpenLanes.SetEquals(other.OpenLanes);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Connection, OpenWith, CheckNames, Adventure, OpenLanes.Count);
}

/// <summary>Where the settings are kept: the app's own database.</summary>
public interface ISettingsStore
{
    /// <summary>The settings, or <see cref="AppSettings.Default"/> before anything was saved.</summary>
    Task<AppSettings> LoadAsync();

    /// <summary>Keeps the settings.</summary>
    Task SaveAsync(AppSettings settings);
}

/// <summary>A search the user made.</summary>
/// <param name="Artist">The name searched.</param>
/// <param name="Adventure">The level.</param>
/// <param name="At">When.</param>
public sealed record RecentSearch(string Artist, int Adventure, DateTimeOffset At);

/// <summary>
/// What the user searched and was shown, on the device only (design §9). What was shown is sent back as
/// exclusions, so later searches stay new to the user: system-level novelty (design §2).
/// </summary>
public interface IHistoryStore
{
    /// <summary>Remembers a search; searching a name again moves it to the top.</summary>
    Task AddSearchAsync(RecentSearch search);

    /// <summary>The latest searches, newest first, one per name.</summary>
    Task<IReadOnlyList<RecentSearch>> RecentSearchesAsync(int count);

    /// <summary>Remembers that these musicians were shown.</summary>
    Task MarkShownAsync(IEnumerable<string> names, DateTimeOffset at);

    /// <summary>Remembers that the user opened this musician in a service.</summary>
    Task MarkOpenedAsync(string name, DateTimeOffset at);

    /// <summary>Musicians shown before, opened ones first, then newest first.</summary>
    Task<IReadOnlyList<string>> ShownAsync(int count);

    /// <summary>A cached result for this key that is newer than <paramref name="since"/>, or null.</summary>
    Task<DiscoveryResult?> CachedAsync(string key, DateTimeOffset since);

    /// <summary>Caches a result under this key.</summary>
    Task CacheAsync(string key, DiscoveryResult result, DateTimeOffset at);

    /// <summary>Forgets the searches, what was shown, and the cache.</summary>
    Task ClearAsync();
}

/// <summary>A musician the user saved.</summary>
/// <param name="Name">The name.</param>
/// <param name="Why">Why it was suggested.</param>
/// <param name="Tags">Its tags.</param>
/// <param name="KnownFor">An album or song.</param>
/// <param name="SpotifyArtistId">Its Spotify artist id, when verification found one.</param>
/// <param name="SavedAt">When it was saved.</param>
public sealed record SavedMusician(
    string Name,
    string Why,
    IReadOnlyList<string> Tags,
    string? KnownFor,
    string? SpotifyArtistId,
    DateTimeOffset SavedAt);

/// <summary>The user's saved musicians.</summary>
public interface ISavedStore
{
    /// <summary>Saves a musician; saving one again replaces it.</summary>
    Task SaveAsync(SavedMusician musician);

    /// <summary>Removes a saved musician. Nothing happens when it is not saved.</summary>
    Task RemoveAsync(string name);

    /// <summary>Every saved musician, newest first.</summary>
    Task<IReadOnlyList<SavedMusician>> AllAsync();

    /// <summary>The normalised names of every saved musician.</summary>
    Task<IReadOnlySet<string>> NamesAsync();
}

/// <summary>Checks that a suggested musician exists (MusicBrainz; design §8.4).</summary>
public interface IArtistVerifier
{
    /// <summary>The outcome for <paramref name="name"/>.</summary>
    Task<Verification> VerifyAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Opens a musician in a music service: the app on Android and iOS, the browser on Windows (design §8.2).</summary>
public interface IMusicLauncher
{
    /// <summary>Opens <paramref name="link"/>.</summary>
    Task<LaunchOutcome> OpenAsync(MusicLink link, CancellationToken cancellationToken = default);
}

/// <summary>Opens an ordinary web page in the browser, such as a provider's key page.</summary>
public interface IWebBrowser
{
    /// <summary>Opens <paramref name="url"/>.</summary>
    Task OpenAsync(Uri url);
}

/// <summary>Page navigation, so view models do not know about Shell.</summary>
public interface INavigator
{
    /// <summary>Opens <paramref name="route"/> with the given query parameters.</summary>
    Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>Returns to the previous page.</summary>
    Task GoBackAsync();
}

/// <summary>Dialogs, so view models do not know about <c>DisplayAlert</c>.</summary>
public interface IDialogs
{
    /// <summary>Asks a yes/no question; <c>true</c> for <paramref name="accept"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Tells the user something.</summary>
    Task AlertAsync(string title, string message, string ok = "OK");
}

/// <summary>The app's routes and their parameters.</summary>
public static class KofflardRoutes
{
    /// <summary>The search page, the Shell root.</summary>
    public const string Search = "search";

    /// <summary>The results; takes <see cref="ArtistParameter"/>, <see cref="LevelParameter"/> and optionally <see cref="CachedParameter"/>.</summary>
    public const string Results = "results";

    /// <summary>Connecting an AI.</summary>
    public const string Connect = "connect";

    /// <summary>The settings.</summary>
    public const string Settings = "settings";

    /// <summary>The saved musicians.</summary>
    public const string Saved = "saved";

    /// <summary>The musician to search.</summary>
    public const string ArtistParameter = "artist";

    /// <summary>The adventurousness level.</summary>
    public const string LevelParameter = "level";

    /// <summary>"true" when a cached result may be shown (a recent search opened again).</summary>
    public const string CachedParameter = "cached";

    /// <summary>The parameters of a search.</summary>
    public static IReadOnlyDictionary<string, object> ForSearch(string artist, Adventure adventure, bool cached = false)
        => new Dictionary<string, object>
        {
            [ArtistParameter] = artist,
            [LevelParameter] = adventure.Level.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [CachedParameter] = cached ? "true" : "false",
        };

    /// <summary>Reads a search's parameters; null when there is no artist.</summary>
    public static (string Artist, Adventure Adventure, bool Cached)? ReadSearch(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(ArtistParameter, out var artist) || artist?.ToString() is not { Length: > 0 } name)
        {
            return null;
        }

        var level = query.TryGetValue(LevelParameter, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? new Adventure(parsed)
            : Adventure.Default;
        var cached = query.TryGetValue(CachedParameter, out var flag) && flag?.ToString() == "true";
        return (name, level, cached);
    }
}
