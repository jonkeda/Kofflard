namespace Kofflard.UITests.Containers;

/// <summary>
/// A card's tap surface (<c>Suggestion_Card</c>): a tap opens the Open with service, a long press shows the
/// other two. It is a plain view with the card's gesture sink, not a button, so a click is the Tap gesture.
/// </summary>
public sealed class CardSurface<TScope> : Button<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>Creates the surface by AutomationId.</summary>
    public CardSurface(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    /// <inheritdoc />
    protected override void ClickCore(IMauiElement element, int? timeoutMs = null)
        => element.PerformGesture(MauiGesture.Tap);
}
