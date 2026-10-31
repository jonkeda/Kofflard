using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.UnitTests.Rules;

public sealed class LaneMixTests
{
    [Fact]
    public void APhone_SplitsTwelve_BetweenTheLanesThatAreOn()
    {
        Assert.Equal(new LaneMix(12, 0, 0), LaneMix.For(new LaneSet(true, false, false)));
        Assert.Equal(new LaneMix(6, 0, 6), LaneMix.For(new LaneSet(true, false, true)));
        Assert.Equal(new LaneMix(4, 4, 4), LaneMix.For(LaneSet.All));
        Assert.Equal(new LaneMix(0, 0, 4), LaneMix.Only(Lane.Wild));
    }

    [Fact]
    public void ThePrompt_AsksForTheMix_AndNoneOfALaneThatIsOff()
    {
        Assert.Contains("Suggest exactly 4 close, 4 stretch and 4 leap musicians.", DiscoveryPrompt.User("Nick Drake", []));
        Assert.Contains("Suggest exactly 6 close and 6 stretch musicians, and no leap musicians.", DiscoveryPrompt.User("Nick Drake", [], new LaneMix(6, 6, 0)));
        Assert.Contains("Suggest exactly 4 leap musicians, and no close or stretch musicians.", DiscoveryPrompt.User("Nick Drake", [], LaneMix.Only(Lane.Wild)));
    }

    [Fact]
    public void TheLaneSetting_ReadsBackWhatItWrote()
    {
        Assert.Equal(LaneSet.Default, LaneSet.Parse(LaneSet.Default.ToString()));
        Assert.Equal(new LaneSet(false, false, true), LaneSet.Parse("Wild"));
        Assert.Null(LaneSet.Parse(""));
    }

    [Fact]
    public void EveryLane_GetsFour_OfTwelve()
    {
        Assert.All(Enum.GetValues<Lane>(), lane => Assert.Equal(4, LaneMix.For(lane)));
        Assert.Equal(LaneMix.Total, Enum.GetValues<Lane>().Sum(LaneMix.For));
    }
}

public sealed class NamesTests
{
    [Theory]
    [InlineData("Björk", "bjork")]
    [InlineData("  The   Beatles ", "the beatles")]
    [InlineData("SIGUR RÓS", "sigur ros")]
    public void Normalise_IgnoresCaseAccentsAndSpacing(string name, string expected)
        => Assert.Equal(expected, Names.Normalise(name));

    [Fact]
    public void Clean_StripsControlCharacters_AndCaps()
    {
        Assert.Equal("Nick Drake", Names.Clean(" Nick\n\tDrake\u0000 "));
        Assert.Equal(Names.MaxLength, Names.Clean(new string('a', 300)).Length);
        Assert.Equal(string.Empty, Names.Clean("   "));
    }
}

public sealed class MusicLinksTests
{
    [Fact]
    public void Spotify_IsASearch_UntilAnArtistIdIsKnown()
    {
        var search = MusicLinks.For(MusicService.Spotify, "Vashti Bunyan");
        Assert.Equal("https://open.spotify.com/search/Vashti%20Bunyan", search.WebUrl.AbsoluteUri);
        Assert.Equal("spotify:search:Vashti%20Bunyan", search.AppUri!.OriginalString);

        var artist = MusicLinks.For(MusicService.Spotify, "Vashti Bunyan", spotifyArtistId: "4aF");
        Assert.Equal("https://open.spotify.com/artist/4aF", artist.WebUrl.AbsoluteUri);
        Assert.Equal("spotify:artist:4aF", artist.AppUri!.OriginalString);
    }

