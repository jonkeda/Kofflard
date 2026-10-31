using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;
using Kofflard.Infrastructure.Data;

namespace Kofflard.Infrastructure.Music;

/// <summary>
/// Checks names against MusicBrainz: free, no key, on the device (design §8.4). Off unless the user
/// turns on Check musician names.
/// </summary>
/// <remarks>
/// <para>
/// MusicBrainz allows about one request a second and asks for a descriptive User-Agent. Requests from
/// every instance go through one gate, spaced <see cref="Spacing"/> apart; outcomes are cached 30 days.
/// </para>
/// <para>
/// A name is verified only when MusicBrainz has an artist of exactly that name (accents and case
/// aside) with a score of at least <see cref="MinScore"/>. The artist's URL relations often hold its
/// Spotify page; then the Spotify button goes straight to the artist.
/// </para>
/// </remarks>
public sealed class MusicBrainzVerifier(HttpClient http, Uri baseUrl, SqliteVerificationCache cache, TimeProvider time, TimeSpan? spacing = null) : IArtistVerifier
{
    /// <summary>The User-Agent MusicBrainz asks for.</summary>
    public const string UserAgent = "Kofflard/1.0 ( https://github.com/jonkeda/kofflard )";

    /// <summary>The lowest search score accepted.</summary>
    public const int MinScore = 90;

    /// <summary>The time between two requests, unless a test asks for less.</summary>
    public static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(1100);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public async Task<Verification> VerifyAsync(string name, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        if (await cache.GetAsync(name, now - SqliteVerificationCache.KeepFor) is { } cached)
        {
            return cached;
        }

        var query = Uri.EscapeDataString($"artist:\"{name.Replace("\"", string.Empty, StringComparison.Ordinal)}\"");
        var search = await GetAsync<SearchResponse>($"ws/2/artist?query={query}&fmt=json&limit=5", cancellationToken);
        var match = search?.Artists?
            .Where(artist => artist.Score >= MinScore && artist.Name is not null && Names.Same(artist.Name, name))
            .OrderByDescending(artist => artist.Score)
            .FirstOrDefault();

        Verification verification;
        if (match?.Id is not { } mbid)
        {
            verification = Verification.NotFound;
        }
        else
        {
            var details = await GetAsync<ArtistResponse>($"ws/2/artist/{mbid}?inc=url-rels&fmt=json", cancellationToken);
            verification = new Verification(VerificationState.Verified, mbid, SpotifyId(details));
        }

        await cache.SetAsync(name, verification, now);
        return verification;
    }

    /// <summary>The Spotify artist id in an artist's URL relations, or null.</summary>
    internal static string? SpotifyId(ArtistResponse? artist)
    {
        const string marker = "open.spotify.com/artist/";
        var url = artist?.Relations?
            .Select(relation => relation.Url?.Resource)
            .FirstOrDefault(resource => resource?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);
        if (url is null)
        {
            return null;
        }

        var id = url[(url.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length)..];
        var end = id.IndexOfAny(['?', '/', '#']);
        return end >= 0 ? id[..end] : id;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var wait = _lastRequest + (spacing ?? Spacing) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl, path));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, cancellationToken);
            _lastRequest = DateTimeOffset.UtcNow;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    internal sealed record SearchResponse([property: JsonPropertyName("artists")] IReadOnlyList<SearchArtist>? Artists);

    internal sealed record SearchArtist(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("score")] int Score);

    internal sealed record ArtistResponse([property: JsonPropertyName("relations")] IReadOnlyList<Relation>? Relations);

    internal sealed record Relation(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("url")] RelationUrl? Url);

    internal sealed record RelationUrl([property: JsonPropertyName("resource")] string? Resource);
}
