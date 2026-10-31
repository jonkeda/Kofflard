using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// How many suggestions each lane gets at each adventurousness level: 12 in all (design §3.2).
/// </summary>
public static class LaneMix
{
    /// <summary>The number of suggestions a search asks for and shows.</summary>
    public const int Total = 12;

    private static readonly (int Close, int Stretch, int Leap)[] Mix =
    [
        (8, 4, 0),
        (6, 4, 2),
        (4, 5, 3),
        (3, 4, 5),
        (2, 4, 6),
    ];

    /// <summary>The count for one lane at this level.</summary>
    public static int For(Adventure adventure, Lane lane)
    {
        var mix = Mix[adventure.Level - 1];
        return lane switch
        {
            Lane.Close => mix.Close,
            Lane.Stretch => mix.Stretch,
            _ => mix.Leap,
        };
    }

    /// <summary>The count per lane at this level, in lane order.</summary>
    public static IReadOnlyDictionary<Lane, int> Of(Adventure adventure)
        => Enum.GetValues<Lane>().ToDictionary(lane => lane, lane => For(adventure, lane));
}
