using Kofflard.App.Pages;
using Kofflard.Core.Services;

namespace Kofflard.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pushed routes; Shell resolves the pages from the container, view models and all.
        Routing.RegisterRoute(KofflardRoutes.Results, typeof(ResultsPage));
        Routing.RegisterRoute(KofflardRoutes.Connect, typeof(ConnectPage));
        Routing.RegisterRoute(KofflardRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(KofflardRoutes.Saved, typeof(SavedPage));
    }
}
