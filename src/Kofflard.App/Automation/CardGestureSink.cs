using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Kofflard.Core.ViewModels;

namespace Kofflard.App.Automation;

/// <summary>
/// The card's long press for Bruinet's gesture bridge. A finger reaches it through the card's TouchBehavior
/// (a right-click on Windows), which the bridge cannot drive (MAUI has no long-press recognizer), so the
/// sink runs the same command. A tap is not here: it is the list's, and Bruinet selects the card's list item,
/// as a click does (.my/rca/card-click.md).
/// </summary>
public sealed class CardGestureSink : IBruinetGestureSink
{
    /// <summary>The one sink; it keeps no state, the card is the element's BindingContext.</summary>
    public static CardGestureSink Instance { get; } = new();

    /// <summary>The verbs to declare on the card's tap surface.</summary>
    public const string Declared = nameof(BruinetVerb.LongPress);

    /// <inheritdoc />
    public IReadOnlyCollection<BruinetVerb> Verbs { get; } = [BruinetVerb.LongPress];

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

            default:
                throw new NotSupportedException($"{nameof(CardGestureSink)} was asked for {verb}, which it does not name.");
        }
    }
}
