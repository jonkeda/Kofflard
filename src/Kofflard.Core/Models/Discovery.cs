namespace Kofflard.Core.Models;

/// <summary>Where a suggestion sits on the scale from safe to surprising (design §3.2).</summary>
public enum Lane
{
    /// <summary>High relevance, some novelty, little surprise.</summary>
    Close,

    /// <summary>Relevant and novel: lesser-known artists, other eras, the same sound.</summary>
    Stretch,

    /// <summary>Another genre, country or decade, joined to the original by a bridge.</summary>
    Leap,
}

/// <summary>How the model answered the name it was given.</summary>
public enum DiscoveryStatus
{
    /// <summary>It knew the musician and suggested others.</summary>
    Ok,

    /// <summary>The name fits several musicians; <see cref="DiscoveryResult.Candidates"/> lists them.</summary>
    Ambiguous,

    /// <summary>It did not know the name; candidates may hold close matches.</summary>
    Unknown,
}

/// <summary>A musician as the model describes them.</summary>
/// <param name="Name">The name.</param>
/// <param name="Summary">One line: genre, country, years. May be null.</param>
public sealed record Musician(string Name, string? Summary);

/// <summary>Whether a suggested name was confirmed against MusicBrainz (design §8.4).</summary>
public enum VerificationState
{
    /// <summary>Not checked: checking is off, or has not reached this name yet.</summary>
    Unverified,

    /// <summary>MusicBrainz knows an artist of exactly this name.</summary>
    Verified,

    /// <summary>MusicBrainz knows no artist of this name.</summary>
    NotFound,
}

/// <summary>The outcome of checking one name.</summary>
/// <param name="State">The outcome.</param>
/// <param name="MusicBrainzId">The artist's MBID when verified.</param>
/// <param name="SpotifyArtistId">The Spotify artist id from MusicBrainz's URL relations, when it has one.</param>
public sealed record Verification(VerificationState State, string? MusicBrainzId = null, string? SpotifyArtistId = null)
{
    /// <summary>Not checked.</summary>
    public static Verification Unverified { get; } = new(VerificationState.Unverified);

    /// <summary>Checked, and not found.</summary>
    public static Verification NotFound { get; } = new(VerificationState.NotFound);
}

/// <summary>One suggested musician.</summary>
/// <param name="Name">The name.</param>
/// <param name="Lane">The lane.</param>
/// <param name="Why">One line on why it fits.</param>
/// <param name="Bridge">For a leap: the concrete link back to the typed artist. Null otherwise.</param>
/// <param name="Tags">One to three tags: genre, era, country.</param>
/// <param name="KnownFor">A real album or song, so the user can check the name.</param>
public sealed record Suggestion(
    string Name,
    Lane Lane,
    string Why,
    string? Bridge,
    IReadOnlyList<string> Tags,
    string? KnownFor);

/// <summary>What a search returned, validated (design §7.2).</summary>
/// <param name="Status">How the model answered.</param>
/// <param name="Artist">The musician searched for, as the model knows them.</param>
/// <param name="Suggestions">The suggestions, in lane order.</param>
/// <param name="Candidates">For an ambiguous or unknown name: who the user may have meant.</param>
public sealed record DiscoveryResult(
    DiscoveryStatus Status,
    Musician? Artist,
    IReadOnlyList<Suggestion> Suggestions,
    IReadOnlyList<Musician> Candidates)
{
    /// <summary>The count per lane, in lane order.</summary>
    public int Count(Lane lane) => Suggestions.Count(suggestion => suggestion.Lane == lane);
}
