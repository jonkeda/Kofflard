using Kofflard.Core.Services;
using Kofflard.Core.ViewModels;

namespace Kofflard.App.Pages;

public partial class ResultsPage : ContentPage, IQueryAttributable
{
    private readonly ResultsViewModel _viewModel;
    private bool _searched;

    public ResultsPage(ResultsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PageAutomation.Declare(this);
    }

    /// <summary>Searches the route's artist, once.</summary>
    /// <remarks>
    /// Shell applies the query again when the user comes back to this page (from an explored musician);
    /// searching again then would exclude everything this page shows and come back empty.
    /// </remarks>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!_searched && KofflardRoutes.ReadSearch(query) is { } search)
        {
            _searched = true;
            _ = _viewModel.LoadAsync(search.Artist, search.Cached);
        }
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

    /// <summary>Stops a running verification once the page is gone for good.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (args.NavigationType is NavigationType.Pop or NavigationType.PopToRoot)
        {
            _viewModel.Stop();
        }
    }
}
