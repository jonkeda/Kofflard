namespace Kofflard.Core.Models;

/// <summary>
/// How adventurous a search is, from 1 (safe) to 5 (wild). The default is 2.
/// </summary>
/// <remarks>
/// The user sets it; the app never raises it on its own. The thesis found that serendipity "out
/// of the blue" lowers satisfaction, so even level 5 keeps close picks (design §2, §3.2).
/// </remarks>
public readonly record struct Adventure
{
    /// <summary>The lowest level.</summary>
    public const int Min = 1;

    /// <summary>The highest level.</summary>
    public const int Max = 5;

    /// <summary>The level a new install starts on.</summary>
    public static Adventure Default { get; } = new(2);

    /// <summary>Every level, Safe first.</summary>
    public static IReadOnlyList<Adventure> All { get; } = [.. Enumerable.Range(Min, Max).Select(level => new Adventure(level))];

    /// <summary>Creates a level, clamped to 1-5.</summary>
    public Adventure(int level) => Level = Math.Clamp(level, Min, Max);

    /// <summary>The level, 1-5.</summary>
    public int Level { get; }

    /// <summary>"Safe", "Curious", "Open", "Bold" or "Wild".</summary>
    public string Name => Level switch
    {
        1 => "Safe",
        2 => "Curious",
        3 => "Open",
        4 => "Bold",
        _ => "Wild",
    };

    /// <summary>The level's icon, from staying in harbour to leaving the planet: "adventure_safe.png".</summary>
    public string Icon => $"adventure_{Name.ToLowerInvariant()}.png";

    /// <summary>The icon in white, for the chosen level: "adventure_safe_on.png".</summary>
    public string ChosenIcon => $"adventure_{Name.ToLowerInvariant()}_on.png";

    /// <inheritdoc />
    public override string ToString() => $"{Level} ({Name})";
}
