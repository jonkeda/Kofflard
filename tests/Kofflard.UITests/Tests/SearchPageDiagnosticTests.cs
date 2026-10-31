using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Tests;

/// <summary>
/// The adventure choices on Windows rendered as coloured boxes with neither icon nor name: the content of a
/// templated RadioButton did not get the option as its binding context. They are now the results page's
/// slider. Saves a screenshot of the results when <c>KOFFLARD_DIAG_SHOT</c> names a file.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Diagnostic", "adventure-choices")]
public sealed class SearchPageDiagnosticTests(KofflardAppFixture fixture)
{
    [Fact]
    public void ResultsPage_AdventureChoices_ShowTheirNames()
    {
        var results = fixture.StartTest().SearchFor("Nick Drake").WaitForSuggestions();
        if (Environment.GetEnvironmentVariable("KOFFLARD_DIAG_SHOT") is { } shot)
        {
            fixture.Context.SaveScreenshot(shot);
        }

        foreach (var adventure in ResultsPage.Adventures)
        {
            Assert.Equal(adventure, results.AdventureName(adventure).GetText());
        }
    }
}
