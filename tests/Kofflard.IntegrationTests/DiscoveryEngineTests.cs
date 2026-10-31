using System.Text.Json;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Infrastructure.Ai;
using Kofflard.TestSupport;

namespace Kofflard.IntegrationTests;

/// <summary>The AI engine through the OpenAI SDK against the fake AI: the real wire format, no real model.</summary>
public sealed class DiscoveryEngineTests : IDisposable
{
    private readonly FakeAi _ai = FakeAi.Start();
    private readonly HttpClient _http = new();
    private readonly DiscoveryEngine _engine;

    public DiscoveryEngineTests()
    {
        _engine = new DiscoveryEngine(new ChatClientFactory(_http, TimeSpan.FromSeconds(10)));
    }

    private AiConnection Local => new("local", _ai.BaseUrl, FakeAi.Model);

    [Fact]
    public async Task Discover_ReturnsTheValidatedLanes()
    {
        var result = await _engine.DiscoverAsync(Local, null, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(DiscoveryStatus.Ok, result.Status);
        Assert.Equal(6, result.Count(Lane.Safe));
        Assert.Equal(4, result.Count(Lane.Curious));
        Assert.Equal(2, result.Count(Lane.Wild));
        Assert.All(result.Suggestions.Where(s => s.Lane == Lane.Wild), s => Assert.NotNull(s.Bridge));
    }

    [Fact]
    public async Task Discover_AsksForTheSchema_AndTheLeanedMix()
    {
        await _engine.DiscoverAsync(Local, null, "Nick Drake", Lane.Wild, ["Bert Jansch"]);

        using var request = JsonDocument.Parse(Assert.Single(_ai.Requests));
        var root = request.RootElement;
        Assert.Equal(FakeAi.Model, root.GetProperty("model").GetString());
        Assert.Equal("json_schema", root.GetProperty("response_format").GetProperty("type").GetString());
        var user = root.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("2 close, 4 stretch and 6 leap", user, StringComparison.Ordinal);
        Assert.Contains("Bert Jansch", user, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discover_DropsExcludedNames_EvenWhenTheModelSuggestsThem()
    {
        var result = await _engine.DiscoverAsync(Local, null, "Nick Drake", LaneMix.Even, ["Vashti Bunyan"]);

        Assert.DoesNotContain(result.Suggestions, s => s.Name == "Vashti Bunyan");
    }

    [Fact]
    public async Task Discover_RetriesOnce_AfterAnUnusableAnswer()
    {
        _ai.InSequence("Sure! Here are some musicians you may like.", FakeAi.Default);

        var result = await _engine.DiscoverAsync(Local, null, "Nick Drake", LaneMix.Even, []);

        Assert.Equal(12, result.Suggestions.Count);
        Assert.Equal(2, _ai.Requests.Count);
        Assert.Contains("could not be used", _ai.Requests[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discover_GivesUp_AfterTwoUnusableAnswers()
    {
        _ai.InSequence("not json", "still not json");

        var failure = await Assert.ThrowsAsync<AiException>(() => _engine.DiscoverAsync(Local, null, "Nick Drake", LaneMix.Even, []));

        Assert.Equal(AiFailure.BadAnswer, failure.Failure);
    }

    [Theory]
    [InlineData(401, AiFailure.InvalidKey)]
    [InlineData(402, AiFailure.NoCredit)]
    [InlineData(404, AiFailure.ModelNotFound)]
    [InlineData(429, AiFailure.RateLimited)]
    [InlineData(503, AiFailure.Unavailable)]
    public async Task Discover_TranslatesProviderErrors(int status, AiFailure expected)
    {
        _ai.FailWith(status);

        var failure = await Assert.ThrowsAsync<AiException>(() => _engine.DiscoverAsync(Local, "key", "Nick Drake", LaneMix.Even, []));

        Assert.Equal(expected, failure.Failure);
    }

    [Fact]
    public async Task Discover_WhenNobodyListens_SaysTheProviderIsUnreachable()
    {
        var nowhere = new AiConnection("local", "http://127.0.0.1:1", FakeAi.Model);

        var failure = await Assert.ThrowsAsync<AiException>(() => _engine.DiscoverAsync(nowhere, null, "Nick Drake", LaneMix.Even, []));

        Assert.Equal(AiFailure.Unreachable, failure.Failure);
    }

    [Fact]
    public async Task Test_PassesOnAnAnswer()
        => await _engine.TestAsync(Local, null);

    [Fact]
    public async Task Azure_SendsTheKeyAsApiKey_ToTheV1Api()
    {
        var azure = new AiConnection("azure", _ai.BaseUrl, "my-deployment");

        await _engine.TestAsync(azure, "azure-key");

        Assert.Equal("/openai/v1/chat/completions", _ai.LastPath);
        var headers = _ai.LastHeaders!;
        Assert.Equal("azure-key", headers["api-key"].Single());
        Assert.Equal("Bearer azure-key", headers["Authorization"].Single());
        using var request = JsonDocument.Parse(_ai.Requests.Single());
        Assert.Equal("my-deployment", request.RootElement.GetProperty("model").GetString());
    }

    public void Dispose()
    {
        _ai.Dispose();
        _http.Dispose();
    }
}
