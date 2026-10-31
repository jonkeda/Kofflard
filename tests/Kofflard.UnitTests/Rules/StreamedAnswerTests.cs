using System.Text.Json;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.UnitTests.Rules;

/// <summary>Reading an answer while it is still being written (.my/design/streaming-answers.md).</summary>
public sealed class StreamedAnswerTests
{
    private const string Bridge = "Both recorded with producer Joe Boyd at Sound Techniques";

    // 6 Safe, 4 Curious and 2 Wild, with an excluded name, a duplicate and the searched musician mixed in.
    private static readonly DiscoveryAnswer Answer = new(
        "ok",
        new("Nick Drake", "English folk singer-songwriter"),
        [
            Pick("Vashti Bunyan", "close"),
            Pick("Nick Drake", "close"),
            Pick("John Martyn", "close"),
            Pick("Bert Jansch", "close"),
            Pick("Linda Perhacs", "close"),
            Pick("Bert Jansch", "close"),
            Pick("Bridget St John", "close"),
            Pick("Sibylle Baier", "close"),
            Pick("Bill Fay", "stretch"),
            Pick("Michael Chapman", "stretch"),
            Pick("Shelagh McDonald", "stretch"),
            Pick("Judee Sill", "stretch"),
            Pick("Ali Farka Touré", "leap", Bridge),
            Pick("Talk Talk", "leap", Bridge),
        ],
        []);

    private static readonly string[] Exclude = ["John Martyn"];

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(100_000)]
    public void InAnyChunks_ShowsEachLanesShare_InOrder_AndAllAreInTheResult(int size)
    {
        var json = JsonSerializer.Serialize(Answer);

        var updates = Feed(json, size);

        Assert.Equal(new DiscoveryUpdate.ArtistFound(new Musician("Nick Drake", "English folk singer-songwriter")), updates[0]);
        var shown = updates.OfType<DiscoveryUpdate.PickFound>().Select(p => p.Suggestion).ToList();
        Assert.Equal(
            ["Vashti Bunyan", "Bert Jansch", "Linda Perhacs", "Bridget St John", "Bill Fay", "Michael Chapman", "Shelagh McDonald", "Judee Sill", "Ali Farka Touré", "Talk Talk"],
            shown.Select(s => s.Name));

        var result = SuggestionValidator.Validate(DiscoveryAnswer.Parse(json), "Nick Drake", Exclude);
        Assert.All(shown, pick => Assert.Contains(result.Suggestions, s => s.Name == pick.Name && s.Lane == pick.Lane));
        Assert.Equal(shown.Count + 1, result.Suggestions.Count);
    }

    [Fact]
    public void HalfAPick_IsNeverShown()
    {
        var json = JsonSerializer.Serialize(Answer);
        var cut = json.IndexOf("Linda Perhacs", StringComparison.Ordinal);

        var updates = Feed(json[..cut], json.Length);

        Assert.Equal(["Vashti Bunyan", "Bert Jansch"], updates.OfType<DiscoveryUpdate.PickFound>().Select(p => p.Suggestion.Name));
    }

    [Fact]
    public void AfterAFence_ReadsTheSame()
    {
        var updates = Feed("```json\n" + JsonSerializer.Serialize(Answer) + "\n```", 5);

        Assert.Equal(10, updates.OfType<DiscoveryUpdate.PickFound>().Count());
    }

    [Fact]
    public void PicksBeforeTheStatus_WaitForIt()
    {
        var json = "{\"suggestions\": [" + JsonSerializer.Serialize(Pick("Vashti Bunyan", "close")) + "], \"status\": \"ok\"";
        var answer = new StreamedAnswer("Nick Drake", []);

        Assert.Empty(answer.Add(json[..^16]));
        Assert.Equal("Vashti Bunyan", Assert.IsType<DiscoveryUpdate.PickFound>(Assert.Single(answer.Add(json[^16..]))).Suggestion.Name);
    }

    [Fact]
    public void ForAnAmbiguousName_ShowsNoPicks()
    {
        var json = JsonSerializer.Serialize(Answer with { Status = "ambiguous" });

        Assert.Empty(Feed(json, 3).OfType<DiscoveryUpdate.PickFound>());
    }

    private static List<DiscoveryUpdate> Feed(string json, int size)
    {
        var answer = new StreamedAnswer("Nick Drake", Exclude);
        var updates = new List<DiscoveryUpdate>();
        for (var start = 0; start < json.Length; start += size)
        {
            updates.AddRange(answer.Add(json.Substring(start, Math.Min(size, json.Length - start))));
        }

        return updates;
    }

    private static DiscoveryAnswer.Pick Pick(string name, string lane, string? bridge = null)
        => new(name, lane, $"Why {name}", bridge, ["folk"], "An album");
}
