using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// Turns the model's answer into what the app shows (design §7.2). The model suggests; this decides.
/// </summary>
/// <remarks>
/// In order: drop picks with no name or lane, duplicates, the searched musician and anything excluded;
/// drop wild picks whose bridge is missing or generic; take each lane's share of the mix; fill what is still
/// short from the other lanes' leftovers, never by inventing picks; cap at <see cref="LaneMix.Total"/>.
/// </remarks>
public static class SuggestionValidator
{
    /// <summary>A bridge shorter than this says nothing concrete.</summary>
    public const int MinBridgeLength = 20;

    private static readonly string[] GenericBridges =
    [
        "similar vibe",
        "similar sound",
        "similar style",
        "similar feel",
        "similar music",
        "same vibe",
        "same energy",
        "also good",
        "you might like",
        "fans will like",
        "fans also like",
    ];

    /// <summary>Validates <paramref name="answer"/> for a search on <paramref name="artist"/>.</summary>
    /// <param name="answer">The parsed answer.</param>
    /// <param name="artist">The name that was searched.</param>
    /// <param name="exclude">Names already shown.</param>
    /// <param name="mix">How many of each lane were asked for; <see cref="LaneMix.Even"/> when null.</param>
    public static DiscoveryResult Validate(
        DiscoveryAnswer answer,
        string artist,
        IReadOnlyCollection<string> exclude,
        LaneMix? mix = null)
    {
        var status = answer.Status switch
        {
            "ambiguous" => DiscoveryStatus.Ambiguous,
            "unknown" => DiscoveryStatus.Unknown,
            _ => DiscoveryStatus.Ok,
        };

        var searched = answer.Artist is { Name: { Length: > 0 } name }
            ? new Musician(name.Trim(), Blank(answer.Artist.Summary))
            : null;

        var candidates = (answer.Candidates ?? [])
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name))
            .Select(candidate => new Musician(candidate.Name!.Trim(), Blank(candidate.Summary)))
            .DistinctBy(candidate => Names.Normalise(candidate.Name))
            .Take(4)
            .ToList();

        if (status != DiscoveryStatus.Ok)
        {
            return new DiscoveryResult(status, searched, [], candidates);
        }

        var filter = new PickFilter(artist, exclude);
        filter.Searched(searched?.Name);
        var usable = (answer.Suggestions ?? []).Select(filter.Accept).OfType<Suggestion>().ToList();

        return new DiscoveryResult(status, searched, Balance(usable, mix ?? LaneMix.Even), candidates);
    }

    /// <summary>Whether <paramref name="bridge"/> names a concrete link rather than a vague likeness.</summary>
    public static bool IsConcreteBridge(string? bridge)
    {
        if (string.IsNullOrWhiteSpace(bridge) || bridge.Trim().Length < MinBridgeLength)
        {
            return false;
        }

        return !GenericBridges.Any(generic => bridge.Contains(generic, StringComparison.OrdinalIgnoreCase));
    }

    // Each lane's share first, in lane order; then whatever is still short from the leftovers, in the
    // model's order, from the lanes asked for only. A pick keeps the lane the model gave it.
    private static List<Suggestion> Balance(List<Suggestion> usable, LaneMix mix)
    {
        var chosen = new List<Suggestion>();
        foreach (var lane in Enum.GetValues<Lane>())
        {
            chosen.AddRange(usable.Where(suggestion => suggestion.Lane == lane).Take(mix.Count(lane)));
        }

        var leftovers = usable.Where(suggestion => !chosen.Contains(suggestion) && mix.Count(suggestion.Lane) > 0);
        chosen.AddRange(leftovers.Take(Math.Max(0, mix.Sum - chosen.Count)));

        return [.. chosen.OrderBy(suggestion => suggestion.Lane).Take(mix.Sum)];
    }

    internal static Suggestion? ToSuggestion(DiscoveryAnswer.Pick pick)
    {
        if (string.IsNullOrWhiteSpace(pick.Name) || ParseLane(pick.Lane) is not { } lane)
        {
            return null;
        }

        var bridge = Blank(pick.Bridge);
        if (lane == Lane.Wild && !IsConcreteBridge(bridge))
        {
            return null;
        }

        var tags = (pick.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Take(3)
            .ToList();

        return new Suggestion(
            pick.Name.Trim(),
            lane,
            Blank(pick.Why) ?? string.Empty,
            lane == Lane.Wild ? bridge : null,
            tags,
            Blank(pick.KnownFor));
    }

    // The prompt's words for the lanes.
    private static Lane? ParseLane(string? lane) => lane?.Trim().ToLowerInvariant() switch
    {
        "close" => Lane.Safe,
        "stretch" => Lane.Curious,
        "leap" => Lane.Wild,
        _ => null,
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>
/// The rules for one pick (<see cref="SuggestionValidator"/>): a name and a known lane, a concrete bridge for a
/// wild pick, not the searched musician, not excluded, not a duplicate. Used on a whole answer, or one pick
/// at a time as it streams in, with the same outcome.
/// </summary>
public sealed class PickFilter
{
    private readonly HashSet<string> _blocked;

    /// <summary>Creates the filter for a search on <paramref name="artist"/>.</summary>
    /// <param name="artist">The name that was searched.</param>
    /// <param name="exclude">Names already shown.</param>
    public PickFilter(string artist, IReadOnlyCollection<string> exclude)
    {
        _blocked = new HashSet<string>(exclude.Select(Names.Normalise)) { Names.Normalise(artist) };
    }

    /// <summary>Blocks the musician as the model named them, too.</summary>
    public void Searched(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _blocked.Add(Names.Normalise(name.Trim()));
        }
    }

    /// <summary>The pick as a suggestion, or null when it breaks a rule or came before.</summary>
    public Suggestion? Accept(DiscoveryAnswer.Pick pick)
        => SuggestionValidator.ToSuggestion(pick) is { } suggestion && _blocked.Add(Names.Normalise(suggestion.Name))
            ? suggestion
            : null;
}
