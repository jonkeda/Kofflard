using Bruinet.Maui.AppSupport.Uia;
using Kofflard.App.Automation;

namespace Kofflard.App.Views;

/// <summary>One suggestion or favourite; its BindingContext is a <c>SuggestionViewModel</c>.</summary>
public partial class SuggestionCardView : ContentView
{
    public SuggestionCardView()
    {
        InitializeComponent();

        // The sink first: it is captured when the verbs publish the surface to the bridge.
        GestureAutomation.SetSink(TapSurface, CardGestureSink.Instance);
        GestureAutomation.SetVerbs(TapSurface, CardGestureSink.Declared);
    }
}
