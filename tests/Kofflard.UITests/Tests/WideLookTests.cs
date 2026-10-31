namespace Kofflard.UITests.Tests;

/// <summary>The desktop's wide layout (.my/design/one-page-desktop.md): what a scenario can't do.</summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
public sealed class WideLookTests(KofflardAppFixture fixture)
{
    /// <summary>Saves the wide layout with results to <c>KOFFLARD_DIAG_SHOT</c>, to look at it on a device.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Diagnostic", "wide-look")]
    public Task ThePage_WithResults_ForALook()
    {
        fixture.StartTest().SearchFor("Nick Drake").WaitForSuggestions().Suggestions.AssertCard("Ali Farka Touré");
        if (Environment.GetEnvironmentVariable("KOFFLARD_DIAG_SHOT") is { } shot)
        {
            fixture.Context.SaveScreenshot(shot);
        }

        return Task.CompletedTask;
    }
}
