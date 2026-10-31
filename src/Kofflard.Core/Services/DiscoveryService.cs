using System.Globalization;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.Core.Services;

/// <summary>
/// One search, end to end: the exclusions from history, the cache, the AI, and remembering what was
/// shown.
/// </summary>
public sealed class DiscoveryService(
    AiConnections connections,
    IDiscoveryEngine engine,
    IHistoryStore history,
    TimeProvider time)
{
    /// <summary>How long a cached result is shown again for a recent search (design §9).</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    /// <summary>How many recent searches the search page lists.</summary>
    public const int RecentCount = 20;

    /// <summary>Searches for musicians like <paramref name="artist"/>.</summary>
    /// <param name="artist">The name as typed; cleaned here.</param>
    /// <param name="adventure">How adventurous.</param>
    /// <param name="useCache">
    /// Whether a result from the last 24 hours may be shown again: yes for a recent search opened again,
    /// no for a new search or "More like this", which ask the AI for musicians not shown yet.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AiException">No AI is connected, or the request failed.</exception>
    public async Task<DiscoveryResult> DiscoverAsync(
        string artist,
        Adventure adventure,
        bool useCache,
        CancellationToken cancellationToken = default)
    {
        var name = Names.Clean(artist);
        if (name.Length == 0)
        {
            throw new ArgumentException("Type a musician's name.", nameof(artist));
        }

        if (await connections.GetWithSecretAsync() is not { } connected)
        {
            throw new AiException(AiFailure.NotConnected, "Connect an AI first.");
        }

        var key = CacheKey(name, adventure, connected.Connection);
        var now = time.GetUtcNow();
        await history.AddSearchAsync(new RecentSearch(name, adventure.Level, now));

        if (useCache && await history.CachedAsync(key, now - CacheFor) is { } cached)
        {
            return cached;
        }

        var exclude = await history.ShownAsync(DiscoveryPrompt.MaxExclusions);
        var result = await engine.DiscoverAsync(connected.Connection, connected.Secret, name, adventure, exclude, cancellationToken);

        if (result.Status == DiscoveryStatus.Ok && result.Suggestions.Count > 0)
        {
            await history.MarkShownAsync(result.Suggestions.Select(suggestion => suggestion.Name), now);
            await history.CacheAsync(key, result, now);
        }

        return result;
    }

    /// <summary>The latest searches, newest first.</summary>
    public Task<IReadOnlyList<RecentSearch>> RecentAsync() => history.RecentSearchesAsync(RecentCount);

    /// <summary>The cache key: the same name, level, provider and model give the same key.</summary>
    public static string CacheKey(string artist, Adventure adventure, AiConnection connection)
        => string.Join(
            '|',
            Names.Normalise(artist),
            adventure.Level.ToString(CultureInfo.InvariantCulture),
            connection.ProviderId,
            connection.Model);
}
