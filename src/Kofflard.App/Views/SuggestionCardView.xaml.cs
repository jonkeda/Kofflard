using Bruinet.Maui.AppSupport.Uia;
using Kofflard.App.Automation;

namespace Kofflard.App.Views;

/// <summary>One suggestion or favourite; its BindingContext is a <c>SuggestionViewModel</c>.</summary>
public partial class SuggestionCardView : ContentView
{
    public SuggestionCardView()
    {
        InitializeComponent();

#if ANDROID || IOS
        // A long press shows the other services. Only the long press: the tap is the list's, which selects
        // the card (.my/rca/card-click.md). Windows has the right-click menu instead.
        var longPress = new CommunityToolkit.Maui.Behaviors.TouchBehavior();
        longPress.SetBinding(
            CommunityToolkit.Maui.Behaviors.TouchBehavior.LongPressCommandProperty,
            new Binding(nameof(Kofflard.Core.ViewModels.SuggestionViewModel.ShowOtherServicesCommand)));
        TapSurface.Behaviors.Add(longPress);
#endif

        // The sink first: it is captured when the verbs publish the surface to the bridge.
        GestureAutomation.SetSink(TapSurface, CardGestureSink.Instance);
        GestureAutomation.SetVerbs(TapSurface, CardGestureSink.Declared);
    }
}
