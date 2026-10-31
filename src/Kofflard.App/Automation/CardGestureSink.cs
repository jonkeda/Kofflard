using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Kofflard.Core.ViewModels;

namespace Kofflard.App.Automation;

/// <summary>
/// The card's tap and long press for Bruinet's gesture bridge. A finger reaches them through the card's
/// TouchBehavior, which the bridge cannot drive (MAUI has no long-press recognizer), so the sink runs the
/// same commands the behavior binds.
/// </summary>
public sealed class CardGestureSink : IBruinetGestureSink
{
    /// <summary>The one sink; it keeps no state, the card is the element's BindingContext.</summary>
    public static CardGestureSink Instance { get; } = new();

    /// <summary>The verbs to declare on the card's tap surface.</summary>
    public const string Declared = nameof(BruinetVerb.Tap) + "," + nameof(BruinetVerb.LongPress);

    /// <inheritdoc />
    public IReadOnlyCollection<BruinetVerb> Verbs { get; } = [BruinetVerb.Tap, BruinetVerb.LongPress];

    /// <inheritdoc />
    public int Invoke(VisualElement element, BruinetVerb verb, int arg1, int arg2)
    {
        if (element.BindingContext is not SuggestionViewModel card)
        {
            return HResults.S_FALSE;
        }

        switch (verb)
        {
            case BruinetVerb.Tap:
                card.OpenCommand.Execute(null);
                return HResults.S_OK;

            case BruinetVerb.LongPress:
                card.ShowOtherServicesCommand.Execute(null);
                return HResults.S_OK;

            default:
                throw new NotSupportedException($"{nameof(CardGestureSink)} was asked for {verb}, which it does not name.");
        }
    }
}
