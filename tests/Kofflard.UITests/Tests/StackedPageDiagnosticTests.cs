using Kofflard.TestSupport;

namespace Kofflard.UITests.Tests;

/// <summary>
/// Regression for two results pages on the Shell stack (.my/rca/stacked-pages.md). After "More →" the
/// UAT runner still holds the first page's object, as it holds one object per page and scenario. On
/// Windows the first page stays in the tree, on screen by UI Automation's account, for about a second, so
/// that object must find the shown page, not the one under it.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Rca", "stacked-pages")]
public sealed class StackedPageDiagnosticTests(KofflardAppFixture fixture)
{
    [Fact]
    public void AfterExploring_TheEarlierPageObject_ReadsTheShownPage()
    {
        var search = fixture.StartTest();
        fixture.Ai.Answer("Bill Fay", FakeAi.Canned("bill-fay"));
        var first = search.SearchFor("Nick Drake").WaitForSuggestions();
        first.Suggestions.LookAt("Bill Fay");
        first.Suggestions.Explore("Bill Fay");

        // Straight away, inside the second in which Windows still reports the first page.
        first.Summary.AssertText("English singer-songwriter and pianist, 1970–");
        first.Suggestions.AssertCard("Judee Sill");
    }
}
