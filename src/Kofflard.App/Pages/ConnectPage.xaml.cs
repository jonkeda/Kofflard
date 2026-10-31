using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class ConnectPage : ContentPage
{
    private readonly ConnectViewModel _viewModel;

    public ConnectPage(ConnectViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PageAutomation.Declare(this);
        _ = _viewModel.LoadAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.OnViewAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.OnViewDisappearing();
    }
}
