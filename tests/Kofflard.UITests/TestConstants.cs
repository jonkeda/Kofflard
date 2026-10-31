using Bruinet.Maui.Enums;

namespace Kofflard.UITests;

/// <summary>Timeouts for the Kofflard UI tests.</summary>
public static class TestConstants
{
    /// <summary>A whole test. xUnit enforces it only on tests returning <see cref="Task"/>.</summary>
    /// <remarks>An upper bound: an Android journey with every lane open takes about a minute on the emulator.</remarks>
    public const int DefaultTestTimeoutMs = 120_000;

    /// <summary>How long a page or a state may take to appear, including an answer from the fake AI.</summary>
    public const int PageTimeoutMs = 10_000;

    /// <summary>How long a search through the results list may take: down, then up, a step at a time.</summary>
    /// <remarks>
    /// On Android a swipe moves a fraction of the list and takes about a second, and with every lane open the
    /// list is 12 cards and 3 headers: 10 s was right at the limit (.my/rca/android-and-ios.md).
    /// </remarks>
    public static int ListSearchMs(MauiPlatform platform)
        => platform == MauiPlatform.Android ? 30_000 : PageTimeoutMs;
}
