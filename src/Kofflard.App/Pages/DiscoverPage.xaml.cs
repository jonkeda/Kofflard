using Kofflard.App.Views;
using Kofflard.Core.ViewModels;
using Kofflard.Infrastructure;

namespace Kofflard.App.Pages;

public partial class DiscoverPage : ContentPage
{
    /// <summary>From this width the page is wide (.my/design/one-page-desktop.md).</summary>
    public const double WideFrom = 900;

    // A little either side of WideFrom, so a window resized across it doesn't flicker.
    private const double Hysteresis = 20;

    private readonly DiscoverViewModel _viewModel;
    private readonly KofflardLayout? _forced;
    private PhoneLayout? _narrow;
    private DesktopLayout? _wide;
    private bool? _isWide;

    public DiscoverPage(DiscoverViewModel viewModel, LaunchSettings settings)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _forced = settings.Layout;
        PageAutomation.Declare(this);
        SizeChanged += (_, _) => ChooseLayout();
        ChooseLayout();
    }

    /// <summary>Reloads the toggles and the recent searches; with no AI connected, goes to Connect.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.OnViewAppearing();
        _ = _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.OnViewDisappearing();
    }

    /// <summary>The system back walks the trail, then goes back to Recent, then leaves the app.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.HasTrail || _viewModel.HasResults)
        {
            _ = _viewModel.BackAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    // The launch may force a layout (tests); otherwise the width decides, and the page switches as the window
    // is resized. Each layout is made once and kept.
    private void ChooseLayout()
    {
        var wide = _forced switch
        {
            KofflardLayout.Phone => false,
            KofflardLayout.Desktop => true,
            _ => Width >= WideFrom + (_isWide == true ? -Hysteresis : Hysteresis),
        };

        if (_isWide == wide)
        {
            return;
        }

        _isWide = wide;
        Host.Content = wide ? _wide ??= new DesktopLayout() : _narrow ??= new PhoneLayout();
    }
}
