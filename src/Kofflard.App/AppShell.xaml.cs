using Kofflard.App.Pages;
using Kofflard.Core.Services;
using Kofflard.Infrastructure;

namespace Kofflard.App;

public partial class AppShell : Shell
{
    public AppShell(LaunchSettings settings)
    {
        InitializeComponent();

        // A phone gets the one page, everything else the search page that pushes the results
        // (.my/design/single-page-phone.md); a test may ask for either.
        var layout = settings.Layout ?? (DeviceInfo.Idiom == DeviceIdiom.Phone ? KofflardLayout.Phone : KofflardLayout.Desktop);
        Items.Add(new ShellContent
        {
            Route = KofflardRoutes.Search,
            Title = "Kofflard",
            ContentTemplate = new DataTemplate(layout == KofflardLayout.Phone ? typeof(DiscoverPage) : typeof(SearchPage)),
        });

        // Pushed routes; Shell resolves the pages from the container, view models and all.
        Routing.RegisterRoute(KofflardRoutes.Results, typeof(ResultsPage));
        Routing.RegisterRoute(KofflardRoutes.Connect, typeof(ConnectPage));
        Routing.RegisterRoute(KofflardRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(KofflardRoutes.Saved, typeof(SavedPage));
    }
}
