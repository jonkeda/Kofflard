using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel _viewModel;

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PageAutomation.Declare(this);
    }

    /// <summary>Reloads the recent searches and the connection; with no AI connected, goes to Connect.</summary>
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
