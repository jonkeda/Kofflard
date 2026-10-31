using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.UnitTests.Rules;

public sealed class SuggestionValidatorTests
{
    private const string GoodBridge = "Both recorded with producer Joe Boyd at Sound Techniques";

    [Fact]
    public void Validate_KeepsEachLanesShare_AtTheDefaultLevel()
    {
        var answer = Answer(Picks(Lane.Close, 8).Concat(Picks(Lane.Stretch, 6)).Concat(Picks(Lane.Leap, 4)));

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Adventure.Default, []);

        Assert.Equal(6, result.Count(Lane.Close));
        Assert.Equal(4, result.Count(Lane.Stretch));
        Assert.Equal(2, result.Count(Lane.Leap));
        Assert.Equal(LaneMix.Total, result.Suggestions.Count);
    }

    [Fact]
    public void Validate_FillsAShortLane_FromTheOthersLeftovers_WithoutRelabelling()
    {
        var answer = Answer(Picks(Lane.Close, 3).Concat(Picks(Lane.Stretch, 9)).Concat(Picks(Lane.Leap, 2)));

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Adventure.Default, []);

        Assert.Equal(3, result.Count(Lane.Close));
        Assert.Equal(7, result.Count(Lane.Stretch));
        Assert.Equal(2, result.Count(Lane.Leap));
    }

    [Fact]
    public void Validate_NeverPads_WhenTheModelGaveTooFew()
    {
        var result = SuggestionValidator.Validate(Answer(Picks(Lane.Close, 2)), "Nick Drake", Adventure.Default, []);

        Assert.Equal(2, result.Suggestions.Count);
    }

    [Fact]
    public void Validate_DropsTheSearchedArtist_Excluded_AndDuplicates_IgnoringCaseAndAccents()
    {
        var answer = Answer(
        [
            Pick("Nick Drake", Lane.Close),
            Pick("björk", Lane.Close),
            Pick("Bjork", Lane.Close),
            Pick("John Martyn", Lane.Close),
            Pick("Vashti Bunyan", Lane.Close),
        ]);

        var result = SuggestionValidator.Validate(answer, "nick drake", Adventure.Default, ["JOHN MARTYN"]);

        Assert.Equal(["björk", "Vashti Bunyan"], result.Suggestions.Select(s => s.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Too short")]
    [InlineData("They have a really similar vibe to him overall")]
    public void Validate_DropsLeaps_WithoutAConcreteBridge(string? bridge)
    {
        var leap = new DiscoveryAnswer.Pick("Ali Farka Touré", "leap", "Desert blues", bridge, ["blues"], null);

        var result = SuggestionValidator.Validate(Answer([leap]), "Nick Drake", Adventure.Default, []);

        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public void Validate_KeepsALeap_WithAConcreteBridge_AndDropsBridgesOnOtherLanes()
    {
        var answer = Answer([Pick("Ali Farka Touré", Lane.Leap, GoodBridge), Pick("Vashti Bunyan", Lane.Close, GoodBridge)]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Adventure.Default, []);

        Assert.Equal(GoodBridge, result.Suggestions.Single(s => s.Lane == Lane.Leap).Bridge);
        Assert.Null(result.Suggestions.Single(s => s.Lane == Lane.Close).Bridge);
    }

    [Fact]
    public void Validate_DropsPicks_WithNoNameOrAnUnknownLane()
    {
        var answer = Answer(
        [
            new DiscoveryAnswer.Pick(" ", "close", "why", null, [], null),
            new DiscoveryAnswer.Pick("Someone", "sideways", "why", null, [], null),
            Pick("Vashti Bunyan", Lane.Close),
        ]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Adventure.Default, []);

        Assert.Equal("Vashti Bunyan", Assert.Single(result.Suggestions).Name);
    }

    [Fact]
    public void Validate_Ambiguous_ReturnsCandidates_AndNoSuggestions()
    {
        var answer = new DiscoveryAnswer(
            "ambiguous",
            null,
            [Pick("Ignored", Lane.Close)],
            [new("Genesis", "English prog rock band"), new("Genesis", "English prog rock band"), new("Genesis Owusu", "Australian rapper")]);

        var result = SuggestionValidator.Validate(answer, "Genesis", Adventure.Default, []);

        Assert.Equal(DiscoveryStatus.Ambiguous, result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(["Genesis", "Genesis Owusu"], result.Candidates.Select(c => c.Name));
    }

    [Fact]
    public void Validate_KeepsAtMostThreeTags()
    {
        var answer = Answer([new DiscoveryAnswer.Pick("Vashti Bunyan", "close", "why", null, ["folk", " ", "1970", "UK", "pastoral"], null)]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Adventure.Default, []);

        Assert.Equal(["folk", "1970", "UK"], result.Suggestions.Single().Tags);
    }

    internal static DiscoveryAnswer Answer(IEnumerable<DiscoveryAnswer.Pick> picks)
        => new("ok", new("Nick Drake", "English folk singer-songwriter"), [.. picks], []);

    internal static DiscoveryAnswer.Pick Pick(string name, Lane lane, string? bridge = null)
        => new(name, lane.ToString().ToLowerInvariant(), $"Why {name}", lane == Lane.Leap ? bridge ?? GoodBridge : bridge, ["folk"], "An album");

    private static IEnumerable<DiscoveryAnswer.Pick> Picks(Lane lane, int count)
        => Enumerable.Range(1, count).Select(i => Pick($"{lane} artist {i}", lane));
}
