using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Kofflard.Core.ViewModels;

namespace Kofflard.App.Automation;

/// <summary>
/// The card's gestures for Bruinet's gesture bridge. The long press: a finger reaches it through the card's
/// TouchBehavior (a right-click on Windows), which the bridge cannot drive (MAUI has no long-press
/// recognizer), so the sink runs the same command. The tap: in a list it is the list's, and Bruinet selects
/// the card's list item, as a click does (.my/rca/card-click.md); in the desktop's lane columns, which are no
/// list, the card takes it, and the sink opens the card as the click does.
/// </summary>
public sealed class CardGestureSink : IBruinetGestureSink
{
    /// <summary>The one sink; it keeps no state, the card is the element's BindingContext.</summary>
    public static CardGestureSink Instance { get; } = new();

    /// <summary>The verbs to declare on the card's tap surface.</summary>
    public const string Declared = nameof(BruinetVerb.LongPress) + " " + nameof(BruinetVerb.Tap);

    /// <inheritdoc />
    public IReadOnlyCollection<BruinetVerb> Verbs { get; } = [BruinetVerb.LongPress, BruinetVerb.Tap];

    /// <inheritdoc />
    public int Invoke(VisualElement element, BruinetVerb verb, int arg1, int arg2)
    {
        if (element.BindingContext is not SuggestionViewModel card)
        {
            return HResults.S_FALSE;
        }

        switch (verb)
        {
            case BruinetVerb.LongPress:
                card.ShowOtherServicesCommand.Execute(null);
                return HResults.S_OK;

            case BruinetVerb.Tap:
                card.OpenCommand.Execute(null);
                return HResults.S_OK;

            default:
                throw new NotSupportedException($"{nameof(CardGestureSink)} was asked for {verb}, which it does not name.");
        }
    }
}
