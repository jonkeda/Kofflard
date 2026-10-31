namespace Kofflard.Core.Models;

/// <summary>
/// How adventurous a suggestion is, from safe to wild (design §3.2, .my/design/adventure-as-outcomes.md).
/// The order matters: the results show every lane up to the one the user slid to.
/// </summary>
/// <remarks>The prompt still calls them close, stretch and leap; those words never reach the screen.</remarks>
public enum Lane
{
    /// <summary>High relevance, some novelty, little surprise. The prompt's "close".</summary>
    Safe,

    /// <summary>Relevant and novel: lesser-known artists, other eras, the same sound. The prompt's "stretch".</summary>
    Curious,

    /// <summary>Another genre, country or decade, joined to the original by a bridge. The prompt's "leap".</summary>
    Wild,
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
/// <param name="Bridge">For a wild pick: the concrete link back to the typed artist. Null otherwise.</param>
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

/// <summary>
/// One step of a search as the answer comes in (.my/design/streaming-answers.md): the musician searched for,
/// a pick to show, and at the end the whole validated result.
/// </summary>
public abstract record DiscoveryUpdate
{
    /// <summary>The musician searched for, as the model knows them.</summary>
    public sealed record ArtistFound(Musician Artist) : DiscoveryUpdate;

    /// <summary>A pick that passed the rules and fits its lane's share: show it now.</summary>
    public sealed record PickFound(Suggestion Suggestion) : DiscoveryUpdate;

    /// <summary>
    /// The whole answer, validated. It holds every pick shown before, and may add a few that top up a short
    /// lane; for an ambiguous or unknown name it holds the candidates.
    /// </summary>
    public sealed record Done(DiscoveryResult Result) : DiscoveryUpdate;
}
