using System.Runtime.CompilerServices;

namespace Kofflard.UITests;

/// <summary>
/// Which design an app under test shows, by its test context: the page objects find the phone's one page
/// or the desktop's pages by it (.my/design/single-page-phone.md).
/// </summary>
public static class Layouts
{
    private static readonly ConditionalWeakTable<IMauiTestContext, object> Phones = new();

    /// <summary>Records that the app behind <paramref name="context"/> shows the phone's design.</summary>
    public static void MarkPhone(IMauiTestContext context) => Phones.AddOrUpdate(context, true);

    /// <summary>Whether the app behind <paramref name="context"/> shows the phone's design.</summary>
    public static bool IsPhone(IMauiTestContext context) => Phones.TryGetValue(context, out _);
}
