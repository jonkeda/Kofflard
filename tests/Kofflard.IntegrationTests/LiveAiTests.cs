using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Infrastructure.Ai;
using Kofflard.TestSupport;
using Xunit.Abstractions;

namespace Kofflard.IntegrationTests;

/// <summary>
/// The real AI, through the same engine the app uses. Skipped unless asked for: run with
/// <c>KOFFLARD_LIVE_AI=1</c> and <c>--filter Category=Live</c> when the prompt or the model changes.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveAiTests : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly DiscoveryEngine _engine;
    private readonly ITestOutputHelper _output;

    public LiveAiTests(ITestOutputHelper output)
    {
        _output = output;
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

        // How long to the first card and to the whole answer, for this model (.my/design/streaming-answers.md).
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan? firstPick = null;
        DiscoveryResult? result = null;
        await foreach (var update in _engine.DiscoverAsync(live.Connection, live.Key, "Nick Drake", []))
        {
            if (update is DiscoveryUpdate.PickFound)
            {
                firstPick ??= clock.Elapsed;
            }
            else if (update is DiscoveryUpdate.Done done)
            {
                result = done.Result;
            }
        }

        _output.WriteLine($"{live.Connection.Describe()}: first card after {firstPick?.TotalSeconds:0.0} s, whole answer after {clock.Elapsed.TotalSeconds:0.0} s.");
        Assert.NotNull(firstPick);
        Assert.NotNull(result);

        Assert.Equal(DiscoveryStatus.Ok, result.Status);
        Assert.InRange(result.Suggestions.Count, 8, 12);
        Assert.True(result.Count(Lane.Safe) > 0);
        Assert.All(result.Suggestions.Where(s => s.Lane == Lane.Wild), s => Assert.False(string.IsNullOrWhiteSpace(s.Bridge)));
        Assert.DoesNotContain(result.Suggestions, s => s.Name.Equals("Nick Drake", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _http.Dispose();
}
