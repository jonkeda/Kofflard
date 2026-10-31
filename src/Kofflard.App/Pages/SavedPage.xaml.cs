using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class SavedPage : ContentPage
{
    private readonly SavedViewModel _viewModel;

    public SavedPage(SavedViewModel viewModel)
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
