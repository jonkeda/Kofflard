using Bruinet.Maui.AppSupport;
using CommunityToolkit.Maui;
using Kofflard.App.Services;
using Kofflard.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Kofflard.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if WINDOWS
                // Makes layouts, ContentView and Border expose their AutomationId, so Bruinet
                // containers (the cards, the state views) resolve on Windows.
                handlers.AddBruinetAutomationHandlers();
#endif
            });

#if WINDOWS
        // MAUI 10 keeps Picker.IsOpen in step with the WinUI ComboBox: on DropDownOpened it sets IsOpen, and
        // the IsOpen mapper then writes ComboBox.IsDropDownOpen back from inside that event. When the
        // dropdown was opened through UI Automation (Bruinet's ExpandCollapse), the ComboBox could still
        // report closed there, the write went through, and it blocked the UI thread for good
        // (.my/rca/picker-hang.md). Kofflard never opens a picker from code, so the mapping is not needed.
        Microsoft.Maui.Handlers.PickerHandler.Mapper.ModifyMapping(
            nameof(IPicker.IsOpen),
            (handler, picker, map) => { });
#endif

        // Off unless this build has the bridge and the test run asks for it.
        builder.UseBruinetGestureBridge();

        // Every setting a test may change, read once, on first use: on Android the launch intent that
        // carries them does not exist yet while the container is built (see LaunchValues).
        builder.Services.AddKofflardApp(
            _ => LaunchSettings.From(LaunchValues.Read, Path.Combine(FileSystem.AppDataDirectory, "kofflard.db")),
            KofflardDevice.Create());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
