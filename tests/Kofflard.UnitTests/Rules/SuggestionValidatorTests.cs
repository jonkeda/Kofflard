using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.UnitTests.Rules;

public sealed class SuggestionValidatorTests
{
    private const string GoodBridge = "Both recorded with producer Joe Boyd at Sound Techniques";

    [Fact]
    public void Validate_KeepsEachLanesShare_OfTheEvenMix()
    {
        var answer = Answer(Picks(Lane.Safe, 8).Concat(Picks(Lane.Curious, 6)).Concat(Picks(Lane.Wild, 6)));

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(4, result.Count(Lane.Safe));
        Assert.Equal(4, result.Count(Lane.Curious));
        Assert.Equal(4, result.Count(Lane.Wild));
        Assert.Equal(LaneMix.Total, result.Suggestions.Count);
    }

    [Fact]
    public void Validate_KeepsEachLanesShare_LeaningWild()
    {
        var answer = Answer(Picks(Lane.Safe, 8).Concat(Picks(Lane.Curious, 6)).Concat(Picks(Lane.Wild, 6)));

        var result = SuggestionValidator.Validate(answer, "Nick Drake", Lane.Wild, []);

        Assert.Equal(2, result.Count(Lane.Safe));
        Assert.Equal(4, result.Count(Lane.Curious));
        Assert.Equal(6, result.Count(Lane.Wild));
    }

    [Fact]
    public void Validate_ReadsThePromptsLaneWords()
    {
        var answer = Answer(
        [
            new DiscoveryAnswer.Pick("Vashti Bunyan", "close", "why", null, [], null),
            new DiscoveryAnswer.Pick("Bill Fay", "stretch", "why", null, [], null),
            new DiscoveryAnswer.Pick("Ali Farka Touré", "leap", "why", GoodBridge, [], null),
            new DiscoveryAnswer.Pick("Someone", "wild", "why", GoodBridge, [], null),
        ]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal([Lane.Safe, Lane.Curious, Lane.Wild], result.Suggestions.Select(s => s.Lane));
    }

    [Fact]
    public void Validate_FillsAShortLane_FromTheOthersLeftovers_WithoutRelabelling()
    {
        var answer = Answer(Picks(Lane.Safe, 3).Concat(Picks(Lane.Curious, 9)).Concat(Picks(Lane.Wild, 2)));

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(3, result.Count(Lane.Safe));
        Assert.Equal(7, result.Count(Lane.Curious));
        Assert.Equal(2, result.Count(Lane.Wild));
    }

    [Fact]
    public void Validate_NeverPads_WhenTheModelGaveTooFew()
    {
        var result = SuggestionValidator.Validate(Answer(Picks(Lane.Safe, 2)), "Nick Drake", LaneMix.Even, []);

        Assert.Equal(2, result.Suggestions.Count);
    }

    [Fact]
    public void Validate_DropsTheSearchedArtist_Excluded_AndDuplicates_IgnoringCaseAndAccents()
    {
        var answer = Answer(
        [
            Pick("Nick Drake", Lane.Safe),
            Pick("björk", Lane.Safe),
            Pick("Bjork", Lane.Safe),
            Pick("John Martyn", Lane.Safe),
            Pick("Vashti Bunyan", Lane.Safe),
        ]);

        var result = SuggestionValidator.Validate(answer, "nick drake", LaneMix.Even, ["JOHN MARTYN"]);

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

        var result = SuggestionValidator.Validate(Answer([leap]), "Nick Drake", LaneMix.Even, []);

        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public void Validate_KeepsALeap_WithAConcreteBridge_AndDropsBridgesOnOtherLanes()
    {
        var answer = Answer([Pick("Ali Farka Touré", Lane.Wild, GoodBridge), Pick("Vashti Bunyan", Lane.Safe, GoodBridge)]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(GoodBridge, result.Suggestions.Single(s => s.Lane == Lane.Wild).Bridge);
        Assert.Null(result.Suggestions.Single(s => s.Lane == Lane.Safe).Bridge);
    }

    [Fact]
    public void Validate_DropsPicks_WithNoNameOrAnUnknownLane()
    {
        var answer = Answer(
        [
            new DiscoveryAnswer.Pick(" ", "close", "why", null, [], null),
            new DiscoveryAnswer.Pick("Someone", "sideways", "why", null, [], null),
            Pick("Vashti Bunyan", Lane.Safe),
        ]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal("Vashti Bunyan", Assert.Single(result.Suggestions).Name);
    }

    [Fact]
    public void Validate_Ambiguous_ReturnsCandidates_AndNoSuggestions()
    {
        var answer = new DiscoveryAnswer(
            "ambiguous",
            null,
            [Pick("Ignored", Lane.Safe)],
            [new("Genesis", "English prog rock band"), new("Genesis", "English prog rock band"), new("Genesis Owusu", "Australian rapper")]);

        var result = SuggestionValidator.Validate(answer, "Genesis", LaneMix.Even, []);

        Assert.Equal(DiscoveryStatus.Ambiguous, result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(["Genesis", "Genesis Owusu"], result.Candidates.Select(c => c.Name));
    }

    [Fact]
    public void Validate_KeepsAtMostThreeTags()
    {
        var answer = Answer([new DiscoveryAnswer.Pick("Vashti Bunyan", "close", "why", null, ["folk", " ", "1970", "UK", "pastoral"], null)]);

        var result = SuggestionValidator.Validate(answer, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(["folk", "1970", "UK"], result.Suggestions.Single().Tags);
    }

    internal static DiscoveryAnswer Answer(IEnumerable<DiscoveryAnswer.Pick> picks)
        => new("ok", new("Nick Drake", "English folk singer-songwriter"), [.. picks], []);

    internal static DiscoveryAnswer.Pick Pick(string name, Lane lane, string? bridge = null)
        => new(name, PromptWord(lane), $"Why {name}", lane == Lane.Wild ? bridge ?? GoodBridge : bridge, ["folk"], "An album");

    // The prompt still calls the lanes close, stretch and leap.
    internal static string PromptWord(Lane lane) => lane switch
    {
        Lane.Safe => "close",
        Lane.Curious => "stretch",
        _ => "leap",
    };

    private static IEnumerable<DiscoveryAnswer.Pick> Picks(Lane lane, int count)
        => Enumerable.Range(1, count).Select(i => Pick($"{lane} artist {i}", lane));
}
