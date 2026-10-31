using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Tests;

/// <summary>
/// The adventure choices on Windows rendered as coloured boxes with neither icon nor name: the content of a
/// templated RadioButton did not get the option as its binding context. Saves a screenshot of the search
/// page when <c>KOFFLARD_DIAG_SHOT</c> names a file.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Diagnostic", "adventure-choices")]
public sealed class SearchPageDiagnosticTests(KofflardAppFixture fixture)
{
    [Fact]
    public void SearchPage_AdventureChoices_ShowTheirNames()
    {
        fixture.StartTest();
        if (Environment.GetEnvironmentVariable("KOFFLARD_DIAG_SHOT") is { } shot)
        {
            fixture.Context.SaveScreenshot(shot);
        }

        var search = fixture.Search;
        foreach (var adventure in SearchPage.Adventures)
        {
            Assert.Equal(adventure, search.AdventureName(adventure).GetText());
        }
    }
}
