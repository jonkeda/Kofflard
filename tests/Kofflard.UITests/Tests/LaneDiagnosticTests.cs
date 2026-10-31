using Xunit.Abstractions;

namespace Kofflard.UITests.Tests;

/// <summary>
/// Regression for the results list on Windows versus headless (.my/rca/windows-vs-headless.md): with every
/// lane shown the list is taller than the window, and a card from each lane must still be found. It reports
/// what the platform says about the list after each step, the evidence the RCA was built on.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Rca", "windows-vs-headless")]
public sealed class LaneDiagnosticTests(KofflardAppFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public void WithEveryLaneShown_ACardFromEachLane_IsFound()
    {
        var results = fixture.StartTest().SearchFor("Nick Drake").WaitForSuggestions();
        Report(results, "answered");

        // Wild and Curious first, so the list ends up scrolled away from the top.
        foreach (var musician in new[] { "Ali Farka Touré", "Bill Fay", "Vashti Bunyan" })
        {
            results.Suggestions.Shown(musician);
            Report(results, $"found {musician}");
        }
    }

    private void Report(Pages.ResultsPage results, string step)
    {
        var view = results.ItemsView;
        output.WriteLine(
            $"{step}: visible={view.GetAttribute("visible")} scrollable={view.GetAttribute("scroll.verticallyscrollable")} " +
            $"scrolled={view.GetAttribute("scroll.verticalscrollpercent")} lanes='{results.LaneHeaders.Summary()}' " +
            $"cards realised={results.Suggestions.GetItemCount()}");
    }
}
