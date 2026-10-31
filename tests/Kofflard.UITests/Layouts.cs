using System.Runtime.CompilerServices;

namespace Kofflard.UITests;

/// <summary>
/// Which layout of the one page an app under test shows, by its test context (.my/design/one-page-desktop.md):
/// the page objects read the phone's one list or the desktop's lane columns by it.
/// </summary>
public static class Layouts
{
    private static readonly ConditionalWeakTable<IMauiTestContext, object> Phones = new();

    /// <summary>Records that the app behind <paramref name="context"/> shows the narrow, phone layout.</summary>
    public static void MarkPhone(IMauiTestContext context) => Phones.AddOrUpdate(context, true);

    /// <summary>Whether the app behind <paramref name="context"/> shows the narrow, phone layout.</summary>
    public static bool IsPhone(IMauiTestContext context) => Phones.TryGetValue(context, out _);
}
