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

        // The adventure stops draw their own dot; WinUI's radio button kept room for its circle beside them,
        // which pushed each stop off its column. The bare style is in Platforms\Windows\App.xaml.
        Microsoft.Maui.Handlers.RadioButtonHandler.Mapper.AppendToMapping("BareRadioButton", (handler, view) =>
        {
            if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("BareRadioButton", out var style))
            {
                handler.PlatformView.Style = (Microsoft.UI.Xaml.Style)style;
            }
        });

        // A grouped list on Windows pins the current lane's header and clips the cards under it. UI Automation
        // then reports them off-screen, and scrolling one into view puts it right back under the header, so
        // automation cannot read it (.my/rca/windows-vs-headless.md). The lane headers scroll with their cards.
        Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("NoStickyGroupHeaders", (handler, view) =>
        {
            if (handler.PlatformView is Microsoft.UI.Xaml.Controls.ListViewBase list)
            {
                list.Loaded += (_, _) =>
                {
                    if (list.ItemsPanelRoot is Microsoft.UI.Xaml.Controls.ItemsStackPanel panel)
                    {
                        panel.AreStickyGroupHeadersEnabled = false;
                    }
                };
            }
        });
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
