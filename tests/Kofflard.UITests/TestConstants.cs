namespace Kofflard.UITests;

/// <summary>Timeouts for the Kofflard UI tests.</summary>
public static class TestConstants
{
    /// <summary>A whole test. xUnit enforces it only on tests returning <see cref="Task"/>.</summary>
    public const int DefaultTestTimeoutMs = 60_000;

    /// <summary>How long a page or a state may take to appear, including an answer from the fake AI.</summary>
    public const int PageTimeoutMs = 10_000;
}
