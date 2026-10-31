using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PageAutomation.Declare(this);
    }

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
}
