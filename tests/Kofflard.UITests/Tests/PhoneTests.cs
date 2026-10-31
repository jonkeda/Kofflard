using Kofflard.TestSupport;
using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Tests;

/// <summary>The phone's one page (.my/design/single-page-phone.md): search at the bottom, lanes as toggles, a trail back.</summary>
[Collection(PhoneCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Layout", "phone")]
public sealed class PhoneTests(PhoneAppFixture fixture)
{
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.06.1")]
    public Task TheToggles_ChooseWhatIsAskedFor_AndWhatShows()
    {
        var phone = fixture.StartPhoneTest();

        // A new install: Safe and Curious on, so nothing wild is asked for.
        var results = phone.SearchFor("Nick Drake");
        results.PageHeading.AssertText("Like Nick Drake");
        Assert.DoesNotContain("Wild", results.LaneHeaders.Summary());

        // Wild on: its picks are asked for on their own, and arrive under the others.
        phone.Toggle("Wild");
        Until(() => results.LaneHeaders.Summary().Contains("Wild"));

        // Safe off: its section goes, with no request.
        phone.Toggle("Safe");
        Until(() => !results.LaneHeaders.Summary().Contains("Safe"));
        return Task.CompletedTask;
    }

    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(TestConstants.PageTimeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Assert.True(condition());
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Journey", "DISC.04.1")]
    public Task ACardsSearch_ReplacesTheList_AndTheTrailGoesBack()
    {
        var phone = fixture.StartPhoneTest();
        fixture.Ai.Answer("Bill Fay", FakeAi.Canned("bill-fay"));
        var results = phone.SearchFor("Nick Drake");

        results.Suggestions.LookAt("Bill Fay");
        results.Suggestions.Card("Bill Fay").ExploreButton.Click();
        results.PageHeading.AssertText("Like Bill Fay", timeoutMs: TestConstants.PageTimeoutMs);
        results.WaitForSuggestions();
        phone.BackButton.AssertText("‹ Nick Drake");

        phone.Back().PageHeading.AssertText("Like Nick Drake", timeoutMs: TestConstants.PageTimeoutMs);
        phone.BackButton.AssertExists(false, "The trail should be gone after going back to the first search.", TestConstants.PageTimeoutMs);
        return Task.CompletedTask;
    }

    /// <summary>Saves the one page with results to <c>KOFFLARD_DIAG_SHOT</c>, to look at the design on a device.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Diagnostic", "phone-look")]
    public Task ThePage_WithResults_ForALook()
    {
        var phone = fixture.StartPhoneTest();
        phone.SearchFor("Nick Drake");
        if (Environment.GetEnvironmentVariable("KOFFLARD_DIAG_SHOT") is { } shot)
        {
            fixture.Context.SaveScreenshot(shot);
        }

        return Task.CompletedTask;
    }
}
