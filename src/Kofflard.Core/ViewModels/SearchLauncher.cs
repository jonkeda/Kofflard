using System.ComponentModel;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// Starts a search on the desktop design and opens the results page on its first card; with no card, no
/// page opens and the caller shows what came of it (.my/design/desktop-aurora-changes.md).
/// </summary>
/// <param name="results">Makes the view model the search runs in, and the results page then shows.</param>
/// <param name="navigator">Opens the results page.</param>
/// <param name="running">Hands the running search to the results page the navigation makes.</param>
public sealed class SearchLauncher(Func<ResultsViewModel> results, INavigator navigator, RunningSearch running)
{
    /// <summary>
    /// Searches <paramref name="artist"/>. Returns null once the results page opened (the search carries on
    /// there), or when <paramref name="cancellationToken"/> stopped it; otherwise the finished search, in the
    /// Empty, Error or Candidates state.
    /// </summary>
    public async Task<ResultsViewModel?> StartAsync(string artist, bool cached, CancellationToken cancellationToken)
    {
        var search = results();
        var firstCard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Watch(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResultsViewModel.State) && search.State == ResultsViewModel.ContentState)
            {
                firstCard.TrySetResult();
            }
        }

        search.PropertyChanged += Watch;
        try
        {
            using (cancellationToken.Register(search.Stop))
            {
                await Task.WhenAny(firstCard.Task, search.LoadAsync(artist, cached));
            }
        }
        finally
        {
            search.PropertyChanged -= Watch;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            search.Stop();
            return null;
        }

        if (search.State == ResultsViewModel.ContentState)
        {
            running.Hand(search);
            await navigator.GoToAsync(KofflardRoutes.Results);
            return null;
        }

        return search;
    }

    /// <summary>"Nothing found like Vashti Bunyan", or the error, for a search that opened no page.</summary>
    public static string NothingFound(ResultsViewModel search)
        => search.State == ResultsViewModel.ErrorState ? search.ErrorMessage : $"Nothing found like {search.Artist}";
}

/// <summary>
/// The search a results page is opened for: the launcher hands it over before it navigates, and the page
/// takes it as it is made, so it binds to the running search from the start.
/// </summary>
public sealed class RunningSearch
{
    private ResultsViewModel? _next;

    /// <summary>Hands <paramref name="search"/> to the next results page.</summary>
    public void Hand(ResultsViewModel search) => _next = search;

    /// <summary>The search handed over, once; null when the page is opened for a route's artist.</summary>
    public ResultsViewModel? Take()
    {
        var next = _next;
        _next = null;
        return next;
    }
}
