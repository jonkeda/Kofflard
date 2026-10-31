using Bruinet.Core.Testing;
using Kofflard.TestSupport;
using Kofflard.Uat.Runtime;

namespace Kofflard.Uat.Phrases;

/// <summary>
/// Steps about what is off the screen: what the fake AI answers, and what the app opened in a music
/// service. Scenarios reach them through <c>Using | Ai</c> in <c>uat.config.md</c>.
/// </summary>
[BddStepDefinitions(Namespace = "Ai")]
public sealed class AiPhrases(KofflardUatFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>The fake AI answers a search for <paramref name="musician"/> with a canned answer (<c>Answers/&lt;name&gt;.json</c>).</summary>
    [BddGiven("the AI answers {musician} with {answer}")]
    public void AiAnswers(string musician, string answer) => fixture.Ai.Answer(musician, FakeAi.Canned(answer));

    /// <summary>The fake AI refuses every request, as a provider refusing the key does.</summary>
    [BddGiven("the AI refuses the key")]
    public void AiRefusesTheKey() => fixture.Ai.FailWith(401);

    /// <summary>Asserts the app opened <paramref name="url"/> in a music service (on Windows, in the browser).</summary>
    [BddThen("the app should have opened {url}")]
    public void AppOpened(string url)
    {
        var line = fixture.WaitForOpenedLink("music ");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!fixture.OpenedLinks.Any(opened => opened.EndsWith(" " + url, StringComparison.Ordinal)) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }

        Assert.True(
            fixture.OpenedLinks.Any(opened => opened.EndsWith(" " + url, StringComparison.Ordinal)),
            $"Expected the app to open {url}. Opened: {string.Join(" | ", fixture.OpenedLinks)} (first: {line})");
    }
}
