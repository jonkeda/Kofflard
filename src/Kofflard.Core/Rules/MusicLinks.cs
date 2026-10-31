using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// The links for a musician in each service (design §8.1). Search links unless a Spotify artist id is
/// known, so a link never leads nowhere, even when the model got a name wrong.
/// </summary>
public static class MusicLinks
{
    /// <summary>The link for <paramref name="name"/> in <paramref name="service"/>.</summary>
    /// <param name="service">The service.</param>
    /// <param name="name">The musician's name.</param>
    /// <param name="knownFor">A title they are known for, which sharpens a YouTube search.</param>
    /// <param name="spotifyArtistId">Their Spotify artist id, when verification found one.</param>
    public static MusicLink For(MusicService service, string name, string? knownFor = null, string? spotifyArtistId = null)
    {
        var query = Uri.EscapeDataString(name.Trim());
        return service switch
        {
            MusicService.Spotify when !string.IsNullOrWhiteSpace(spotifyArtistId) => new(
                service,
                new Uri($"spotify:artist:{spotifyArtistId}"),
                new Uri($"https://open.spotify.com/artist/{spotifyArtistId}")),
            MusicService.Spotify => new(
                service,
                new Uri($"spotify:search:{query}"),
                new Uri($"https://open.spotify.com/search/{query}")),
            // No app scheme: music.youtube.com is an App Link / Universal Link, so the app takes it when installed.
            MusicService.YouTubeMusic => new(
                service,
                null,
                new Uri($"https://music.youtube.com/search?q={query}")),
            _ => YouTube(name, knownFor),
        };
    }

    /// <summary>"YouTube", "YouTube Music" or "Spotify".</summary>
    public static string DisplayName(MusicService service) => service switch
    {
        MusicService.YouTubeMusic => "YouTube Music",
        MusicService.Spotify => "Spotify",
        _ => "YouTube",
    };

    private static MusicLink YouTube(string name, string? knownFor)
    {
        var words = string.IsNullOrWhiteSpace(knownFor) ? $"{name.Trim()} music" : $"{name.Trim()} {knownFor.Trim()}";
        var query = Uri.EscapeDataString(words);
        return new(
            MusicService.YouTube,
            new Uri($"youtube://results?search_query={query}"),
            new Uri($"https://www.youtube.com/results?search_query={query}"));
    }
}
