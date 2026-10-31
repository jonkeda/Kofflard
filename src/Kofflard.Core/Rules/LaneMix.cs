using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// How many suggestions each lane gets: 12 in all, 4 Safe, 4 Curious and 4 Wild
/// (.my/design/adventure-as-outcomes.md).
/// </summary>
public static class LaneMix
{
    /// <summary>The number of suggestions a search asks for and shows.</summary>
    public const int Total = 12;

    /// <summary>The count for one lane.</summary>
    public static int For(Lane lane) => Total / Enum.GetValues<Lane>().Length;
}
