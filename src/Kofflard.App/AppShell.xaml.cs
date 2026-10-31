using Kofflard.App.Pages;
using Kofflard.Core.Services;

namespace Kofflard.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // The one page, laid out for the window's width (.my/design/one-page-desktop.md).
        Items.Add(new ShellContent
        {
            Route = KofflardRoutes.Search,
            Title = "Kofflard",
            ContentTemplate = new DataTemplate(typeof(DiscoverPage)),
        });

        // Pushed routes; Shell resolves the pages from the container, view models and all.
        Routing.RegisterRoute(KofflardRoutes.Connect, typeof(ConnectPage));
        Routing.RegisterRoute(KofflardRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(KofflardRoutes.Saved, typeof(SavedPage));
    }
}
