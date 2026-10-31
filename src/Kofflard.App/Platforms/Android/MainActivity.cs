using Android.App;
using Android.Content.PM;
using Android.OS;
using Kofflard.App.Services;
using Kofflard.Infrastructure;

namespace Kofflard.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Hands the launch intent's <c>KOFFLARD_*</c> extras to <see cref="LaunchValues"/>, then lets MAUI
    /// build the window - whose first page is what first asks for the settings.
    /// </summary>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (Intent?.Extras is { } extras && extras.KeySet() is { } keys)
        {
            LaunchValues.UseIntentExtras(keys
                .Where(key => key.StartsWith(LaunchSettings.Prefix, StringComparison.Ordinal))
                .ToDictionary(key => key, key => extras.GetString(key) ?? string.Empty));
        }

        base.OnCreate(savedInstanceState);
    }
}
