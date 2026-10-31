using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// How many suggestions a search asks for in each lane. The desktop design asks for 4 Safe, 4 Curious and 4
/// Wild (.my/design/adventure-as-outcomes.md); a phone splits 12 between the lanes that are on, and asks
/// for 4 of one lane turned on later (.my/design/single-page-phone.md).
/// </summary>
/// <param name="Safe">Safe picks.</param>
/// <param name="Curious">Curious picks.</param>
/// <param name="Wild">Wild picks.</param>
public sealed record LaneMix(int Safe, int Curious, int Wild)
{
    /// <summary>The number of suggestions a search asks for and shows.</summary>
    public const int Total = 12;

    /// <summary>How many a lane turned on later asks for.</summary>
    public const int OneLane = 4;

    /// <summary>4 + 4 + 4.</summary>
    public static LaneMix Even { get; } = For(LaneSet.All);

    /// <summary><see cref="Total"/> split between the lanes in <paramref name="lanes"/>: 12; 6 + 6; 4 + 4 + 4.</summary>
    public static LaneMix For(LaneSet lanes)
    {
        var each = lanes.Count == 0 ? 0 : Total / lanes.Count;
        return new LaneMix(lanes.Safe ? each : 0, lanes.Curious ? each : 0, lanes.Wild ? each : 0);
    }

    /// <summary><paramref name="count"/> picks of one lane.</summary>
    public static LaneMix Only(Lane lane, int count = OneLane) => lane switch
    {
        Lane.Safe => new LaneMix(count, 0, 0),
        Lane.Curious => new LaneMix(0, count, 0),
        _ => new LaneMix(0, 0, count),
    };

    /// <summary>The count for <paramref name="lane"/> in the even mix.</summary>
    public static int For(Lane lane) => Even.Count(lane);

    /// <summary>The count for <paramref name="lane"/>.</summary>
    public int Count(Lane lane) => lane switch
    {
        Lane.Safe => Safe,
        Lane.Curious => Curious,
        _ => Wild,
    };

    /// <summary>Every pick asked for.</summary>
    public int Sum => Safe + Curious + Wild;

    /// <summary>"4-4-4", for the cache key.</summary>
    public override string ToString() => $"{Safe}-{Curious}-{Wild}";
}
