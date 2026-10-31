namespace Kofflard.UITests.Tests;

/// <summary>
/// The phone's one page (.my/design/single-page-phone.md). Its journeys are UAT scenarios
/// (tests/Kofflard.Uat/Features/PHONE.01-one-page.feature.md); this keeps what a scenario can't do.
/// </summary>
[Collection(PhoneCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Layout", "phone")]
public sealed class PhoneTests(PhoneAppFixture fixture)
{
    /// <summary>Saves the narrow layout with results to <c>KOFFLARD_DIAG_SHOT</c>, to look at it on a device.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Diagnostic", "phone-look")]
    public Task ThePage_WithResults_ForALook()
    {
        fixture.StartTest().SearchFor("Nick Drake").WaitForSuggestions();
        if (Environment.GetEnvironmentVariable("KOFFLARD_DIAG_SHOT") is { } shot)
        {
            fixture.Context.SaveScreenshot(shot);
        }

        return Task.CompletedTask;
    }
}
