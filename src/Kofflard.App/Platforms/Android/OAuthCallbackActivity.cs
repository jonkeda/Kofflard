using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Kofflard.App;

/// <summary>
/// Receives <c>kofflard://oauth/callback</c> after a sign-in in Custom Tabs and hands it to
/// <c>WebAuthenticator</c> (design §6.3).
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "kofflard",
    DataHost = "oauth",
    DataPath = "/callback")]
public class OAuthCallbackActivity : WebAuthenticatorCallbackActivity
{
}
