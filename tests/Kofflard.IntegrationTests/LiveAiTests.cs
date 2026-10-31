using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Infrastructure.Ai;
using Kofflard.TestSupport;

namespace Kofflard.IntegrationTests;

/// <summary>
/// The real AI (ai.secrets.json), through the same engine the app uses. Skipped without secrets, and not
/// part of the gate: run with <c>--filter Category=Live</c> when the prompt or the model changes.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveAiTests : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly DiscoveryEngine _engine;

    public LiveAiTests()
    {
        _engine = new DiscoveryEngine(new ChatClientFactory(_http, TimeSpan.FromSeconds(120)));
    }

    [LiveAiFact]
    public async Task TheConnectionWorks()
    {
        var live = LiveAi.Load();

        await _engine.TestAsync(live.Connection, live.Key);
    }

    [LiveAiFact]
    public async Task AKnownMusician_GetsLanes_AndEveryLeapABridge()
    {
        var live = LiveAi.Load();

        var result = await _engine.DiscoverAsync(live.Connection, live.Key, "Nick Drake", []);

        Assert.Equal(DiscoveryStatus.Ok, result.Status);
        Assert.InRange(result.Suggestions.Count, 8, 12);
        Assert.True(result.Count(Lane.Safe) > 0);
        Assert.All(result.Suggestions.Where(s => s.Lane == Lane.Wild), s => Assert.False(string.IsNullOrWhiteSpace(s.Bridge)));
        Assert.DoesNotContain(result.Suggestions, s => s.Name.Equals("Nick Drake", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _http.Dispose();
}
