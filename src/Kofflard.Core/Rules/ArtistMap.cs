using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// Where the desktop's artist map puts each pick (.my/design/one-page-desktop.md): the searched musician in
/// the middle, Safe picks above, Curious below, Wild out to the sides. Pure: the same picks give the same map.
/// </summary>
public static class ArtistMap
{
    /// <summary>The map's width.</summary>
    public const double Width = 380;

    /// <summary>The map's height.</summary>
    public const double Height = 160;

    /// <summary>The middle, where the searched musician is.</summary>
    public static (double X, double Y) Centre { get; } = (Width / 2, Height / 2);

    // Up to four places per lane, nearest first.
    private static readonly Dictionary<Lane, (double X, double Y)[]> Places = new()
    {
        [Lane.Safe] = [(130, 22), (250, 22), (70, 38), (310, 38)],
        [Lane.Curious] = [(130, 138), (250, 138), (70, 122), (310, 122)],
        [Lane.Wild] = [(22, 80), (358, 80), (40, 108), (340, 52)],
    };

    /// <summary>A pick on the map.</summary>
    /// <param name="Name">The musician.</param>
    /// <param name="Lane">Its lane, for its colour.</param>
    /// <param name="X">The middle of its dot, from the left.</param>
    /// <param name="Y">The middle of its dot, from the top.</param>
    public sealed record Node(string Name, Lane Lane, double X, double Y);

    /// <summary>
    /// The nodes for <paramref name="picks"/>, in their order: the first four of each lane get a place, the
    /// rest are left off.
    /// </summary>
    public static IReadOnlyList<Node> Lay(IEnumerable<(string Name, Lane Lane)> picks)
    {
        var used = new Dictionary<Lane, int>();
        var nodes = new List<Node>();
        foreach (var (name, lane) in picks)
        {
            var place = used.GetValueOrDefault(lane);
            if (place >= Places[lane].Length)
            {
                continue;
            }

            used[lane] = place + 1;
            var (x, y) = Places[lane][place];
            nodes.Add(new Node(name, lane, x, y));
        }

        return nodes;
    }

    /// <summary>"ND" for Nick Drake, "B" for Björk: the first letters of the first two words.</summary>
    public static string Initials(string name)
        => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
}
