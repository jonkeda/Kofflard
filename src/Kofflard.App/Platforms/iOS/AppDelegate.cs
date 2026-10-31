using Foundation;
using UIKit;

namespace Kofflard.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    /// <summary>Hands <c>kofflard://oauth/callback</c> to <c>WebAuthenticator</c> (design §6.3).</summary>
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
        => Microsoft.Maui.Authentication.WebAuthenticator.Default.OpenUrl(application, url, options) || base.OpenUrl(application, url, options);
}
