namespace Kofflard.UITests.Containers;

/// <summary>
/// A button whose text a test reads: a card's buttons say which service they open ("▶ YouTube",
/// "Spotify"), and the order follows the Open with setting.
/// </summary>
public sealed class TextButton<TScope> : Button<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>Creates the button by AutomationId.</summary>
    public TextButton(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    /// <summary>The button's text.</summary>
    public string? GetText(int? timeoutMs = null) => RunGetWithElement(element => element.Text, timeoutMs);

    /// <summary>Asserts the button's text, waiting for it.</summary>
    public TScope AssertText(string expected, int? timeoutMs = null)
        => RunAssertWithElement(expected, element => element.Text, (want, have) => want == have, timeoutMs: timeoutMs);
}
