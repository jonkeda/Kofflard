using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// How many suggestions each lane gets: 12 in all, 4 of each for a search (.my/design/adventure-as-outcomes.md).
/// "More like this…" leans to where the results' slider is: more safe picks at Safe, more wild ones at Wild.
/// </summary>
public static class LaneMix
{
    /// <summary>The number of suggestions a search asks for and shows.</summary>
    public const int Total = 12;

    /// <summary>The lean of a search: the even mix.</summary>
    public const Lane Even = Lane.Curious;

    private static readonly (int Safe, int Curious, int Wild)[] Mix =
    [
        (6, 4, 2),
        (4, 4, 4),
        (2, 4, 6),
    ];

    /// <summary>The count for one lane, leaning to <paramref name="lean"/>.</summary>
    public static int For(Lane lean, Lane lane)
    {
        var mix = Mix[(int)lean];
        return lane switch
        {
            Lane.Safe => mix.Safe,
            Lane.Curious => mix.Curious,
            _ => mix.Wild,
        };
    }

    /// <summary>The count per lane, leaning to <paramref name="lean"/>, in lane order.</summary>
    public static IReadOnlyDictionary<Lane, int> Of(Lane lean)
        => Enum.GetValues<Lane>().ToDictionary(lane => lane, lane => For(lean, lane));
}