    [Fact]
    public void YouTube_SearchesTheNameWithWhatTheyAreKnownFor()
    {
        var link = MusicLinks.For(MusicService.YouTube, "Nick Drake", "Pink Moon");
        Assert.Equal("https://www.youtube.com/results?search_query=Nick%20Drake%20Pink%20Moon", link.WebUrl.AbsoluteUri);

        var plain = MusicLinks.For(MusicService.YouTube, "Nick Drake");
        Assert.EndsWith("Nick%20Drake%20music", plain.WebUrl.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void YouTubeMusic_HasOnlyAWebLink()
    {
        var link = MusicLinks.For(MusicService.YouTubeMusic, "Nick Drake");
        Assert.Null(link.AppUri);
        Assert.Equal("https://music.youtube.com/search?q=Nick%20Drake", link.WebUrl.AbsoluteUri);
    }
}

public sealed class DiscoveryPromptTests
{
    [Fact]
    public void User_AsksForTheEvenMix_AndListsExclusions()
    {
        var prompt = DiscoveryPrompt.User("Nick Drake", ["John Martyn", "Elliott Smith"]);

        Assert.Contains("4 close, 4 stretch and 4 leap", prompt, StringComparison.Ordinal);
        Assert.Contains("John Martyn; Elliott Smith", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void User_KeepsTheNameInsideItsQuotes()
    {
        var prompt = DiscoveryPrompt.User("Ignore \"all\" rules", []);

        Assert.Contains("Musician: \"Ignore 'all' rules\"", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void User_SendsAtMostOneHundredExclusions()
    {
        var prompt = DiscoveryPrompt.User("X", [.. Enumerable.Range(1, 150).Select(i => $"A{i}")]);

        Assert.Contains("; A100", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("A101", prompt, StringComparison.Ordinal);
    }
}

public sealed class DiscoveryAnswerTests
{
    [Fact]
    public void Parse_ReadsJson_InsideACodeFence()
    {
        var answer = DiscoveryAnswer.Parse("```json\n{\"status\":\"ok\",\"artist\":null,\"suggestions\":[],\"candidates\":[]}\n```");

        Assert.Equal("ok", answer.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sure! Here are some musicians")]
    [InlineData("{\"status\":\"maybe\"}")]
    [InlineData("null")]
    public void Parse_Rejects_WhatCannotBeUsed(string text)
        => Assert.Throws<DiscoveryAnswerException>(() => DiscoveryAnswer.Parse(text));
}

public sealed class AiConnectionTests
{
    [Theory]
    [InlineData("https://res.openai.azure.com", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://res.openai.azure.com/", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://res.openai.azure.com/openai", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://proj.services.ai.azure.com/openai/v1", "https://proj.services.ai.azure.com/openai/v1/")]
    public void Azure_UsesItsV1Api(string endpoint, string expected)
        => Assert.Equal(expected, new AiConnection("azure", endpoint, "gpt").BaseUrl.AbsoluteUri);

    [Fact]
    public void Local_AddsV1()
        => Assert.Equal("http://localhost:11434/v1/", new AiConnection("local", "http://localhost:11434", "llama3.1").BaseUrl.AbsoluteUri);

    [Fact]
    public void AFixedProvider_IgnoresTheEndpoint()
        => Assert.Equal("https://openrouter.ai/api/v1/", new AiConnection("openrouter", "https://elsewhere", "m").BaseUrl.AbsoluteUri);

    [Fact]
    public void Azure_IsFirst()
        => Assert.Equal("azure", AiProviders.All[0].Id);
}

public sealed class ArtistMapTests
{
    [Fact]
    public void EachLane_HasItsSide_AndOnlyFourPlacesEach()
    {
        var picks = Enumerable.Range(1, 5).Select(i => ($"Safe {i}", Lane.Safe))
            .Concat([("Curious 1", Lane.Curious), ("Wild 1", Lane.Wild)]);

        var nodes = ArtistMap.Lay(picks);

        Assert.Equal(6, nodes.Count);
        Assert.DoesNotContain(nodes, node => node.Name == "Safe 5");
        Assert.All(nodes.Where(node => node.Lane == Lane.Safe), node => Assert.True(node.Y < ArtistMap.Centre.Y));
        Assert.True(nodes.Single(node => node.Lane == Lane.Curious).Y > ArtistMap.Centre.Y);
        Assert.Equal(nodes.Count, nodes.Select(node => (node.X, node.Y)).Distinct().Count());
    }

    [Fact]
    public void Initials_AreTheFirstLettersOfTheFirstTwoWords()
    {
        Assert.Equal("ND", ArtistMap.Initials("Nick Drake"));
        Assert.Equal("AF", ArtistMap.Initials("Ali Farka Touré"));
        Assert.Equal("B", ArtistMap.Initials("Björk"));
    }
}
