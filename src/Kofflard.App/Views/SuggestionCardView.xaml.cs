using Bruinet.Maui.AppSupport.Uia;
using Kofflard.App.Automation;

namespace Kofflard.App.Views;

/// <summary>One suggestion or favourite; its BindingContext is a <c>SuggestionViewModel</c>.</summary>
public partial class SuggestionCardView : ContentView
{
    /// <summary>Whether the initials photo shows: the desktop's columns (.my/design/one-page-desktop.md).</summary>
    public static readonly BindableProperty ShowsPhotoProperty = BindableProperty.Create(
        nameof(ShowsPhoto), typeof(bool), typeof(SuggestionCardView), false,
        propertyChanged: (view, _, value) => ((SuggestionCardView)view).Photo.IsVisible = (bool)value);

    /// <summary>
    /// Whether a click on the card opens it. In a list the list's selection does that (.my/rca/card-click.md);
    /// the desktop's columns are no list, so the card takes the click itself.
    /// </summary>
    public static readonly BindableProperty OpensOnTapProperty = BindableProperty.Create(
        nameof(OpensOnTap), typeof(bool), typeof(SuggestionCardView), false,
        propertyChanged: (view, _, value) => ((SuggestionCardView)view).SetOpensOnTap((bool)value));

    private TapGestureRecognizer? _tap;

    /// <inheritdoc cref="ShowsPhotoProperty" />
    public bool ShowsPhoto
    {
        get => (bool)GetValue(ShowsPhotoProperty);
        set => SetValue(ShowsPhotoProperty, value);
    }

    /// <inheritdoc cref="OpensOnTapProperty" />
    public bool OpensOnTap
    {
        get => (bool)GetValue(OpensOnTapProperty);
        set => SetValue(OpensOnTapProperty, value);
    }

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

    private void SetOpensOnTap(bool opens)
    {
        if (opens && _tap is null)
        {
            _tap = new TapGestureRecognizer();
            _tap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(Kofflard.Core.ViewModels.SuggestionViewModel.OpenCommand)));
            TapSurface.GestureRecognizers.Add(_tap);
        }
        else if (!opens && _tap is not null)
        {
            TapSurface.GestureRecognizers.Remove(_tap);
            _tap = null;
        }
    }
}
