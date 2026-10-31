using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;

namespace Kofflard.App.Pages;

/// <summary>
/// The gesture-bridge verbs every Kofflard page declares, as in Bruinet's Todo sample: the navigation
/// questions a test asks before it acts (where are we, is the app idle, is an alert up), and toolbar
/// items, which UI Automation cannot invoke on Windows. Inert in a build without the bridge.
/// </summary>
internal static class PageAutomation
{
    private static readonly string Verbs = string.Join(
        ",",
        nameof(BruinetVerb.NavigateBack),
        nameof(BruinetVerb.GetState),
        nameof(BruinetVerb.CurrentRoute),
        nameof(BruinetVerb.IsIdle),
        nameof(BruinetVerb.CurrentAlert),
        nameof(BruinetVerb.InvokeMenuItem),
        nameof(BruinetVerb.InvokeToolbarItem));

    /// <summary>Declares the verbs on <paramref name="page"/>, whose AutomationId must already be set.</summary>
    public static void Declare(ContentPage page) => GestureAutomation.SetVerbs(page, Verbs);
}
