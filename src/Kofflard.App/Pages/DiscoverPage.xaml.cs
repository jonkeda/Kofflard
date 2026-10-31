using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class DiscoverPage : ContentPage
{
    private readonly DiscoverViewModel _viewModel;

    public DiscoverPage(DiscoverViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PageAutomation.Declare(this);
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

    private void OnNameFocused(object? sender, FocusEventArgs e) => _viewModel.IsEditing = true;

    private void OnNameUnfocused(object? sender, FocusEventArgs e) => _viewModel.IsEditing = false;
}
